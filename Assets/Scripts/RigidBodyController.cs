using UnityEngine;

public class RigidBodyController : MonoBehaviour
{
    public Transform leftController;
    public Transform rightController;

    public Transform frontWheel;
    public Transform backWheel;

    public Transform turningPivot; // assign in inspector (e.g., front wheel pivot point)

    public SpeedReceiver speedReceiver;
    public float multiplier = 1f;
    public float turnSensitivity = 1f; // sensitivity for steering
    public Rigidbody rb;

    public float calculateAngle;

    public float straightAngle;

    void Update()
    {
        // Get direct handlebar angle (wrapped to -180 to 180)
        if (!TryGetHandlebarYawWrapped(out float wrappedYawDeg))
            return;

        // Apply sensitivity and trim
        float angle = (wrappedYawDeg - straightAngle) * turnSensitivity;

        // Rotate around turning pivot (local Y axis)
        if (turningPivot != null)
        {
            transform.RotateAround(turningPivot.position, Vector3.up, angle * Time.deltaTime);
        }
        else
        {
            transform.Rotate(Vector3.up, angle * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        // Forward motion
        rb.linearVelocity = transform.forward * multiplier * speedReceiver.speedKph;
    }

    private bool TryGetHandlebarYawWrapped(out float angleDeg)
    {
        angleDeg = 0f;
        if (!leftController || !rightController) return false;

        // Controller positions in bike-local space, ignore vertical
        Vector3 leftLocal = transform.InverseTransformPoint(leftController.position);
        Vector3 rightLocal = transform.InverseTransformPoint(rightController.position);
        leftLocal.y = 0f; rightLocal.y = 0f;

        Vector3 barRight = rightLocal - leftLocal;           // grips line (left→right)
        if (barRight.sqrMagnitude < 1e-6f) return false;     // hands coincide: no reliable reading

        // Perpendicular gives the bar's forward in bike space:
        Vector3 barForward = Vector3.Cross(barRight, Vector3.up).normalized;

        // Yaw relative to bike forward (Z+)
        angleDeg = Vector3.SignedAngle(Vector3.forward, barForward, Vector3.up);
        calculateAngle = angleDeg;
        return true;
    }
}
