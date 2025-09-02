using System.Collections;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float maxSpeed = 10f;         // Max forward speed
    public float acceleration = 5f;      // Acceleration rate
    public float brakingForce = 8f;      // Deceleration rate
    public float rotationSpeed = 120f;   // Base turn speed (deg/sec)
    public float sharpTurnAngle = 45f;   // Angle threshold to slow down on sharp turns
    public float waypointTolerance = 1f; // Distance to switch waypoint

    [Header("Wheel Transforms")]
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    [Header("Detection Settings")]
    public BoxCollider detectionCollider; // Reference to the disabled child collider for detection
    public float raycastLength = 5f;
    public float boxcastLength = 1f;

    [Header("Turn Detection Settings")]
    public float turnDetectionAngleThreshold = 10f; // Minimum angle to trigger turn detection
    public float turnBoxcastLength = 3f; // Length of the turn detection boxcast
    public bool enableTurnDetection = true; // Toggle for turn detection feature
    public Transform leftTurnRayCastStartingPoint;
    public Transform rightTurnRayCastStartingPoint;
    public float rayDistance;

    [Header("Stop/Signal Settings")]
    public SignalStoppingVehicle signalStoppingVehicle;
    public float delayTimeAfterStopping = 4f;
    public float yPos;

    [Header("Deadlock Prevention")]
    public float maxWaitTime = 3f; // Maximum time to wait before forcing movement
    public float yieldDistance = 2f; // Distance to move back when yielding

    [Header("Speed Control")]
    public float speedChangeRate = 2f; // How fast speed changes when modified externally

    [Header("Debug Settings")]
    public bool enableDebugLogs = false; // Enable to see signal state logs

    [SerializeField] private Transform[] waypoints;
    [SerializeField] private int currentWaypointIndex = 0;

    private Quaternion frontLeftOriginalRotation;
    private Quaternion frontRightOriginalRotation;

    [SerializeField] private float currentSpeed = 0f;
    private bool isPaused = false;
    private bool collidingWithCar = false;
    private WaitForSeconds waitFor;

    // Deadlock prevention variables
    private float collisionStartTime;
    private int vehiclePriority;
    private bool isYielding = false;
    private Vector3 originalPosition;

    // Speed control variables
    private float targetMaxSpeed; // The speed we're transitioning to
    private float currentMaxSpeed; // The current max speed being used

    // Progress tracking for deadlock detection
    private Vector3 lastPosition;
    private float lastProgressTime;

    public BoxCollider currentCollider;
    private Coroutine moveRoutineCoroutine;



    private BoxCollider bicycleCollider;
    [SerializeField] private BoxCollider frontLeftChecBoxCollider;
    [SerializeField] private BoxCollider frontRightCheckBoxCollider;
    void Awake()
    {
        bicycleCollider = DataManager.Instance.bikeCollider;
        currentCollider = GetComponent<BoxCollider>();
        if (frontLeftWheel != null) frontLeftOriginalRotation = frontLeftWheel.localRotation;
        if (frontRightWheel != null) frontRightOriginalRotation = frontRightWheel.localRotation;

        signalStoppingVehicle = GetComponent<SignalStoppingVehicle>();
        transform.position = new Vector3(transform.position.x, yPos, transform.position.z);
        waitFor = new WaitForSeconds(delayTimeAfterStopping);

        // Assign priority based on instance ID for consistent deterministic behavior
        vehiclePriority = GetInstanceID();

        // Initialize speed control
        targetMaxSpeed = maxSpeed;
        currentMaxSpeed = maxSpeed;

        // Setup detection collider
        SetupDetectionCollider();
    }

    void Start()
    {
        // nothing here; coroutine starts when waypoints assigned
    }

    private void SetupDetectionCollider()
    {
        if (detectionCollider != null)
        {
            // Ensure the detection collider is disabled
            detectionCollider.enabled = false;

            if (enableDebugLogs)
            {
                Debug.Log($"{gameObject.name}: Detection collider setup - Size: {detectionCollider.size}, Center: {detectionCollider.center}");
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No detection collider assigned! Please assign a disabled BoxCollider child object.");
        }
    }

    private Vector3 GetDetectionBoxCenter()
    {
        if (detectionCollider == null)
        {
            // Fallback to original behavior if no collider is assigned
            return transform.position + Vector3.up * 0.5f;
        }

        // Get the world position of the collider's center
        Vector3 localCenter = detectionCollider.center;
        Vector3 worldCenter = detectionCollider.transform.TransformPoint(localCenter);
        return worldCenter;
    }
    private Vector3 GetDetectionBoxCenter(BoxCollider c)
    {
        if(detectionCollider == null)
        {
            return transform.position + Vector3.up * 0.5f;
        }

        Vector3 localCenter = c.center;
        Vector3 worldCenter = c.transform.TransformPoint(localCenter);
        return worldCenter;
    }

    private Vector3 GetDetectionBoxSize()
    {
        if (detectionCollider == null)
        {
            // Fallback to original behavior if no collider is assigned
            return new Vector3(0.5f, 0.5f, 0.5f);
        }

        // Get the world-space size of the collider
        Vector3 localSize = detectionCollider.size;
        Vector3 lossyScale = detectionCollider.transform.lossyScale;
        Vector3 worldSize = Vector3.Scale(localSize, lossyScale) * 0.5f; // BoxCast uses half-extents
        return worldSize;
    }
    private Vector3 GetDetectionBoxSize(BoxCollider c)
    {
        if (detectionCollider == null)
        {
            // Fallback to original behavior if no collider is assigned
            return new Vector3(0.5f, 0.5f, 0.5f);
        }

        // Get the world-space size of the collider
        Vector3 localSize = c.size;
        Vector3 lossyScale = c.transform.lossyScale;
        Vector3 worldSize = Vector3.Scale(localSize, lossyScale) * 0.5f; // BoxCast uses half-extents
        return worldSize;
    }

    private Quaternion GetDetectionBoxOrientation()
    {
        if (detectionCollider == null)
        {
            return transform.rotation;
        }

        return detectionCollider.transform.rotation;
    }
    private Quaternion GetDetectionBoxOrientation(BoxCollider c)
    {
        if (detectionCollider == null)
        {
            return transform.rotation;
        }

        return c.transform.rotation;
    }

    private void FixedUpdate()
    {
        // Gradually update currentMaxSpeed toward target (keeps physics-consistent)
        UpdateMaxSpeed();
    }

    private IEnumerator MoveRoutine()
    {
        if (waypoints == null || waypoints.Length == 0) yield break;

        lastPosition = transform.position;
        lastProgressTime = Time.time;

        while (true)
        {
            if (waypoints == null || waypoints.Length == 0) break;

            Transform target = waypoints[currentWaypointIndex];
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0;
            float distance = toTarget.magnitude;

            // If reached waypoint -> switch
            if (distance < waypointTolerance)
            {
                if (currentWaypointIndex + 1 > waypoints.Length - 1)
                {
                    VehiclePoolManager.Instance.ReturnCar(this.gameObject);
                    //Debug.Log("Should return to the pool");
                }
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                // reset progress tracking so we don't false-detect deadlock immediately
                lastPosition = transform.position;
                lastProgressTime = Time.time;
                yield return null;
                continue;
            }

            // If currently yielding, handle yielding routine (backwards movement + wait)
            if (isYielding)
            {
                HandleYieldingStep();
                // small check to avoid being stuck in yield forever (safety timeout)
                if (Time.time - lastProgressTime > maxWaitTime + 2f)
                {
                    // End yielding forcibly
                    EndYieldingImmediate();
                }
                yield return null;
                continue;
            }

            // --- Calculate direction and angle for both movement and detection ---
            Vector3 currentDir = transform.forward;
            float angleToTarget = Vector3.Angle(currentDir, toTarget.normalized);

            // --- Obstacle & pausing checks (similar to HandlePausing) ---
            bool shouldPause = false;
            // Check immediate collisions that were detected via OnCollisionEnter
            if (collidingWithCar)
            {
                shouldPause = true;
            }
            else
            {
                // Modified forward boxcast using detection collider parameters
                Vector3 boxCenter = GetDetectionBoxCenter();
                Vector3 halfExtents = GetDetectionBoxSize();
                Quaternion orientation = GetDetectionBoxOrientation();

                if (Physics.BoxCast(boxCenter, halfExtents, transform.forward, out RaycastHit hit, orientation, boxcastLength))
                {
                    if (hit.collider.CompareTag("Vehicle"))
                    {
                        shouldPause = true;
                    }
                }

                // NEW: Turn detection boxcast
                if (!shouldPause && enableTurnDetection)
                {
                    // Only check turn direction if we're making a significant turn
                    if (angleToTarget > turnDetectionAngleThreshold)
                    {
                        Transform currentWayPoint = waypoints[currentWaypointIndex];
                        Vector3 turnDirection = (currentWayPoint.position - transform.position).normalized;

                        // Cast from both left and right origins
                        Transform[] rayOrigins = new Transform[]
                        {
                            leftTurnRayCastStartingPoint,
                            rightTurnRayCastStartingPoint
                        };

                        foreach (var rayOrigin in rayOrigins)
                        {
                            if (Physics.Raycast(rayOrigin.position, turnDirection, out RaycastHit turnHit, rayDistance))
                            {
                                // Debug ray visualization
                                Debug.DrawRay(rayOrigin.position,
                                    turnDirection * rayDistance,
                                    turnHit.collider.CompareTag("Vehicle") ? Color.red : Color.yellow,
                                    0.1f);

                                if (turnHit.collider.CompareTag("Vehicle") && turnHit.collider != currentCollider)
                                {
                                    shouldPause = true;
                                    if (enableDebugLogs)
                                        Debug.Log($"{gameObject.name}: Turn detection - Vehicle detected in turn direction from {rayOrigin.name}");
                                    break; // Stop checking once we found a blocking vehicle
                                }
                            }
                            else
                            {
                                // Draw ray even when nothing is hit (for debugging)
                                Debug.DrawRay(rayOrigin.position,
                                    turnDirection * rayDistance,
                                    Color.green,
                                    0.1f);
                            }
                        }
                    }
                }

            }

            // Traffic signal check
            if (!shouldPause && signalStoppingVehicle != null && signalStoppingVehicle.signal != null)
            {
                if (signalStoppingVehicle.signal.State != LightState.Green)
                    shouldPause = true;
            }

            isPaused = shouldPause;

            if (isPaused)
            {
                // Deadlock detection: if no meaningful movement for maxWaitTime -> resolve
                if (Vector3.Distance(transform.position, lastPosition) > 0.1f)
                {
                    // we made progress
                    lastPosition = transform.position;
                    lastProgressTime = Time.time;
                }
                else
                {
                    if (Time.time - lastProgressTime > maxWaitTime)
                    {
                        if (enableDebugLogs) Debug.Log($"{gameObject.name}: Deadlock suspected, resolving...");
                        ResolveDeadlock();
                        // update progress time to avoid immediate repeat
                        lastProgressTime = Time.time;
                    }
                }

                yield return null;
                continue;
            }

            // --- Movement and steering when not paused ---
            // Use the already calculated direction & angle

            // Adaptive speed target (sharp turn => slow down)
            float targetSpeed;
            if (angleToTarget > sharpTurnAngle)
                targetSpeed = currentMaxSpeed * 0.5f;
            else
                targetSpeed = currentMaxSpeed;

            // Smooth speed change
            if (currentSpeed < targetSpeed)
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            else
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, brakingForce * Time.deltaTime);

            // Adaptive turning
            float turnMultiplier = Mathf.Lerp(0.3f, 1f, angleToTarget / 90f);
            Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * turnMultiplier * Time.deltaTime);

            // Move forward
            transform.position += transform.forward * currentSpeed * Time.deltaTime;

            // Wheel animation
            float wheelRotationAmount = currentSpeed * 360f * Time.deltaTime;
            RotateWheels(wheelRotationAmount, angleToTarget);

            // Update progress time because we moved
            if (Vector3.Distance(transform.position, lastPosition) > 0.05f)
            {
                lastPosition = transform.position;
                lastProgressTime = Time.time;
            }

            yield return null;
        }

        // When loop ends (no waypoints), return car to pool if you want that behavior:
        // VehiclePoolManager.Instance.ReturnCar(gameObject);
    }

    private void HandleYieldingStep()
    {
        // Move backward slowly
        Vector3 backwardDirection = -transform.forward;
        float yieldSpeed = Mathf.Max(1f, currentMaxSpeed * 0.3f);
        transform.position += backwardDirection * yieldSpeed * Time.deltaTime;

        // Rotate wheels for backing up
        float wheelRotationAmount = -yieldSpeed * 360f * Time.deltaTime;
        RotateWheels(wheelRotationAmount, 0f);

        // Check if we've moved far enough back
        if (Vector3.Distance(transform.position, originalPosition) > yieldDistance)
        {
            // schedule end yielding
            StartCoroutine(EndYielding());
        }
    }

    private IEnumerator EndYielding()
    {
        // small wait to give other vehicle time to pass
        yield return new WaitForSeconds(0.8f);
        isYielding = false;
        currentSpeed = 0f; // reset for smooth restart
        // update last progress to avoid false deadlock detection
        lastPosition = transform.position;
        lastProgressTime = Time.time;
    }

    public bool IsRight(Vector3 origin, Vector3 forward, Vector3 target)
    {
        Vector3 toTarget = target - origin;

        // Cross product sign determines left vs right
        float crossY = Vector3.Cross(forward, toTarget).y;

        return crossY < 0f; // negative => right, positive => left
    }

    private void EndYieldingImmediate()
    {
        isYielding = false;
        currentSpeed = 0f;
        lastPosition = transform.position;
        lastProgressTime = Time.time;
    }

    private void ResolveDeadlock()
    {
        // Find the other vehicle we're colliding/close to
        Collider[] nearby = Physics.OverlapSphere(transform.position, 3f);
        CarMovementController otherCar = null;

        foreach (Collider col in nearby)
        {
            if (col.CompareTag("Vehicle") && col.gameObject != gameObject)
            {
                var comp = col.GetComponent<CarMovementController>();
                if (comp != null && comp.collidingWithCar)
                {
                    otherCar = comp;
                    break;
                }
            }
        }

        if (otherCar != null)
        {
            if (vehiclePriority < otherCar.vehiclePriority)
            {
                if (enableDebugLogs) Debug.Log($"{gameObject.name}: Lower priority — will yield.");
                StartYielding();
            }
            else if (vehiclePriority > otherCar.vehiclePriority)
            {
                if (enableDebugLogs) Debug.Log($"{gameObject.name}: Higher priority — forcing resume.");
                ForceResume();
            }
            else
            {
                // Tiebreaker by X coordinate
                if (transform.position.x < otherCar.transform.position.x)
                    StartYielding();
                else
                    ForceResume();
            }
        }
        else
        {
            // No other car found — try to nudge forward by forcing resume
            ForceResume();
        }
    }

    private void StartYielding()
    {
        isYielding = true;
        originalPosition = transform.position;
        collidingWithCar = false;
        isPaused = false;
        currentSpeed = 0f; // Stop immediately when starting to yield
        // update progress
        lastProgressTime = Time.time;
    }

    private void ForceResume()
    {
        collidingWithCar = false;
        isPaused = false;
        // give a tiny nudge forward to break potential stuck state
        transform.position += transform.forward * 0.05f;
        lastPosition = transform.position;
        lastProgressTime = Time.time;
    }

    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
        currentWaypointIndex = 1;

        if (waypoints != null && waypoints.Length > 1)
        {
            Transform currentWayPoint = waypoints[1];
            Vector3 wayPointDir = (currentWayPoint.position - transform.position).normalized;
            wayPointDir.y = 0;
            transform.rotation = Quaternion.LookRotation(wayPointDir);
        }

        currentSpeed = 0f; // reset speed when setting new path

        // restart movement coroutine
        if (moveRoutineCoroutine != null) StopCoroutine(moveRoutineCoroutine);
        moveRoutineCoroutine = StartCoroutine(MoveRoutine());
    }

    public void Pause() => isPaused = true;
    public void Resume() => isPaused = false;

    // Public methods for external speed control
    public void SetMaxSpeed(float newMaxSpeed)
    {
        targetMaxSpeed = Mathf.Max(0f, newMaxSpeed); // Ensure non-negative
    }

    public void IncreaseMaxSpeed(float amount)
    {
        targetMaxSpeed += amount;
        targetMaxSpeed = Mathf.Max(0f, targetMaxSpeed);
    }

    public void DecreaseMaxSpeed(float amount)
    {
        targetMaxSpeed -= amount;
        targetMaxSpeed = Mathf.Max(0f, targetMaxSpeed);
    }

    public float GetCurrentMaxSpeed()
    {
        return currentMaxSpeed;
    }

    public float GetTargetMaxSpeed()
    {
        return targetMaxSpeed;
    }

    public bool IsSpeedChanging()
    {
        return Mathf.Abs(currentMaxSpeed - targetMaxSpeed) > 0.01f;
    }

    private void UpdateMaxSpeed()
    {
        if (currentMaxSpeed != targetMaxSpeed)
        {
            currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, targetMaxSpeed, speedChangeRate * Time.deltaTime);
        }
    }

    private void RotateWheels(float rotationAmount, float steerAngle)
    {
        // Rear wheels just rotate
        rearLeftWheel?.Rotate(Vector3.right, -rotationAmount);
        rearRightWheel?.Rotate(Vector3.right, -rotationAmount);

        // Front wheels rotate and steer
        if (frontLeftWheel != null)
        {
            frontLeftWheel.localRotation = frontLeftOriginalRotation * Quaternion.Euler(0, steerAngle * 0.5f, 0);
            frontLeftWheel.Rotate(Vector3.right, -rotationAmount);
        }

        if (frontRightWheel != null)
        {
            frontRightWheel.localRotation = frontRightOriginalRotation * Quaternion.Euler(0, steerAngle * 0.5f, 0);
            frontRightWheel.Rotate(Vector3.right, -rotationAmount);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Vehicle"))
        {
            if (enableDebugLogs)
                Debug.Log($"{gameObject.name}: Car Collided with another car");

            collidingWithCar = true;
            collisionStartTime = Time.time;
            isPaused = true;

            // start/reschedule last progress time so deadlock timer starts from now
            lastProgressTime = Time.time;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Vehicle"))
        {
            if (enableDebugLogs)
                Debug.Log($"{gameObject.name}: Car Not Collided with another car");

            StartCoroutine(ResumeAfterSomeTime());
        }
    }

    private IEnumerator ResumeAfterSomeTime()
    {
        yield return waitFor;
        collidingWithCar = false;
        isPaused = false;
        // update progress markers
        lastPosition = transform.position;
        lastProgressTime = Time.time;
    }

    private void OnDrawGizmosSelected()
    {
        if (detectionCollider == null) return;

        Gizmos.color = Color.red;

        // Get detection box parameters from the assigned collider
        Vector3 boxCenter = GetDetectionBoxCenter();
        Vector3 halfExtents = GetDetectionBoxSize();
        Quaternion orientation = GetDetectionBoxOrientation();

        // Draw the detection box extending forward by raycastLength
        Vector3 castDirection = transform.forward;
        Vector3 castEndCenter = boxCenter + castDirection * boxcastLength;

        // Draw the box at the end of the cast
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(castEndCenter, orientation, Vector3.one);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2);

        // Draw a line showing the cast direction
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(boxCenter, castEndCenter);

        // Draw the starting box position
        Gizmos.color = Color.green;
        rotationMatrix = Matrix4x4.TRS(boxCenter, orientation, Vector3.one);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2);

        // Reset matrix
        Gizmos.matrix = Matrix4x4.identity;
    }
}