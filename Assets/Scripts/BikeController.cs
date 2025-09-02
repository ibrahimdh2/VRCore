using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using static BikeController;

public class BikeController : MonoBehaviour
{
    [Header("Handlebar Controllers")]
    public Transform leftController;
    public Transform rightController;
    public Transform frontWheelTransform;
    public Transform frontHandleAssembly;
    public Vector3 handleBarRotationOffset;

    [Header("Steering Clamp")]
    public float minSteerClamp = -25f; // Reduced from -30
    public float maxSteerClamp = 25f;  // Reduced from 30

    public enum TurnAngle { X, Y, Z }
    public TurnAngle currentturnAngle;

    [Header("Speed & Display")]
    [SerializeField] private SpeedReceiver receiver;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Back Body Follow")]
    public float turnFollowSpeed = 2f;
    private float moveSpeed;

    [Header("Handlebar Settings")]
    public float maxTurnAngle = 15f; // Much smaller for high speeds
    public float turnSensitivity = 0.3f; // Very low sensitivity for precision
    public float rotationMultiplier = 3f; // Much reduced for high-speed control

    [Header("Wheel Colliders")]
    public WheelCollider frontWheelCollider;
    public WheelCollider backWheelCollider;

    [Header("Wheel Physics")]
    public float motorForce = 4000f; // Increased for high speeds
    public float brakeForce = 8000f; // Much stronger brakes needed
    public float maxSteerAngle = 12f; // Very small for stability at high speeds

    [Header("Physics Settings")]
    public bool useStabilityAssist = true;
    public float stabilityForce = 200f; // Much stronger for high speeds
    public float gyroscopicForce = 100f; // Strong gyroscopic effect needed
    public float uprightTorque = 300f; // Stronger upright force
    public float minSpeedForStability = 3f; // Increased to 3 km/h to avoid low-speed interference
    public float highSpeedStabilityBoost = 2f; // Additional stability at high speeds
    public float lowSpeedSteerBoost = 3f; // Extra steering power at low speeds

    // For wheel visual updates
    public Transform backWheelTransform;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Improved rigidbody properties for high-speed motorcycle physics
        rb.mass = 250f; // Heavier for high-speed stability
        rb.centerOfMass = new Vector3(0, -0.4f, 0.2f); // Lower and more forward
        rb.linearDamping = 0.02f; // Lower air resistance for high speeds
        rb.angularDamping = 8f; // Higher angular drag for stability

