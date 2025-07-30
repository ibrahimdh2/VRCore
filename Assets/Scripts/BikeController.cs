using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class BikeController : MonoBehaviour
{
    [Header("Handlebar Controllers")]
    public Transform leftController;
    public Transform rightController;
    public Transform frontWheelTransform;
    public Vector3 rotationOffsets;
    public Vector3 positionOffsets;

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
        // Apply the handlebar angle directly to the front wheel collider
        if (frontWheelCollider != null)
        {
            // Clamp the steering angle to prevent unrealistic turns
            float steerAngle = Mathf.Clamp(turnAngle, -maxSteerAngle, maxSteerAngle);
            frontWheelCollider.steerAngle = steerAngle;
        }

        // Also update the visual front wheel rotation
        if (frontWheelTransform != null)
        {
            switch (currentturnAngle)
            {
                case TurnAngle.X:
                    frontWheelTransform.localRotation = Quaternion.Euler(turnAngle + rotationOffsets.x, rotationOffsets.y, rotationOffsets.z);
                    break;
                case TurnAngle.Y:
                    frontWheelTransform.localRotation = Quaternion.Euler(rotationOffsets.x, turnAngle + rotationOffsets.y, rotationOffsets.z);
                    break;
                case TurnAngle.Z:
                    frontWheelTransform.localRotation = Quaternion.Euler(rotationOffsets.x, rotationOffsets.y, turnAngle + rotationOffsets.z);
                    break;
            }
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
        // Update front wheel visual position and rotation
        if (frontWheelCollider != null && frontWheelTransform != null)
        {
            Vector3 pos;
            Quaternion rot;
            frontWheelCollider.GetWorldPose(out pos, out rot);

            // Update position with offset
            frontWheelTransform.position = pos + transform.TransformDirection(positionOffsets);

            // The steering rotation is already handled in ApplySteering()
            // But we need to add the wheel spinning rotation
            Vector3 eulerRot = rot.eulerAngles;
            switch (currentturnAngle)
            {
                case TurnAngle.X:
                    frontWheelTransform.rotation = Quaternion.Euler(
                        frontWheelCollider.steerAngle + rotationOffsets.x,
                        eulerRot.y + rotationOffsets.y,
                        eulerRot.z + rotationOffsets.z
                    );
                    break;
                case TurnAngle.Y:
                    frontWheelTransform.rotation = Quaternion.Euler(
                        eulerRot.x + rotationOffsets.x,
                        frontWheelCollider.steerAngle + rotationOffsets.y,
                        eulerRot.z + rotationOffsets.z
                    );
                    break;
                case TurnAngle.Z:
                    frontWheelTransform.rotation = Quaternion.Euler(
                        eulerRot.x + rotationOffsets.x,
                        eulerRot.y + rotationOffsets.y,
                        frontWheelCollider.steerAngle + rotationOffsets.z
                    );
                    break;
            }
        }

        //// Update back wheel visual position and rotation
        //if (backWheelCollider != null && backWheelTransform != null)
        //{
        //    Vector3 pos;
        //    Quaternion rot;
        //    backWheelCollider.GetWorldPose(out pos, out rot);
        //    backWheelTransform.position = pos;
        //    backWheelTransform.rotation = rot;
        //}
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