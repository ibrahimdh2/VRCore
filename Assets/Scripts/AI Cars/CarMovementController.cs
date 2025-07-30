using System.Collections;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotateSpeed = 5f;
    public float maxSteerAngle = 30f; // Maximum steering angle for front wheels

    [Header("Wheel Transforms")]
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    private Transform[] waypoints;
    private int currentIndex = 0;

    // To cache original local rotations of the front wheels
    private Quaternion frontLeftOriginalRotation;
    private Quaternion frontRightOriginalRotation;

    private void Awake()
    {
        if (frontLeftWheel != null) frontLeftOriginalRotation = frontLeftWheel.localRotation;
        if (frontRightWheel != null) frontRightOriginalRotation = frontRightWheel.localRotation;
    }

    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints = newWaypoints;
        currentIndex = 0;
        StopAllCoroutines();
        StartCoroutine(MoveRoutine());
    }

    IEnumerator MoveRoutine()
    {
        while (currentIndex < waypoints.Length)
        {
            Vector3 targetPos = new Vector3(waypoints[currentIndex].position.x, transform.position.y, waypoints[currentIndex].position.z);

            while (Vector3.Distance(transform.position, targetPos) > 0.1f)
            {
                Vector3 direction = (targetPos - transform.position).normalized;
                direction.y = 0;

                // Calculate target rotation and steer angle
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                float steerAngle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
                steerAngle = Mathf.Clamp(steerAngle, -maxSteerAngle, maxSteerAngle);

                // Smoothly rotate car body
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);

                // Move forward
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

                // Rotate wheels visually
                float rotationAmount = moveSpeed * 360f * Time.deltaTime;
                RotateWheels(rotationAmount, steerAngle);

                yield return null;
            }

            currentIndex++;
            yield return null;
        }

        // Return to pool when done
        VehiclePoolManager.Instance.ReturnCar(gameObject);
    }

    void RotateWheels(float rotationAmount, float steerAngle)
    {
        // Rotate rear wheels (no steering)
        rearLeftWheel?.Rotate(Vector3.right, -rotationAmount);
        rearRightWheel?.Rotate(Vector3.right, -rotationAmount);

        // Rotate front wheels (steering + rolling)
        if (frontLeftWheel != null)
        {
            frontLeftWheel.localRotation = frontLeftOriginalRotation * Quaternion.Euler(0, steerAngle, 0);
            frontLeftWheel.Rotate(Vector3.right, -rotationAmount);
        }

        if (frontRightWheel != null)
        {
            frontRightWheel.localRotation = frontRightOriginalRotation * Quaternion.Euler(0, steerAngle, 0);
            frontRightWheel.Rotate(Vector3.right, -rotationAmount);
        }
    }
}