        // Configure wheel colliders with better friction
        ConfigureWheelFriction();
    }

    void ConfigureWheelFriction()
    {
        // High-speed optimized friction settings
        WheelFrictionCurve forwardFriction = new WheelFrictionCurve();
        forwardFriction.extremumSlip = 0.2f; // Lower slip for high-speed grip
        forwardFriction.extremumValue = 1.5f; // Higher grip
        forwardFriction.asymptoteSlip = 0.6f;
        forwardFriction.asymptoteValue = 1.0f;
        forwardFriction.stiffness = 3f; // Higher stiffness for stability

        WheelFrictionCurve sidewaysFriction = new WheelFrictionCurve();
        sidewaysFriction.extremumSlip = 0.15f; // Very low slip for cornering grip
        sidewaysFriction.extremumValue = 2.0f; // High lateral grip
        sidewaysFriction.asymptoteSlip = 0.4f;
        sidewaysFriction.asymptoteValue = 1.2f;
        sidewaysFriction.stiffness = 4f; // Very high for high-speed cornering

        if (frontWheelCollider != null)
        {
            frontWheelCollider.forwardFriction = forwardFriction;
            frontWheelCollider.sidewaysFriction = sidewaysFriction;
        }

        if (backWheelCollider != null)
        {
            backWheelCollider.forwardFriction = forwardFriction;
            backWheelCollider.sidewaysFriction = sidewaysFriction;
        }
    }

    private float turnAngle;
    void Update()
    {
        moveSpeed = receiver.speedKph;
        speedText.text = $"{moveSpeed:F2} km/h";

        turnAngle = CalculateHandlebarAngle();
        
    }

    void FixedUpdate()
    {
        ApplySteering(turnAngle);
        UpdateWheelVisuals();
        ApplyMotorForce();
        AddBikeStability();
        AddUprightForce();
        LimitExtremeMovements();
    }

    private void AddBikeStability()
    {
        if (!useStabilityAssist || rb == null) return;

        float currentSpeed = rb.linearVelocity.magnitude;
        float speedKmh = currentSpeed * 3.6f;

        Vector3 up = transform.up;
        Vector3 worldUp = Vector3.up;
        float tiltAngle = Vector3.Angle(up, worldUp);

        // Disable most stability assists at very low speeds to allow natural movement
        if (speedKmh < minSpeedForStability)
        {
            // Only apply minimal upright force at very low speeds
            if (speedKmh > 0.5f) // Only above 0.5 km/h
            {
                if (tiltAngle > 45f) // Only when severely tilted
                {
                    Vector3 correctionTorque = Vector3.Cross(up, worldUp) * tiltAngle * stabilityForce * 0.1f;
                    rb.AddTorque(correctionTorque, ForceMode.Acceleration);
                }
            }
            return;
        }

        // Speed-based stability multiplier - more stable at higher speeds
        float speedStabilityMultiplier = 1f;

        if (speedKmh > 50f) // Above 50 km/h, increase stability significantly
        {
            speedStabilityMultiplier = 1f + (speedKmh / 100f) * highSpeedStabilityBoost;
        }
        else if (speedKmh < 15f) // Reduce stability at low-medium speeds
        {
            speedStabilityMultiplier = Mathf.Lerp(0.3f, 1f, (speedKmh - minSpeedForStability) / 12f);
        }

        // Apply corrective torque when tilted
        if (tiltAngle > 2f)
        {
            Vector3 correctionTorque = Vector3.Cross(up, worldUp) * tiltAngle * stabilityForce * speedStabilityMultiplier;
            rb.AddTorque(correctionTorque, ForceMode.Acceleration);
        }

        // Enhanced gyroscopic effect - but only at reasonable speeds
        if (speedKmh > 8f) // Only apply gyroscopic effects above 8 km/h
        {
            float speedFactor = Mathf.Clamp01(currentSpeed / 30f); // Normalize to 30 m/s (~108 km/h)
            float gyroEffect = speedFactor * gyroscopicForce * speedStabilityMultiplier;
            Vector3 gyroTorque = transform.up * frontWheelCollider.steerAngle * -gyroEffect * 0.01f;
            rb.AddTorque(gyroTorque, ForceMode.Acceleration);

            // Counter-steering effect at high speeds
            if (speedKmh > 80f && Mathf.Abs(frontWheelCollider.steerAngle) > 2f)
            {
                Vector3 counterSteerTorque = -transform.up * frontWheelCollider.steerAngle * gyroEffect * 0.005f;
                rb.AddTorque(counterSteerTorque, ForceMode.Acceleration);
            }
        }
    }

    private void AddUprightForce()
    {
        if (rb == null) return;

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        // Greatly reduce upright force at very low speeds
        if (speedKmh < 2f) return; // No upright force below 2 km/h

        Vector3 up = transform.up;
        Vector3 worldUp = Vector3.up;

        float uprightDot = Vector3.Dot(up, worldUp);
        if (uprightDot < 0.8f) // If bike is tilted more than ~36 degrees
        {
            // Scale upright force with speed - less force at low speeds
            float speedScale = speedKmh < 10f ? Mathf.Lerp(0.1f, 1f, (speedKmh - 2f) / 8f) : 1f;

            Vector3 uprightDirection = Vector3.Cross(Vector3.Cross(worldUp, up), worldUp).normalized;
            rb.AddTorque(uprightDirection * uprightTorque * speedScale, ForceMode.Acceleration);
        }
    }

    private void LimitExtremeMovements()
    {
        if (rb == null) return;

        // Prevent excessive angular velocity (especially important at high speeds)
        if (rb.angularVelocity.magnitude > 5f) // Reduced for high-speed stability
        {
            rb.angularVelocity = rb.angularVelocity.normalized * 5f;
        }

        // Allow higher linear velocity for high-speed capability
        float maxLinearVelocity = 280f / 3.6f; // Convert 280 km/h to m/s (~77.8 m/s)
        if (rb.linearVelocity.magnitude > maxLinearVelocity)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxLinearVelocity;
        }
    }

    private void ApplySteering(float turnAngle)
    {
        if (frontWheelCollider != null)
        {
            // Speed-sensitive steering with special handling for low speeds
            float currentSpeed = rb.linearVelocity.magnitude * 3.6f; // km/h
            float speedFactor = 1f;

            if (currentSpeed < 5f) // Low speed range (0-5 km/h)
            {
                // Boost steering at very low speeds for maneuverability
                speedFactor = lowSpeedSteerBoost;
            }
            else if (currentSpeed > 25f) // Above average speed, reduce steering
            {
                speedFactor = Mathf.Lerp(1f, 0.1f, (currentSpeed - 25f) / 255f); // Scale from 25 to 280 km/h
            }

            // At very high speeds (200+ km/h), steering becomes extremely limited
            if (currentSpeed > 200f)
            {
                speedFactor *= 0.3f; // Additional reduction for extreme speeds
            }

            float steerAngle = Mathf.Clamp(turnAngle * speedFactor, minSteerClamp, maxSteerClamp);
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
            float currentSpeed = rb.linearVelocity.magnitude * 3.6f;
            float speedDifference = moveSpeed - currentSpeed;

            if (moveSpeed > 0.1f)
            {
                // Adjust motor force application based on speed range
                float motorMultiplier = 0.2f;

                // More aggressive acceleration at low speeds
                if (currentSpeed < 10f)
                {
                    motorMultiplier = 0.4f; // Double the motor force for low speeds
                }

                float torque = speedDifference * motorForce * motorMultiplier;
                torque = Mathf.Clamp(torque, -motorForce, motorForce);

                // Rear-wheel drive for motorcycles
                backWheelCollider.motorTorque = torque;
                frontWheelCollider.motorTorque = 0f; // No front motor assistance
            }
            else
            {
                backWheelCollider.motorTorque = 0f;
                frontWheelCollider.motorTorque = 0f;
            }

            // High-performance braking system
            if (moveSpeed < 0.1f || speedDifference < -5f)
            {
                float brakeAmount = moveSpeed < 0.1f ? brakeForce : brakeForce * 0.6f;

                // Distribute braking: more on front for stability
                backWheelCollider.brakeTorque = brakeAmount * 0.4f;
                frontWheelCollider.brakeTorque = brakeAmount * 0.6f;
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
        float speed = rb.linearVelocity.magnitude;
        float wheelRadius = frontWheelCollider != null ? frontWheelCollider.radius : 0.36f;
        float wheelCircumference = 2 * Mathf.PI * wheelRadius;
        float wheelRpm = speed / wheelCircumference;
        float wheelRotationSpeed = wheelRpm * 360f * Time.deltaTime;

        if (frontWheelTransform != null)
        {
            frontWheelTransform.Rotate(-Vector3.forward, wheelRotationSpeed);
        }

        if (backWheelTransform != null)
        {
            backWheelTransform.Rotate(Vector3.forward, wheelRotationSpeed);
        }

        UpdateWheelPositions();
    }

    private void UpdateWheelPositions()
    {
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

        Vector3 leftLocalPos = transform.InverseTransformPoint(leftController.position);
        Vector3 rightLocalPos = transform.InverseTransformPoint(rightController.position);

        float forwardBackwardDiff = leftLocalPos.z - rightLocalPos.z;
        float turnAngle = forwardBackwardDiff * turnSensitivity * rotationMultiplier;

        return Mathf.Clamp(turnAngle, -maxTurnAngle, maxTurnAngle);
    }
}