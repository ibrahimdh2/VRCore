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
    public float raycastLength = 5f;
    public Vector3 halfExtents = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 boxOffset = Vector3.zero;

    [Header("Stop/Signal Settings")]
    public SignalStoppingVehicle signalStoppingVehicle;
    public float delayTimeAfterStopping = 4f;
    public float yPos;

    [Header("Deadlock Prevention")]
    public float maxWaitTime = 8f; // Maximum time to wait before forcing movement
    public float yieldDistance = 2f; // Distance to move back when yielding

    [Header("Speed Control")]
    public float speedChangeRate = 2f; // How fast speed changes when modified externally

    [Header("Debug Settings")]
    public bool enableDebugLogs = false; // Enable to see signal state logs

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;

    private Quaternion frontLeftOriginalRotation;
    private Quaternion frontRightOriginalRotation;

    private float currentSpeed = 0f;
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

    void Awake()
    {
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
    }

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        // Handle yielding behavior
        if (isYielding)
        {
            HandleYielding();
            return;
        }

        if (isPaused)
        {
            // Check for deadlock timeout
            if (collidingWithCar && Time.time - collisionStartTime > maxWaitTime)
            {
                ResolveDeadlock();
            }
            return;
        }

        // Update current max speed gradually towards target
        UpdateMaxSpeed();

        Transform target = waypoints[currentWaypointIndex];
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0; // keep flat
        float distance = toTarget.magnitude;

        // --- Check for obstacles, signals, collisions ---
        HandlePausing();

        if (isPaused) return;

        // Check next waypoint direction to adapt turning & speed
        Vector3 currentDir = transform.forward;
        float angleToTarget = Vector3.Angle(currentDir, toTarget.normalized);

        // --- Adaptive Speed ---
        float targetSpeed;
        if (angleToTarget > sharpTurnAngle)
        {
            // Sharp turn ahead → slow down
            targetSpeed = currentMaxSpeed * 0.5f;
        }
        else
        {
            // Gentle turn or straight → accelerate
            targetSpeed = currentMaxSpeed;
        }

        // Smoothly adjust current speed towards target
        if (currentSpeed < targetSpeed)
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        else
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, brakingForce * Time.deltaTime);

        // --- Adaptive Turning ---
        float turnMultiplier = Mathf.Lerp(0.3f, 1f, angleToTarget / 90f); // gentle = 0.3, sharp = 1
        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * turnMultiplier * Time.deltaTime);

        // --- Move Forward ---
        transform.position += transform.forward * currentSpeed * Time.deltaTime;

        // --- Wheel Animation ---
        float wheelRotationAmount = currentSpeed * 360f * Time.deltaTime;
        RotateWheels(wheelRotationAmount, angleToTarget);

        // --- Waypoint Switching ---
        if (distance < waypointTolerance)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }

    private void HandlePausing()
    {
        bool shouldPause = false;

        // Check for collision with other vehicles
        if (collidingWithCar)
        {
            shouldPause = true;
        }
        else
        {
            // Check for obstacles ahead using boxcast
            Vector3 boxCenter = transform.position + boxOffset + Vector3.up * 0.5f;
            Quaternion orientation = transform.rotation;

            if (Physics.BoxCast(boxCenter, halfExtents, transform.forward, out RaycastHit hit, orientation, raycastLength))
            {
                if (hit.collider.CompareTag("Vehicle"))
                {
                    shouldPause = true;
                }
            }
        }

        // Check traffic signal - only pause if signal exists and is NOT green
        if (!shouldPause && signalStoppingVehicle != null && signalStoppingVehicle.signal != null)
        {
            // Only pause if signal is red or yellow, resume if green
            if (signalStoppingVehicle.signal.State != LightState.Green)
            {
                shouldPause = true;
                if (enableDebugLogs)
                {
                    Debug.Log($"{gameObject.name}: Pausing for signal state: {signalStoppingVehicle.signal.State}");
                }
            }
            else if (enableDebugLogs && isPaused)
            {
                Debug.Log($"{gameObject.name}: Resuming - signal is Green");
            }
        }

        isPaused = shouldPause;
    }

    private void UpdateMaxSpeed()
    {
        if (currentMaxSpeed != targetMaxSpeed)
        {
            currentMaxSpeed = Mathf.MoveTowards(currentMaxSpeed, targetMaxSpeed, speedChangeRate * Time.deltaTime);
        }
    }

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

    private void ResolveDeadlock()
    {
        // Find the other vehicle we're colliding with
        Collider[] nearbyVehicles = Physics.OverlapSphere(transform.position, 3f);
        CarMovementController otherCar = null;

        foreach (Collider col in nearbyVehicles)
        {
            if (col.CompareTag("Vehicle") && col.gameObject != gameObject)
            {
                otherCar = col.GetComponent<CarMovementController>();
                if (otherCar != null && otherCar.collidingWithCar)
                    break;
            }
        }

        if (otherCar != null)
        {
            // Vehicle with lower priority yields (moves back)
            if (vehiclePriority < otherCar.vehiclePriority)
            {
                StartYielding();
            }
            else if (vehiclePriority > otherCar.vehiclePriority)
            {
                // This vehicle has higher priority, force resume
                ForceResume();
            }
            else
            {
                // Same priority (shouldn't happen with instance IDs, but fallback)
                // Use position as tiebreaker
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
            // No other car found, just resume
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
    }

    private void HandleYielding()
    {
        // Move backward slowly
        Vector3 backwardDirection = -transform.forward;
        float yieldSpeed = currentMaxSpeed * 0.3f;
        transform.position += backwardDirection * yieldSpeed * Time.deltaTime;

        // Rotate wheels for backing up
        float wheelRotationAmount = -yieldSpeed * 360f * Time.deltaTime;
        RotateWheels(wheelRotationAmount, 0f);

        // Check if we've moved far enough back
        if (Vector3.Distance(transform.position, originalPosition) > yieldDistance)
        {
            // Wait a moment, then resume normal movement
            StartCoroutine(EndYielding());
        }
    }

    private IEnumerator EndYielding()
    {
        yield return new WaitForSeconds(1f);
        isYielding = false;
        currentSpeed = 0f; // Reset speed for smooth restart
    }

    private void ForceResume()
    {
        collidingWithCar = false;
        isPaused = false;
    }

    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
        currentWaypointIndex = 1;

        if (waypoints != null && waypoints.Length > 1)
        {
            Transform currentWayPoint = waypoints[currentWaypointIndex];
            Vector3 wayPointDir = (currentWayPoint.position - transform.position).normalized;
            wayPointDir.y = 0;
            transform.rotation = Quaternion.LookRotation(wayPointDir);
        }

        currentSpeed = 0f; // reset speed when setting new path
    }

    public void Pause() => isPaused = true;
    public void Resume() => isPaused = false;

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
            Pause();
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
        Resume();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 boxCenter = transform.position + boxOffset + Vector3.up * 0.5f;
        Quaternion orientation = transform.rotation;
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(boxCenter + transform.forward * raycastLength * 0.5f, orientation, Vector3.one);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2 + new Vector3(0, 0, raycastLength));
    }
}