using System.Collections;
using UnityEngine;

public class CarMovementController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotateSpeed = 5f;
    public float maxSteerAngle = 30f;

    [Header("Wheel Transforms")]
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    [SerializeField ]private Transform[] waypoints;
    private int currentIndex = 0;

    private Quaternion frontLeftOriginalRotation;
    private Quaternion frontRightOriginalRotation;
    public float raycastLength;
    [SerializeField]private bool isPaused = false;
    public Vector3 halfExtents;

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

    public void Pause()
    {
        isPaused = true;
    }

    public void Resume()
    {
        isPaused = false;
    }

    IEnumerator MoveRoutine()
    {
        while (currentIndex < waypoints.Length)
        {
            Vector3 targetPos = new Vector3(waypoints[currentIndex].position.x, transform.position.y, waypoints[currentIndex].position.z);

            while (Vector3.Distance(transform.position, targetPos) > 0.1f)
            {
                Vector3 boxCenter = transform.position + Vector3.up * 0.5f;
        
                Quaternion orientation = transform.rotation;

                if (Physics.BoxCast(boxCenter, halfExtents, transform.forward, out RaycastHit hit, orientation, raycastLength))

                {
                    // Ignore self or any children
                    if (hit.collider.CompareTag("Vehicle"))
                    {
                        isPaused = true;
                    }
                    else
                    {
                        isPaused = false;
                    }
                }
                else
                {
                    if ((signal != null && signal.State != LightState.Green))
                    {
                        isPaused = true;
                    }
                    else
                    {

                        isPaused = false;
                    }
                }

                if (!isPaused)
                {
                    Vector3 direction = (targetPos - transform.position).normalized;
                    direction.y = 0;

                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    float steerAngle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
                    steerAngle = Mathf.Clamp(steerAngle, -maxSteerAngle, maxSteerAngle);

                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

                    float rotationAmount = moveSpeed * 360f * Time.deltaTime;
                    RotateWheels(rotationAmount, steerAngle);
                }

                yield return null;
            }

            currentIndex++;
            yield return null;
        }

        VehiclePoolManager.Instance.ReturnCar(gameObject);
    }

    public TrafficLight signal = null;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Signal Set");
        if (other.CompareTag("Stopper"))
        {
            if (signal == null)
            {
                if (other.gameObject.TryGetComponent<TrafficLightStopper>(out TrafficLightStopper stopper))
                {
                    signal = stopper.trafficLight;
                }
            }

            
        }
    }
    

    private void OnTriggerExit(Collider other)
    {
        signal = null;
    }

    void RotateWheels(float rotationAmount, float steerAngle)
    {
        rearLeftWheel?.Rotate(Vector3.right, -rotationAmount);
        rearRightWheel?.Rotate(Vector3.right, -rotationAmount);

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

    // Optional: visualize the ray in the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 boxCenter = transform.position + Vector3.up * 0.5f;
        Quaternion orientation = transform.rotation;

        Matrix4x4 rotationMatrix = Matrix4x4.TRS(boxCenter + transform.forward * raycastLength * 0.5f, orientation, Vector3.one);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2 + new Vector3(0, 0, raycastLength));
    }

}
