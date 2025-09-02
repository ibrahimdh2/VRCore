using System.Collections;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
    public enum CurrentCollider { Small, Big};
    public CurrentCollider currentDetectionCollider;
    [Header("Movement Settings")]
    public float maxSpeed = 10f;
    public float acceleration = 5f;
    public float brakingForce = 8f;
    public float rotationSpeed = 120f;
    public float sharpTurnAngle = 45f;
    public float waypointTolerance = 1f;

    [Header("Wheel Transforms")]
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    [Header("Detection Settings")]
    public BoxCollider detectionCollider;
    public float raycastLength = 5f;
    public float boxcastLength = 1f;

    [Header("Intersection Safety Settings")]
    public BoxCollider intersectionDetectionCollider;
    public float intersectionBoxcastLength = 3f;
    public float intersectionSafetyDuration = 3f;
    public bool enableIntersectionSafety = true;

    [Header("Turn Detection Settings")]
    public float turnDetectionAngleThreshold = 10f;
    public float turnBoxcastLength = 3f;
    public bool enableTurnDetection = true;
    public Transform leftTurnRayCastStartingPoint;
    public Transform rightTurnRayCastStartingPoint;
    public float rayDistance;

    [Header("Stop/Signal Settings")]
    public SignalStoppingVehicle signalStoppingVehicle;
    public float delayTimeAfterStopping = 4f;
    public float yPos;

    [Header("Deadlock Prevention")]
    public float maxWaitTime = 3f;
    public float yieldDistance = 2f;

    [Header("Speed Control")]
    public float speedChangeRate = 2f;

    [Header("Debug Settings")]
    public bool enableDebugLogs = false;

    [SerializeField] private Transform[] waypoints;
    [SerializeField] private int currentWaypointIndex = 0;

    // Cached rotations
    private Quaternion frontLeftOriginalRotation;
    private Quaternion frontRightOriginalRotation;

    [SerializeField] private float currentSpeed = 0f;
    private bool isPaused = false;
    private bool collidingWithCar = false;
    private WaitForSeconds waitFor;

    // Deadlock prevention
    private float collisionStartTime;
    private int vehiclePriority;
    private bool isYielding = false;
    private Vector3 originalPosition;

    // Speed control
    private float targetMaxSpeed;
    private float currentMaxSpeed;

    // Progress tracking
    private Vector3 lastPosition;
    private float lastProgressTime;

    public BoxCollider currentCollider;
    private Coroutine moveRoutineCoroutine;

    // Bicycle avoidance
    private BoxCollider bicycleCollider;
    [SerializeField] private BoxCollider frontLeftCheckBoxCollider;
    [SerializeField] private BoxCollider frontRightCheckBoxCollider;
    private int leftRightBoxCastRayLength;
    public float avoidanceDistance;
    public bool checkFrontLeftAndRightAvoidBicycle;

    // Intersection safety variables
    private bool wasAtRedLight = false;
    private bool usingIntersectionDetection = false;
    private float intersectionSafetyStartTime;
    private Coroutine intersectionSafetyCoroutine;

    // Cached detection data (optimization)
    private struct DetectionBoxData
    {
        public Vector3 center;
        public Vector3 halfExtents;
        public Quaternion orientation;
    }

    private DetectionBoxData mainDetectionBox;
    private DetectionBoxData intersectionDetectionBox;
    private DetectionBoxData leftDetectionBox;
    private DetectionBoxData rightDetectionBox;
    private bool detectionDataCached = false;

    // Physics layer masks for optimized collision detection
    private int vehicleLayerMask;
    private int bicycleLayerMask;

    void Awake()
    {
        // Safe DataManager access
        if (DataManager.Instance != null)
        {
            bicycleCollider = DataManager.Instance.bikeCollider;
        }

        InitializeComponents();
        CacheDetectionData();
        SetupPhysicsLayers();
    }

    void Start()
    {
        // Retry DataManager access if failed in Awake
        if (bicycleCollider == null && DataManager.Instance != null)
        {
            bicycleCollider = DataManager.Instance.bikeCollider;
        }
    }

    private void InitializeComponents()
    {
        currentCollider = GetComponent<BoxCollider>();
        if (frontLeftWheel != null) frontLeftOriginalRotation = frontLeftWheel.localRotation;
        if (frontRightWheel != null) frontRightOriginalRotation = frontRightWheel.localRotation;

        signalStoppingVehicle = GetComponent<SignalStoppingVehicle>();
        transform.position = new Vector3(transform.position.x, yPos, transform.position.z);
        waitFor = new WaitForSeconds(delayTimeAfterStopping);

        vehiclePriority = GetInstanceID();
        targetMaxSpeed = maxSpeed;
        currentMaxSpeed = maxSpeed;

        SetupDetectionCollider();
        SetupIntersectionDetectionCollider();
    }

    private void SetupPhysicsLayers()
    {
        // Cache layer masks for better performance
        vehicleLayerMask = 1 << LayerMask.NameToLayer("Default"); // Adjust to your vehicle layer
        bicycleLayerMask = 1 << LayerMask.NameToLayer("Default");  // Adjust to your bicycle layer
    }

    private void CacheDetectionData()
    {
        if (detectionCollider != null)
        {
            mainDetectionBox = new DetectionBoxData
            {
                center = detectionCollider.transform.TransformPoint(detectionCollider.center),
                halfExtents = Vector3.Scale(detectionCollider.size, detectionCollider.transform.lossyScale) * 0.5f,
                orientation = detectionCollider.transform.rotation
            };
        }

        if (intersectionDetectionCollider != null)
        {
            intersectionDetectionBox = new DetectionBoxData
            {
                center = intersectionDetectionCollider.transform.TransformPoint(intersectionDetectionCollider.center),
                halfExtents = Vector3.Scale(intersectionDetectionCollider.size, intersectionDetectionCollider.transform.lossyScale) * 0.5f,
                orientation = intersectionDetectionCollider.transform.rotation
            };
        }

        if (frontLeftCheckBoxCollider != null)
        {
            leftDetectionBox = new DetectionBoxData
            {
                center = frontLeftCheckBoxCollider.transform.TransformPoint(frontLeftCheckBoxCollider.center),
                halfExtents = Vector3.Scale(frontLeftCheckBoxCollider.size, frontLeftCheckBoxCollider.transform.lossyScale) * 0.5f,
                orientation = frontLeftCheckBoxCollider.transform.rotation
            };
        }

        if (frontRightCheckBoxCollider != null)
        {
            rightDetectionBox = new DetectionBoxData
            {
                center = frontRightCheckBoxCollider.transform.TransformPoint(frontRightCheckBoxCollider.center),
                halfExtents = Vector3.Scale(frontRightCheckBoxCollider.size, frontRightCheckBoxCollider.transform.lossyScale) * 0.5f,
                orientation = frontRightCheckBoxCollider.transform.rotation
            };
        }

        detectionDataCached = true;
    }

    private void SetupDetectionCollider()
    {
        if (detectionCollider != null)
        {
            detectionCollider.enabled = false;
            if (enableDebugLogs)
            {
                Debug.Log($"{gameObject.name}: Detection collider setup - Size: {detectionCollider.size}");
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No detection collider assigned!");
        }
    }

    private void SetupIntersectionDetectionCollider()
    {
        if (intersectionDetectionCollider != null)
        {
            intersectionDetectionCollider.enabled = false;
            if (enableDebugLogs)
            {
                Debug.Log($"{gameObject.name}: Intersection detection collider setup - Size: {intersectionDetectionCollider.size}");
            }
        }
        else if (enableIntersectionSafety)
        {
            Debug.LogWarning($"{gameObject.name}: Intersection safety enabled but no intersection detection collider assigned!");
        }
    }

    // Optimized detection methods - use cached data
    private Vector3 GetDetectionBoxCenter() => detectionDataCached ? mainDetectionBox.center : transform.position + Vector3.up * 0.5f;
    private Vector3 GetDetectionBoxSize() => detectionDataCached ? mainDetectionBox.halfExtents : new Vector3(0.25f, 0.25f, 0.25f);
    private Quaternion GetDetectionBoxOrientation() => detectionDataCached ? mainDetectionBox.orientation : transform.rotation;

    private Vector3 GetIntersectionDetectionBoxCenter() => detectionDataCached ? intersectionDetectionBox.center : transform.position + Vector3.up * 0.5f;
    private Vector3 GetIntersectionDetectionBoxSize() => detectionDataCached ? intersectionDetectionBox.halfExtents : new Vector3(0.5f, 0.5f, 0.5f);
    private Quaternion GetIntersectionDetectionBoxOrientation() => detectionDataCached ? intersectionDetectionBox.orientation : transform.rotation;

    private void FixedUpdate()
    {
        UpdateMaxSpeed();
        CheckTrafficLightState();

        // Update cached detection data less frequently for performance
        if (Time.fixedTime % 0.1f < Time.fixedDeltaTime) // Every 0.1 seconds
        {
            UpdateDetectionCache();
        }
    }

    private void CheckTrafficLightState()
    {
        if (!enableIntersectionSafety || signalStoppingVehicle?.signal == null) return;

        bool currentlyAtRedLight = signalStoppingVehicle.signal.State != LightState.Green;

        // Detect transition from red to green
        if (wasAtRedLight && !currentlyAtRedLight)
        {
            // Light just changed from red to green
            StartIntersectionSafety();
            if (enableDebugLogs)
            {
                Debug.Log($"{gameObject.name}: Traffic light changed to green, activating intersection safety mode");
            }
        }

        wasAtRedLight = currentlyAtRedLight;
    }

    private void StartIntersectionSafety()
    {
        if (intersectionDetectionCollider == null) return;

        usingIntersectionDetection = true;
        intersectionSafetyStartTime = Time.time;

        // Stop any existing intersection safety coroutine
        if (intersectionSafetyCoroutine != null)
        {
            StopCoroutine(intersectionSafetyCoroutine);
        }

        intersectionSafetyCoroutine = StartCoroutine(IntersectionSafetyTimer());
    }

    private IEnumerator IntersectionSafetyTimer()
    {
        yield return new WaitForSeconds(intersectionSafetyDuration);
        EndIntersectionSafety();
    }

    private void EndIntersectionSafety()
    {
        usingIntersectionDetection = false;
        if (enableDebugLogs)
        {
            Debug.Log($"{gameObject.name}: Intersection safety mode ended, returning to normal detection");
        }
    }

    private void UpdateDetectionCache()
    {
        if (detectionCollider != null)
        {
            mainDetectionBox.center = detectionCollider.transform.TransformPoint(detectionCollider.center);
            mainDetectionBox.orientation = detectionCollider.transform.rotation;
        }

        if (intersectionDetectionCollider != null)
        {
            intersectionDetectionBox.center = intersectionDetectionCollider.transform.TransformPoint(intersectionDetectionCollider.center);
            intersectionDetectionBox.orientation = intersectionDetectionCollider.transform.rotation;
        }

        if (frontLeftCheckBoxCollider != null)
        {
            leftDetectionBox.center = frontLeftCheckBoxCollider.transform.TransformPoint(frontLeftCheckBoxCollider.center);
            leftDetectionBox.orientation = frontLeftCheckBoxCollider.transform.rotation;
        }

        if (frontRightCheckBoxCollider != null)
        {
            rightDetectionBox.center = frontRightCheckBoxCollider.transform.TransformPoint(frontRightCheckBoxCollider.center);
            rightDetectionBox.orientation = frontRightCheckBoxCollider.transform.rotation;
        }
    }

    private IEnumerator MoveRoutine()
    {
        if (waypoints == null || waypoints.Length == 0) yield break;

        lastPosition = transform.position;
        lastProgressTime = Time.time;

        // Cache frequently used values
        Transform thisTransform = transform;
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        float deltaTime;

        while (true)
        {
            if (waypoints == null || waypoints.Length == 0) break;

            deltaTime = Time.deltaTime;
            Transform target = waypoints[currentWaypointIndex];
            Vector3 toTarget = target.position - thisTransform.position;
            toTarget.y = 0;
            float distanceSqr = toTarget.sqrMagnitude; // Use sqrMagnitude for performance

            // Waypoint switching with squared distance
            if (distanceSqr < waypointTolerance * waypointTolerance)
            {
                if (currentWaypointIndex + 1 > waypoints.Length - 1)
                {
                    VehiclePoolManager.Instance.ReturnCar(gameObject);
                }
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                lastPosition = thisTransform.position;
                lastProgressTime = Time.time;
                yield return null;
                continue;
            }

            // Handle yielding state
            if (isYielding)
            {
                HandleYieldingStep(deltaTime);
                if (Time.time - lastProgressTime > maxWaitTime + 2f)
                {
                    EndYieldingImmediate();
                }
                yield return null;
                continue;
            }

            // Cached direction calculation
            Vector3 currentDir = thisTransform.forward;
            Vector3 normalizedToTarget = toTarget.normalized;
            float angleToTarget = Vector3.Angle(currentDir, normalizedToTarget);

            // Optimized obstacle detection
            bool shouldPause = CheckObstacles(deltaTime);

            // Traffic signal check (only if not already paused)
            if (!shouldPause && signalStoppingVehicle?.signal != null)
            {
                shouldPause = signalStoppingVehicle.signal.State != LightState.Green;
            }

            isPaused = shouldPause;

            if (isPaused)
            {
                HandleDeadlockDetection();
                yield return null;
                continue;
            }

            // Movement execution
            ExecuteMovement(normalizedToTarget, angleToTarget, deltaTime);

            yield return null;
        }
    }

    private bool CheckObstacles(float deltaTime)
    {
        if (collidingWithCar) return true;

        // Choose detection system based on intersection safety mode
        bool obstacleDetected = usingIntersectionDetection ?
            CheckIntersectionObstacles() :
            CheckNormalObstacles();

        if (obstacleDetected) return true;

        // Bicycle avoidance (only if enabled and bicycle exists)
        if (checkFrontLeftAndRightAvoidBicycle && bicycleCollider != null)
        {
            return HandleBicycleAvoidance(deltaTime);
        }

        // Turn detection
        if (enableTurnDetection)
        {
            return CheckTurnObstacles();
        }

        return false;
    }

    private bool CheckNormalObstacles()
    {
        // Forward obstacle check with normal detection box
        if (Physics.BoxCast(mainDetectionBox.center, mainDetectionBox.halfExtents,
            transform.forward, out RaycastHit hit, mainDetectionBox.orientation, boxcastLength))
        {
            if (hit.collider.CompareTag("Vehicle"))
            {
                return true;
            }
        }
        return false;
    }

    private bool CheckIntersectionObstacles()
    {
        if (intersectionDetectionCollider == null)
        {
            // Fallback to normal detection if intersection collider is not set
            return CheckNormalObstacles();
        }

        // Forward obstacle check with larger intersection detection box
        if (Physics.BoxCast(intersectionDetectionBox.center, intersectionDetectionBox.halfExtents,
            transform.forward, out RaycastHit hit, intersectionDetectionBox.orientation, intersectionBoxcastLength))
        {
            if (hit.collider.CompareTag("Vehicle"))
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"{gameObject.name}: Intersection safety detected vehicle: {hit.collider.name}");
                }
                return true;
            }
        }
        return false;
    }

    private bool HandleBicycleAvoidance(float deltaTime)
    {
        // Single-pass detection for both sides
        bool leftHasBicycle = false, rightHasBicycle = false;
        bool leftHasVehicle = false, rightHasVehicle = false;

        // Left side check
        if (frontLeftCheckBoxCollider != null &&
            Physics.BoxCast(leftDetectionBox.center, leftDetectionBox.halfExtents,
            transform.forward, out RaycastHit leftHit, leftDetectionBox.orientation, leftRightBoxCastRayLength))
        {
            if (leftHit.collider == bicycleCollider)
                leftHasBicycle = true;
            else if (leftHit.collider.CompareTag("Vehicle"))
                leftHasVehicle = true;
        }

        // Right side check
        if (frontRightCheckBoxCollider != null &&
            Physics.BoxCast(rightDetectionBox.center, rightDetectionBox.halfExtents,
            transform.forward, out RaycastHit rightHit, rightDetectionBox.orientation, leftRightBoxCastRayLength))
        {
            if (rightHit.collider == bicycleCollider)
                rightHasBicycle = true;
            else if (rightHit.collider.CompareTag("Vehicle"))
                rightHasVehicle = true;
        }

        // Optimized decision logic
        if (leftHasBicycle || rightHasBicycle)
        {
            if (leftHasBicycle && rightHasBicycle)
            {
                if (enableDebugLogs) Debug.Log($"{name}: Both sides have bicycles - stopping");
                return true;
            }

            if (leftHasBicycle && !rightHasVehicle)
            {
                // Move right to avoid left bicycle
                transform.position += transform.right * (avoidanceDistance * deltaTime);
                UpdateProgressTracking();
                if (enableDebugLogs) Debug.Log($"{name}: Avoiding left bicycle");
                return false;
            }

            if (rightHasBicycle && !leftHasVehicle)
            {
                // Move left to avoid right bicycle
                transform.position -= transform.right * (avoidanceDistance * deltaTime);
                UpdateProgressTracking();
                if (enableDebugLogs) Debug.Log($"{name}: Avoiding right bicycle");
                return false;
            }

            // Bicycle present but opposite side blocked
            if (enableDebugLogs) Debug.Log($"{name}: Bicycle detected but escape route blocked");
            return true;
        }

        return false;
    }

    private bool CheckTurnObstacles()
    {
        Vector3 currentDir = transform.forward;
        Vector3 toTarget = waypoints[currentWaypointIndex].position - transform.position;
        float angleToTarget = Vector3.Angle(currentDir, toTarget.normalized);

        if (angleToTarget <= turnDetectionAngleThreshold) return false;

        Vector3 turnDirection = toTarget.normalized;
        Transform[] rayOrigins = { leftTurnRayCastStartingPoint, rightTurnRayCastStartingPoint };

        foreach (var rayOrigin in rayOrigins)
        {
            if (rayOrigin == null) continue;

            if (Physics.Raycast(rayOrigin.position, turnDirection, out RaycastHit turnHit, rayDistance))
            {
                if (turnHit.collider.CompareTag("Vehicle") && turnHit.collider != currentCollider)
                {
                    if (enableDebugLogs)
                        Debug.Log($"{name}: Turn blocked by vehicle from {rayOrigin.name}");
                    return true;
                }
            }
        }

        return false;
    }

    private void ExecuteMovement(Vector3 normalizedToTarget, float angleToTarget, float deltaTime)
    {
        // Speed calculation
        float targetSpeed = (angleToTarget > sharpTurnAngle) ? currentMaxSpeed * 0.5f : currentMaxSpeed;

        float speedDelta = (currentSpeed < targetSpeed ? acceleration : brakingForce) * deltaTime;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedDelta);

        // Rotation
        float turnMultiplier = Mathf.Lerp(0.3f, 1f, angleToTarget / 90f);
        Quaternion targetRot = Quaternion.LookRotation(normalizedToTarget);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot,
            rotationSpeed * turnMultiplier * deltaTime);

        // Movement
        transform.position += transform.forward * (currentSpeed * deltaTime);

        // Wheel animation
        AnimateWheels(currentSpeed * 360f * deltaTime, angleToTarget);

        // Progress tracking
        UpdateProgressTracking();
    }

    private void UpdateProgressTracking()
    {
        lastPosition = transform.position;
        lastProgressTime = Time.time;
    }

    private void HandleDeadlockDetection()
    {
        if (Vector3.Distance(transform.position, lastPosition) > 0.1f)
        {
            UpdateProgressTracking();
        }
        else if (Time.time - lastProgressTime > maxWaitTime)
        {
            if (enableDebugLogs) Debug.Log($"{name}: Deadlock detected, resolving...");
            ResolveDeadlock();
            lastProgressTime = Time.time;
        }
    }

    private void HandleYieldingStep(float deltaTime)
    {
        float yieldSpeed = Mathf.Max(1f, currentMaxSpeed * 0.3f);
        transform.position -= transform.forward * (yieldSpeed * deltaTime);

        AnimateWheels(-yieldSpeed * 360f * deltaTime, 0f);

        if (Vector3.Distance(transform.position, originalPosition) > yieldDistance)
        {
            StartCoroutine(EndYielding());
        }
    }

    private void AnimateWheels(float rotationAmount, float steerAngle)
    {
        // Optimized wheel rotation
        Vector3 wheelRotation = Vector3.right * (-rotationAmount);

        rearLeftWheel?.Rotate(wheelRotation);
        rearRightWheel?.Rotate(wheelRotation);

        float steerAmount = steerAngle * 0.5f;

        if (frontLeftWheel != null)
        {
            frontLeftWheel.localRotation = frontLeftOriginalRotation * Quaternion.Euler(0, steerAmount, 0);
            frontLeftWheel.Rotate(wheelRotation);
        }

        if (frontRightWheel != null)
        {
            frontRightWheel.localRotation = frontRightOriginalRotation * Quaternion.Euler(0, steerAmount, 0);
            frontRightWheel.Rotate(wheelRotation);
        }
    }

    private IEnumerator EndYielding()
    {
        yield return new WaitForSeconds(0.8f);
        isYielding = false;
        currentSpeed = 0f;
        UpdateProgressTracking();
    }

    private void EndYieldingImmediate()
    {
        isYielding = false;
        currentSpeed = 0f;
        UpdateProgressTracking();
    }

    private void ResolveDeadlock()
    {
        // Optimized nearby vehicle search
        Collider[] nearby = Physics.OverlapSphere(transform.position, 3f, vehicleLayerMask);
        CarMovementController otherCar = null;

        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;

            var comp = col.GetComponent<CarMovementController>();
            if (comp != null && comp.collidingWithCar)
            {
                otherCar = comp;
                break;
            }
        }

        if (otherCar != null)
        {
            if (vehiclePriority < otherCar.vehiclePriority)
                StartYielding();
            else if (vehiclePriority > otherCar.vehiclePriority)
                ForceResume();
            else
            {
                if (transform.position.x < otherCar.transform.position.x)
                {
                    StartYielding();

                }
                else
                {
                    ForceResume();
                }

            }
        }
        else
        {
            ForceResume();
        }
    }

    private void StartYielding()
    {
        isYielding = true;
        originalPosition = transform.position;
        collidingWithCar = false;
        isPaused = false;
        currentSpeed = 0f;
        lastProgressTime = Time.time;
    }

    private void ForceResume()
    {
        collidingWithCar = false;
        isPaused = false;
        transform.position += transform.forward * 0.05f;
        UpdateProgressTracking();
    }

    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
        currentWaypointIndex = 1;

        if (waypoints != null && waypoints.Length > 1)
        {
            Vector3 wayPointDir = (waypoints[1].position - transform.position).normalized;
            wayPointDir.y = 0;
            transform.rotation = Quaternion.LookRotation(wayPointDir);
        }

        currentSpeed = 0f;

        if (moveRoutineCoroutine != null) StopCoroutine(moveRoutineCoroutine);
        moveRoutineCoroutine = StartCoroutine(MoveRoutine());
    }

    // Public interface methods
    public void Pause() => isPaused = true;
    public void Resume() => isPaused = false;
    public void SetMaxSpeed(float newMaxSpeed) => targetMaxSpeed = Mathf.Max(0f, newMaxSpeed);
    public void IncreaseMaxSpeed(float amount) => targetMaxSpeed = Mathf.Max(0f, targetMaxSpeed + amount);
    public void DecreaseMaxSpeed(float amount) => targetMaxSpeed = Mathf.Max(0f, targetMaxSpeed - amount);
    public float GetCurrentMaxSpeed() => currentMaxSpeed;
    public float GetTargetMaxSpeed() => targetMaxSpeed;
    public bool IsSpeedChanging() => Mathf.Abs(currentMaxSpeed - targetMaxSpeed) > 0.01f;

    // Intersection safety public interface
    public void ForceStartIntersectionSafety() => StartIntersectionSafety();
    public void ForceEndIntersectionSafety() => EndIntersectionSafety();
    public bool IsUsingIntersectionDetection() => usingIntersectionDetection;

    private void UpdateMaxSpeed()
    {
        if (currentMaxSpeed != targetMaxSpeed)
        {
            currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, targetMaxSpeed, speedChangeRate * Time.deltaTime);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Vehicle"))
        {
            if (enableDebugLogs) Debug.Log($"{name}: Collision with vehicle");

            collidingWithCar = true;
            collisionStartTime = Time.time;
            isPaused = true;
            lastProgressTime = Time.time;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Vehicle"))
        {
            if (enableDebugLogs) Debug.Log($"{name}: Collision ended");
            StartCoroutine(ResumeAfterDelay());
        }
    }

    private IEnumerator ResumeAfterDelay()
    {
        yield return waitFor;
        collidingWithCar = false;
        isPaused = false;
        UpdateProgressTracking();
    }

    // Optimized gizmo drawing
    private void OnDrawGizmosSelected()
    {
        if (detectionCollider == null && intersectionDetectionCollider == null) return;

        // Draw normal detection box
        if (detectionCollider != null && !usingIntersectionDetection)
        {
            Gizmos.color = Color.red;
            Vector3 boxCenter = GetDetectionBoxCenter();
            Vector3 halfExtents = GetDetectionBoxSize();
            Vector3 castEndCenter = boxCenter + transform.forward * boxcastLength;

            Gizmos.matrix = Matrix4x4.TRS(castEndCenter, GetDetectionBoxOrientation(), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2);

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(boxCenter, castEndCenter);
        }

        // Draw intersection detection box
        if (intersectionDetectionCollider != null && usingIntersectionDetection)
        {
            Gizmos.color = Color.blue;
            Vector3 intersectionBoxCenter = GetIntersectionDetectionBoxCenter();
            Vector3 intersectionHalfExtents = GetIntersectionDetectionBoxSize();
            Vector3 intersectionCastEndCenter = intersectionBoxCenter + transform.forward * intersectionBoxcastLength;

            Gizmos.matrix = Matrix4x4.TRS(intersectionCastEndCenter, GetIntersectionDetectionBoxOrientation(), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, intersectionHalfExtents * 2);

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(intersectionBoxCenter, intersectionCastEndCenter);
        }

        // Show intersection safety area when enabled but not active
        if (enableIntersectionSafety && intersectionDetectionCollider != null && !usingIntersectionDetection)
        {
            Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
            Vector3 intersectionBoxCenter = GetIntersectionDetectionBoxCenter();
            Vector3 intersectionHalfExtents = GetIntersectionDetectionBoxSize();
            Vector3 intersectionCastEndCenter = intersectionBoxCenter + transform.forward * intersectionBoxcastLength;

            Gizmos.matrix = Matrix4x4.TRS(intersectionCastEndCenter, GetIntersectionDetectionBoxOrientation(), Vector3.one);
            Gizmos.DrawCube(Vector3.zero, intersectionHalfExtents * 2);

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}