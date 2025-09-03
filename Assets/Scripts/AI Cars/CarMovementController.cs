using System.Collections;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
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
    public BoxCollider intersectionDetectionCollider;
    public float raycastLength = 5f;
    public float boxcastLength = 1f;

    [Header("Intersection Safety Settings")]
    public float intersectionSafetyDuration = 3f;

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

    [Header("Waypoint Timeout Settings")]
    public float waypointTimeoutBuffer = 2f; // Extra time buffer beyond calculated travel time
    public float minWaypointTimeout = 5f; // Minimum timeout regardless of distance
    public float maxWaypointTimeout = 15f; // Maximum timeout to prevent indefinite waiting

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

    // Waypoint timeout system - MODIFIED
    private float waypointStartTime;
    private float waypointTimeoutDuration;
    private Vector3 waypointStartPosition;
    private float initialDistanceToWaypoint;
    private float pausedTimeAccumulator = 0f; // NEW: Tracks time spent paused
    private bool waypointTimerPaused = false; // NEW: Flag to pause timer

    public BoxCollider currentCollider;
    private Coroutine moveRoutineCoroutine;

    // Bicycle avoidance
    private BoxCollider bicycleCollider;
    [SerializeField] private BoxCollider frontLeftCheckBoxCollider;
    [SerializeField] private BoxCollider frontRightCheckBoxCollider;
    private int leftRightBoxCastRayLength;
    public float avoidanceDistance;
    public bool checkFrontLeftAndRightAvoidBicycle;

    // NEW: Bicycle collision tracking
    private bool collidingWithBicycle = false;

    // Intersection safety variables
    private bool useIntersectionCollider = false;
    private float intersectionSafetyStartTime;
    private LightState previousSignalState = LightState.Red;

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

    private float deadLockResumeDelay = 3f;
    private WaitForSeconds deadLockResumeWaitFor;

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
        deadLockResumeWaitFor = new WaitForSeconds(deadLockResumeDelay);
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

        if (intersectionDetectionCollider != null)
        {
            intersectionDetectionCollider.enabled = false;
            if (enableDebugLogs)
            {
                Debug.Log($"{gameObject.name}: Intersection detection collider setup - Size: {intersectionDetectionCollider.size}");
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No intersection detection collider assigned!");
        }
    }

    // Optimized detection methods - use cached data with intersection safety
    private Vector3 GetActiveDetectionBoxCenter()
    {
        if (useIntersectionCollider && intersectionDetectionCollider != null)
            return detectionDataCached ? intersectionDetectionBox.center : transform.position + Vector3.up * 0.5f;
        return detectionDataCached ? mainDetectionBox.center : transform.position + Vector3.up * 0.5f;
    }

    private Vector3 GetActiveDetectionBoxSize()
    {
        if (useIntersectionCollider && intersectionDetectionCollider != null)
            return detectionDataCached ? intersectionDetectionBox.halfExtents : new Vector3(0.5f, 0.5f, 0.5f);
        return detectionDataCached ? mainDetectionBox.halfExtents : new Vector3(0.25f, 0.25f, 0.25f);
    }

    private Quaternion GetActiveDetectionBoxOrientation()
    {
        if (useIntersectionCollider && intersectionDetectionCollider != null)
            return detectionDataCached ? intersectionDetectionBox.orientation : transform.rotation;
        return detectionDataCached ? mainDetectionBox.orientation : transform.rotation;
    }

    private void FixedUpdate()
    {
        UpdateMaxSpeed();
        UpdateIntersectionSafety();

        // Update cached detection data less frequently for performance
        if (Time.fixedTime % 0.1f < Time.fixedDeltaTime) // Every 0.1 seconds
        {
            UpdateDetectionCache();
        }
    }

    private void UpdateIntersectionSafety()
    {
        // Check if we need to activate intersection safety
        if (signalStoppingVehicle?.signal != null)
        {
            LightState currentState = signalStoppingVehicle.signal.State;

            // Detect transition from Red to Green
            if (previousSignalState == LightState.Red && currentState == LightState.Green)
            {
                ActivateIntersectionSafety();
                if (enableDebugLogs)
                {
                    Debug.Log($"{name}: Signal changed from Red to Green - Activating intersection safety for {intersectionSafetyDuration} seconds");
                }
            }

            previousSignalState = currentState;
        }

        // Check if intersection safety period has expired
        if (useIntersectionCollider && Time.time - intersectionSafetyStartTime >= intersectionSafetyDuration)
        {
            DeactivateIntersectionSafety();
            if (enableDebugLogs)
            {
                Debug.Log($"{name}: Intersection safety period ended - Switching back to normal detection");
            }
        }
    }

    private void ActivateIntersectionSafety()
    {
        useIntersectionCollider = true;
        intersectionSafetyStartTime = Time.time;
    }

    private void DeactivateIntersectionSafety()
    {
        useIntersectionCollider = false;
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

    // MODIFIED: Calculate timeout for reaching waypoint based on distance and speed
    private void SetWaypointTimeout(Transform targetWaypoint)
    {
        waypointStartTime = Time.time;
        waypointStartPosition = transform.position;
        pausedTimeAccumulator = 0f; // Reset paused time accumulator

        // Calculate distance to waypoint
        initialDistanceToWaypoint = Vector3.Distance(transform.position, targetWaypoint.position);

        // Calculate expected travel time based on current max speed (with safety factor for acceleration/deceleration)
        float averageSpeed = currentMaxSpeed * 0.7f; // Account for acceleration/deceleration and obstacles
        float expectedTravelTime = initialDistanceToWaypoint / Mathf.Max(averageSpeed, 1f);

        // Add buffer time and apply min/max constraints
        waypointTimeoutDuration = Mathf.Clamp(expectedTravelTime + waypointTimeoutBuffer, minWaypointTimeout, maxWaypointTimeout);

        if (enableDebugLogs)
        {
            Debug.Log($"{name}: Set waypoint timeout - Distance: {initialDistanceToWaypoint:F1}m, Expected time: {expectedTravelTime:F1}s, Total timeout: {waypointTimeoutDuration:F1}s");
        }
    }

    // MODIFIED: Check if waypoint timeout has been exceeded (accounting for paused time)
    private bool IsWaypointTimedOut()
    {
        float elapsed = Time.time - waypointStartTime - pausedTimeAccumulator;
        bool timedOut = elapsed > waypointTimeoutDuration;

        if (timedOut && enableDebugLogs)
        {
            float currentDistance = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
            Debug.Log($"{name}: Waypoint timeout! Elapsed: {elapsed:F1}s, Timeout: {waypointTimeoutDuration:F1}s, Distance remaining: {currentDistance:F1}m, Paused time: {pausedTimeAccumulator:F1}s");
        }

        return timedOut;
    }

    // NEW: Update paused time accumulator
    private void UpdateWaypointTimer(bool shouldPauseTimer)
    {
        if (shouldPauseTimer && !waypointTimerPaused)
        {
            // Just started pausing
            waypointTimerPaused = true;
            if (enableDebugLogs)
            {
                Debug.Log($"{name}: Pausing waypoint timer (signal/obstacle)");
            }
        }
        else if (!shouldPauseTimer && waypointTimerPaused)
        {
            // Just resumed
            waypointTimerPaused = false;
            if (enableDebugLogs)
            {
                Debug.Log($"{name}: Resuming waypoint timer, total paused time: {pausedTimeAccumulator:F1}s");
            }
        }

        // Accumulate paused time
        if (waypointTimerPaused)
        {
            pausedTimeAccumulator += Time.deltaTime;
        }
    }

    // NEW: Force skip to next waypoint
    private void ForceNextWaypoint()
    {
        if (enableDebugLogs)
        {
            Debug.Log($"{name}: Forcing skip to next waypoint due to timeout");
        }

        // Move to next waypoint
        if (currentWaypointIndex + 1 > waypoints.Length - 1)
        {
            VehiclePoolManager.Instance.ReturnCar(gameObject);
            return;
        }

        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;

        // Set new waypoint timeout
        SetWaypointTimeout(waypoints[currentWaypointIndex]);

        // Reset states
        lastPosition = transform.position;
        lastProgressTime = Time.time;
        collidingWithCar = false;
        collidingWithBicycle = false; // NEW: Reset bicycle collision
        isPaused = false;
        isYielding = false;
        currentSpeed = 0f;
        waypointTimerPaused = false; // NEW: Reset timer pause state
    }

    private IEnumerator MoveRoutine()
    {
        if (waypoints == null || waypoints.Length == 0) yield break;

        lastPosition = transform.position;
        lastProgressTime = Time.time;

        // Initialize waypoint timeout for first waypoint
        if (waypoints.Length > currentWaypointIndex)
        {
            SetWaypointTimeout(waypoints[currentWaypointIndex]);
        }

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

                // Set timeout for new waypoint
                if (waypoints.Length > currentWaypointIndex)
                {
                    SetWaypointTimeout(waypoints[currentWaypointIndex]);
                }

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
            bool stoppedAtSignal = false;
            if (!shouldPause && signalStoppingVehicle?.signal != null)
            {
                stoppedAtSignal = signalStoppingVehicle.signal.State != LightState.Green;
                shouldPause = stoppedAtSignal;
            }

            // MODIFIED: Update waypoint timer based on pause reasons
            bool shouldPauseTimer = stoppedAtSignal || collidingWithBicycle; // NEW: Pause timer for signals and bicycle collisions
            UpdateWaypointTimer(shouldPauseTimer);

            isPaused = shouldPause;

            if (isPaused)
            {
                // MODIFIED: Only handle deadlock if not colliding with bicycle and not stopped at signal
                if (!collidingWithBicycle && !stoppedAtSignal)
                {
                    HandleDeadlockDetection();
                }
                yield return null;
                continue;
            }

            // MODIFIED: Check waypoint timeout after checking if we should pause
            if (!waypointTimerPaused && IsWaypointTimedOut())
            {
                ForceNextWaypoint();
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

        // Forward obstacle check - use active detection collider (intersection or normal)
        Vector3 activeCenter = GetActiveDetectionBoxCenter();
        Vector3 activeHalfExtents = GetActiveDetectionBoxSize();
        Quaternion activeOrientation = GetActiveDetectionBoxOrientation();

        if (Physics.BoxCast(activeCenter, activeHalfExtents,
            transform.forward, out RaycastHit hit, activeOrientation, boxcastLength))
        {
            if (hit.collider.CompareTag("Vehicle"))
            {
                // NEW: Check if it's a bicycle collision
                if (hit.collider == bicycleCollider)
                {
                    collidingWithBicycle = true;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"{name}: Detected bicycle ahead - pausing and pausing waypoint timer");
                    }
                }
                else
                {
                    if (enableDebugLogs && useIntersectionCollider)
                    {
                        Debug.Log($"{name}: Intersection safety detection - Vehicle detected ahead");
                    }
                }
                return true;
            }
        }
        else
        {
            // NEW: Clear bicycle collision flag when no longer detecting bicycle
            if (collidingWithBicycle)
            {
                collidingWithBicycle = false;
                if (enableDebugLogs)
                {
                    Debug.Log($"{name}: No longer detecting bicycle - resuming");
                }
            }
        }

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
            {
                leftHasBicycle = true;
                collidingWithBicycle = true; // NEW: Set bicycle collision flag
            }
            else if (leftHit.collider.CompareTag("Vehicle"))
                leftHasVehicle = true;
        }

        // Right side check
        if (frontRightCheckBoxCollider != null &&
            Physics.BoxCast(rightDetectionBox.center, rightDetectionBox.halfExtents,
            transform.forward, out RaycastHit rightHit, rightDetectionBox.orientation, leftRightBoxCastRayLength))
        {
            if (rightHit.collider == bicycleCollider)
            {
                rightHasBicycle = true;
                collidingWithBicycle = true; // NEW: Set bicycle collision flag
            }
            else if (rightHit.collider.CompareTag("Vehicle"))
                rightHasVehicle = true;
        }

        // NEW: Clear bicycle collision if no bicycle detected on either side
        if (!leftHasBicycle && !rightHasBicycle && collidingWithBicycle)
        {
            collidingWithBicycle = false;
            if (enableDebugLogs)
            {
                Debug.Log($"{name}: No longer avoiding bicycle");
            }
        }

        // Optimized decision logic
        if (leftHasBicycle || rightHasBicycle)
        {
            if (leftHasBicycle && rightHasBicycle)
            {
                if (enableDebugLogs) Debug.Log($"{name}: Both sides have bicycles - stopping and pausing waypoint timer");
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
            if (enableDebugLogs) Debug.Log($"{name}: Bicycle detected but escape route blocked - stopping and pausing waypoint timer");
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
                    // NEW: Check if it's a bicycle
                    if (turnHit.collider == bicycleCollider)
                    {
                        collidingWithBicycle = true;
                        if (enableDebugLogs)
                            Debug.Log($"{name}: Turn blocked by bicycle from {rayOrigin.name} - pausing waypoint timer");
                    }
                    else
                    {
                        if (enableDebugLogs)
                            Debug.Log($"{name}: Turn blocked by vehicle from {rayOrigin.name}");
                    }
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
        // MODIFIED: Don't resolve deadlock if colliding with bicycle
        if (collidingWithBicycle)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{name}: Deadlock resolution skipped - colliding with bicycle");
            }
            return;
        }

        Collider[] nearby = Physics.OverlapSphere(transform.position, 3f, vehicleLayerMask);
        CarMovementController otherCar = null;

        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;

            var comp = col.GetComponent<CarMovementController>();
            if (comp != null && comp.collidingWithCar && !comp.collidingWithBicycle) // NEW: Only consider cars not colliding with bicycles
            {
                otherCar = comp;
                break;
            }
        }

        if (otherCar != null)
        {
            Vector3 myForward = transform.forward.normalized;
            Vector3 otherForward = otherCar.transform.forward.normalized;

            bool sameDirection = Vector3.Dot(myForward, otherForward) > 0.9f; // close to parallel

            if (sameDirection)
            {
                // Check which one is ahead (compare along the forward direction)
                float myProjection = Vector3.Dot(transform.position, myForward);
                float otherProjection = Vector3.Dot(otherCar.transform.position, myForward);

                if (myProjection > otherProjection)
                {
                    // I'm ahead → move immediately
                    ForceResume();
                    // other car will wait
                    otherCar.StartCoroutine(otherCar.ResumeWithDelay());
                }
                else
                {
                    // I'm behind → wait 3s
                    StartCoroutine(ResumeWithDelay());
                    // let the other car move immediately
                    otherCar.ForceResume();
                }
            }
            else
            {
                // Fallback to your existing priority-based logic
                if (vehiclePriority < otherCar.vehiclePriority)
                    StartYielding();
                else if (vehiclePriority > otherCar.vehiclePriority)
                    ForceResume();
                else
                {
                    if (transform.position.x < otherCar.transform.position.x)
                        StartYielding();
                    else
                        ForceResume();
                }
            }
        }
        else
        {
            ForceResume();
        }
    }

    private IEnumerator ResumeWithDelay()
    {
        yield return deadLockResumeWaitFor;
        collidingWithCar = false;
        isPaused = false;
        UpdateProgressTracking();
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

            // Set initial waypoint timeout
            SetWaypointTimeout(waypoints[currentWaypointIndex]);
        }

        currentSpeed = 0f;
        // NEW: Reset states
        collidingWithBicycle = false;
        waypointTimerPaused = false;
        pausedTimeAccumulator = 0f;

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

    // MODIFIED: Public methods for waypoint timeout information (accounting for paused time)
    public float GetWaypointTimeRemaining()
    {
        float elapsed = Time.time - waypointStartTime - pausedTimeAccumulator;
        return Mathf.Max(0f, waypointTimeoutDuration - elapsed);
    }

    public float GetWaypointProgress()
    {
        if (waypoints == null || waypoints.Length <= currentWaypointIndex) return 0f;

        float currentDistance = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
        return Mathf.Clamp01(1f - (currentDistance / initialDistanceToWaypoint));
    }

    public bool IsWaypointTimeoutActive() => waypointTimeoutDuration > 0f;

    // NEW: Public methods for timer status
    public bool IsWaypointTimerPaused() => waypointTimerPaused;
    public float GetPausedTimeAccumulated() => pausedTimeAccumulator;
    public bool IsCollidingWithBicycle() => collidingWithBicycle;

    // Public methods for intersection safety control
    public void ForceActivateIntersectionSafety() => ActivateIntersectionSafety();
    public void ForceDeactivateIntersectionSafety() => DeactivateIntersectionSafety();
    public bool IsUsingIntersectionDetection() => useIntersectionCollider;
    public float GetRemainingIntersectionSafetyTime()
    {
        if (!useIntersectionCollider) return 0f;
        return Mathf.Max(0f, intersectionSafetyDuration - (Time.time - intersectionSafetyStartTime));
    }

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
            // NEW: Check if colliding with bicycle
            if (collision.collider == bicycleCollider)
            {
                collidingWithBicycle = true;
                if (enableDebugLogs) Debug.Log($"{name}: Collision with bicycle - pausing waypoint timer");
            }
            else
            {
                if (enableDebugLogs) Debug.Log($"{name}: Collision with vehicle");
                collidingWithCar = true;
                collisionStartTime = Time.time;
            }

            isPaused = true;
            lastProgressTime = Time.time;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Vehicle"))
        {
            // NEW: Handle bicycle collision exit
            if (collision.collider == bicycleCollider)
            {
                collidingWithBicycle = false;
                if (enableDebugLogs) Debug.Log($"{name}: Bicycle collision ended - resuming waypoint timer");
            }
            else
            {
                if (enableDebugLogs) Debug.Log($"{name}: Vehicle collision ended");
                StartCoroutine(ResumeAfterDelay());
            }
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

        // Draw active detection collider
        Vector3 activeCenter = GetActiveDetectionBoxCenter();
        Vector3 activeHalfExtents = GetActiveDetectionBoxSize();
        Vector3 castEndCenter = activeCenter + transform.forward * boxcastLength;

        // Use different colors for different detection modes
        Gizmos.color = useIntersectionCollider ? Color.magenta : Color.red;

        // Draw detection box
        Gizmos.matrix = Matrix4x4.TRS(castEndCenter, GetActiveDetectionBoxOrientation(), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, activeHalfExtents * 2);

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = useIntersectionCollider ? Color.cyan : Color.yellow;
        Gizmos.DrawLine(activeCenter, castEndCenter);

        // Draw waypoint timeout information
        if (Application.isPlaying && waypoints != null && waypoints.Length > currentWaypointIndex)
        {
            // Draw line to current waypoint
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, waypoints[currentWaypointIndex].position);

            // Draw waypoint timeout progress
            Vector3 textPos = transform.position + Vector3.up * 2.5f;
            float timeRemaining = GetWaypointTimeRemaining();
            float progress = GetWaypointProgress();

#if UNITY_EDITOR
            string timerStatus = waypointTimerPaused ? " (PAUSED)" : "";
            string bicycleStatus = collidingWithBicycle ? " [BICYCLE]" : "";
            UnityEditor.Handles.Label(textPos, $"Waypoint {currentWaypointIndex}\nTime: {timeRemaining:F1}s{timerStatus}\nProgress: {progress:P0}{bicycleStatus}\nPaused: {pausedTimeAccumulator:F1}s");

            // Draw progress bar above car
            Vector3 barStart = transform.position + Vector3.up * 4f - Vector3.right * 1f;
            Vector3 barEnd = transform.position + Vector3.up * 4f + Vector3.right * 1f;

            // Background bar
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(barStart, barEnd);

            // Progress bar - different colors based on timer status
            if (waypointTimerPaused)
                Gizmos.color = Color.magenta; // Orange when paused
            else
                Gizmos.color = timeRemaining > 1f ? Color.green : Color.red;

            Vector3 progressEnd = Vector3.Lerp(barStart, barEnd, progress);
            Gizmos.DrawLine(barStart, progressEnd);
#endif
        }

        // Draw intersection safety status
        if (useIntersectionCollider && intersectionDetectionCollider != null)
        {
            Gizmos.color = Color.green;
            Vector3 textPos = transform.position + Vector3.up * 3f;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(textPos, $"Intersection Safety: {GetRemainingIntersectionSafetyTime():F1}s");
#endif
        }

        // NEW: Draw bicycle collision status
        if (collidingWithBicycle)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 0.5f);
        }
    }
}