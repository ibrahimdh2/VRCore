using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class BikeController : MonoBehaviour
{
    [Header("Handlebar Controllers")]
    public Transform leftController;
    public Transform rightController;
    public Transform frontWheelTransform; // The actual wheel mesh that spins
    public Transform frontHandleAssembly; // Parent object that steers left/right
    public Vector3 handleBarRotationOffset;
    [Header("Steering Clamp")]
    public float minSteerClamp = -30f;
    public float maxSteerClamp = 30f;


    public enum TurnAngle { X, Y, Z }
    public TurnAngle currentturnAngle;

    [Header("Speed & Display")]
    [SerializeField] private SpeedReceiver receiver;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Back Body Follow")]
    public float turnFollowSpeed = 2f;
    private float moveSpeed;

    [Header("Handlebar Settings")]
    public float maxTurnAngle = 45f; // Maximum turn angle in degrees
    public float turnSensitivity = 1f; // Sensitivity multiplier for turning
    public float rotationMultiplier = 10f; // How much the rotation is affected

    [Header("Wheel Colliders")]
    public WheelCollider frontWheelCollider;
    public WheelCollider backWheelCollider;

    [Header("Wheel Physics")]
    public float motorForce = 1500f;
    public float brakeForce = 3000f;
    public float maxSteerAngle = 30f;

    [Header("Physics Settings")]
    public bool useStabilityAssist = true;
    public float stabilityForce = 50f;
    public float gyroscopicForce = 10f;

    // For wheel visual updates
    public Transform backWheelTransform;

    // Wheel rotation tracking
    private float frontWheelRotation = 0f;
    private float backWheelRotation = 0f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Set rigidbody properties for realistic bike physics
        rb.mass = 180f; // Typical bike + rider weight
        rb.centerOfMass = new Vector3(0, -0.5f, 0); // Lower center of mass for stability
    }

    void Update()
    {
        // Get speed from receiver (assuming this gives you desired speed)
        moveSpeed = receiver.speedKph;
        speedText.text = $"{moveSpeed:F2} km/h";

        // Calculate handlebar turn angle
        float turnAngle = CalculateHandlebarAngle();

        // Apply steering to front wheel collider
        ApplySteering(turnAngle);

        // Update visual wheel rotations
        UpdateWheelVisuals();

        // Apply motor force based on desired speed
        ApplyMotorForce();
    }

    void FixedUpdate()
    {
        // All physics-based movement is handled by wheel colliders automatically
        // The wheel colliders will move the rigidbody based on:
        // - Motor torque applied to wheels
        // - Steering angle of front wheel
        // - Friction settings of wheel colliders
        // - Ground contact and physics interactions

        // Optional: Add some stability assistance for bikes
        AddBikeStability();
    }

    private void AddBikeStability()
    {
        if (!useStabilityAssist || rb == null) return;

        // Add some upright force to prevent the bike from falling over easily
        // This simulates a rider's balance adjustments
        Vector3 up = transform.up;
        Vector3 worldUp = Vector3.up;

        // Calculate how much the bike is tilted
        float tiltAngle = Vector3.Angle(up, worldUp);

        // Apply corrective torque if tilted too much
        if (tiltAngle > 5f && rb.linearVelocity.magnitude > 1f) // Only when moving
        {
            Vector3 correctionTorque = Vector3.Cross(up, worldUp) * tiltAngle * stabilityForce;
            rb.AddTorque(correctionTorque, ForceMode.Acceleration);
        }

        // Add gyroscopic effect - bikes are more stable when moving faster
        if (rb.linearVelocity.magnitude > 0.5f)
        {
            float gyroEffect = Mathf.Clamp(rb.linearVelocity.magnitude * gyroscopicForce, 0f, 100f);
            Vector3 gyroTorque = transform.up * frontWheelCollider.steerAngle * -gyroEffect * 0.1f;
            rb.AddTorque(gyroTorque, ForceMode.Acceleration);
        }

        // Prevent extreme rotation speeds that could cause flipping
        if (rb.angularVelocity.magnitude > 10f)
        {
            rb.angularVelocity = rb.angularVelocity.normalized * 10f;
        }
    }

    private void ApplySteering(float turnAngle)
    {
        if (frontWheelCollider != null)
        {
            // Clamp the steering angle using inspector-defined min/max
            float steerAngle = Mathf.Clamp(turnAngle, minSteerClamp, maxSteerClamp);
            frontWheelCollider.steerAngle = steerAngle;
        }

        if (frontHandleAssembly != null)
        {
            Vector3 steerRotation = Vector3.zero;
            switch (currentturnAngle)
            {
                case TurnAngle.X:
                    steerRotation.x = turnAngle;
                    break;
                case TurnAngle.Y:
                    steerRotation.y = turnAngle;
                    break;
                case TurnAngle.Z:
                    steerRotation.z = turnAngle;
                    break;
            }
            frontHandleAssembly.localRotation = Quaternion.Euler(steerRotation + handleBarRotationOffset);
        }
    }

    private void ApplyMotorForce()
    {
        if (backWheelCollider != null)
        {
            // Get current speed in km/h
            float currentSpeed = rb.linearVelocity.magnitude * 3.6f; // Convert m/s to km/h
            float speedDifference = moveSpeed - currentSpeed;

            // Apply motor torque to both wheels for better traction and movement
            if (moveSpeed > 0.1f) // Only apply motor force if we want to move
            {
                float torque = speedDifference * motorForce * 0.1f;
                torque = Mathf.Clamp(torque, -motorForce, motorForce);

                // Apply motor torque to back wheel (primary drive)
                backWheelCollider.motorTorque = torque;

                // Optional: slight motor assistance to front wheel for better hill climbing
                // Comment out if you want rear-wheel drive only
                frontWheelCollider.motorTorque = torque * 0.1f;
            }
            else
            {
                backWheelCollider.motorTorque = 0f;
                frontWheelCollider.motorTorque = 0f;
            }

            // Apply braking when target speed is much lower or when stopping
            if (moveSpeed < 0.1f || speedDifference < -2f)
            {
                float brakeAmount = moveSpeed < 0.1f ? brakeForce : brakeForce * 0.3f;
                backWheelCollider.brakeTorque = brakeAmount;
                frontWheelCollider.brakeTorque = brakeAmount * 0.7f;
            }
            else
            {
                backWheelCollider.brakeTorque = 0f;
                frontWheelCollider.brakeTorque = 0f;
            }
        }
    }

    private void UpdateWheelVisuals()
    {
        // Calculate wheel rotation based on movement
        float wheelCircumference = 2 * Mathf.PI * (frontWheelCollider != null ? frontWheelCollider.radius : 0.33f); // Default radius if null
        float distanceTraveled = rb.linearVelocity.magnitude * Time.deltaTime;
        float wheelRotationDelta = (distanceTraveled / wheelCircumference) * 360f; // Convert to degrees

        // Update front wheel - ONLY spinning rotation, steering is handled by parent
        if (frontWheelTransform != null)
        {
            frontWheelRotation += wheelRotationDelta;

            // Apply only the spinning rotation to the wheel mesh
            // The steering rotation is handled by frontHandleAssembly
            frontWheelTransform.localRotation = Quaternion.Euler(frontWheelRotation, 0, 0);
        }

        // Update back wheel - spinning rotation only
        if (backWheelTransform != null)
        {
            backWheelRotation += wheelRotationDelta;
            backWheelTransform.localRotation = Quaternion.Euler(backWheelRotation, 0, 0);
        }

        // Update wheel positions based on wheel collider positions
        UpdateWheelPositions();
    }

    private void UpdateWheelPositions()
    {
        // Update front wheel assembly position
        if (frontWheelCollider != null && frontHandleAssembly != null)
        {
            Vector3 pos;
            Quaternion rot;
            frontWheelCollider.GetWorldPose(out pos, out rot);

            // Position the entire front assembly based on wheel collider
        }

        // Update back wheel position
        if (backWheelCollider != null && backWheelTransform != null)
        {
            Vector3 pos;
            Quaternion rot;
            backWheelCollider.GetWorldPose(out pos, out rot);
            backWheelTransform.position = pos;
        }
    }

    private float CalculateHandlebarAngle()
    {
        if (leftController == null || rightController == null)
            return 0f;

        // Convert controller positions to bike's local space
        Vector3 leftLocalPos = transform.InverseTransformPoint(leftController.position);
        Vector3 rightLocalPos = transform.InverseTransformPoint(rightController.position);

        // Calculate the forward/backward difference (Z-axis in local space)
        float forwardBackwardDiff = leftLocalPos.z - rightLocalPos.z;

        // Convert the difference to an angle
        float turnAngle = forwardBackwardDiff * turnSensitivity * rotationMultiplier;

        // Clamp the turn angle to prevent extreme turns
        turnAngle = Mathf.Clamp(turnAngle, -maxTurnAngle, maxTurnAngle);

        return turnAngle;
    }
}