using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class BikeController : MonoBehaviour
{
    [Header("Handlebar Controllers")]
    public Transform leftController;
    public Transform rightController;
    public Transform frontWheelTransform;

    public enum TurnAngle { X, Y, Z }
    public TurnAngle currentturnAngle;

    public enum BikeBackAngle { X, Y, Z }
    public BikeBackAngle currentbackAngle;

    [Header("Speed & Display")]
    [SerializeField] private SpeedReceiver receiver;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Offsets")]
    [FormerlySerializedAs("offsets")]
    public Vector3 rotationOffsets;
    public Vector3 positionOffset;

    [Header("Movement")]
    public Transform wheelForwardTransform;

    [Header("Back Body Follow")]
    public Transform bikeContainer; //turn this when cycle turns
    public float turnFollowSpeed = 2f;
    private float moveSpeed;
    public Vector3 backBodyOffset;

    [Header("Handlebar Settings")]
    public float maxTurnAngle = 45f; // Maximum turn angle in degrees
    public float turnSensitivity = 1f; // Sensitivity multiplier for turning
    public float rotationMultiplier = 10f; // How much the rotation is affected

    void Update()
    {
        // 1. Get speed
        moveSpeed = receiver.speedKph;
        speedText.text = $"{moveSpeed:F2} km/h";

        // 2. Update front wheel position offset
        if (frontWheelTransform != null)
            frontWheelTransform.localPosition = positionOffset;

        // 3. Calculate handlebar direction in local space
        float turnAngle = CalculateHandlebarAngle();
        Debug.Log($"Turn angle: {turnAngle:F2} degrees");

        // 4. Rotate front wheel
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

        // 5. Move the bike forward
        if (wheelForwardTransform != null)
        {
            Vector3 forwardDir = wheelForwardTransform.forward;
            forwardDir.y = 0f;
            forwardDir.Normalize();
            transform.position += forwardDir * moveSpeed * Time.deltaTime;

            if (bikeContainer != null)
            {
                // Calculate target rotation based on wheel forward direction
                Vector3 targetForward = wheelForwardTransform.forward;
                targetForward.y = 0f; // Keep it horizontal
                targetForward.Normalize();

                // Create target rotation
                Quaternion targetRotation = Quaternion.LookRotation(targetForward, Vector3.up);

                // Apply back body offset rotation if needed
                if (backBodyOffset != Vector3.zero)
                {
                    Quaternion offsetRotation = Quaternion.Euler(backBodyOffset);
                    targetRotation *= offsetRotation;
                }

                // Smoothly interpolate to target rotation
                // The turn follow speed can be adjusted based on bike speed for more realistic behavior
                float actualTurnSpeed = turnFollowSpeed;

                // Optional: Make turn speed relative to bike speed (faster bike = slower turn response)
                if (moveSpeed > 0)
                {
                    // You can uncomment and adjust this formula for speed-dependent turning
                    actualTurnSpeed = turnFollowSpeed * (1f / (1f + moveSpeed * 0.1f));
                    bikeContainer.rotation = Quaternion.Slerp(bikeContainer.rotation, targetRotation, actualTurnSpeed * Time.deltaTime);
                }

            }
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
        float forwardBackwardDiff = leftLocalPos.z - rightLocalPos.z; // Swapped to fix direction

        // Convert the difference to an angle
        // Positive difference means left controller is forward (turn left)
        // Negative difference means right controller is forward (turn right)
        float turnAngle = forwardBackwardDiff * turnSensitivity * rotationMultiplier;

        // Clamp the turn angle to prevent extreme turns
        turnAngle = Mathf.Clamp(turnAngle, -maxTurnAngle, maxTurnAngle);

        return turnAngle;
    }
}