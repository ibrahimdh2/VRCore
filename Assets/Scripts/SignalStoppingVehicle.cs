using UnityEngine;

public class SignalStoppingVehicle : MonoBehaviour
{

    public TrafficLight signal = null;
    public bool bicycleSlowdown;
    public SpeedReceiver speedReceiver;
    private float lastReceivedTime;
    public Vector3 halfExtents;
    public int raycastLength;
    public Vector3 boxOffset;

    void Update()
    {


        if (bicycleSlowdown)
        {
            if (IsAVehicleAhead())
            {
                speedReceiver.Stop();
            }
            else
            {

                if (signal != null)
                {
                    if (bicycleSlowdown)
                    {
                        if (signal.State == LightState.Red)
                        {

                            speedReceiver.Stop();


                        }
                        else if (signal.State == LightState.Green)
                        {
                            speedReceiver.Resume();
                        }
                    }
                }
                else
                {
                    speedReceiver.Resume();
                }
            } 
        }
        else
        {
            if (signal != null && bicycleSlowdown)
            {
 
                    if (signal.State == LightState.Red)
                    {

                        speedReceiver.Stop();


                    }
                    else if (signal.State == LightState.Green)
                    {
                        speedReceiver.Resume();
                    }
            }
            
        }

    }
    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("Signal Set");
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

    private bool IsAVehicleAhead()
    {
        Vector3 boxCenter = transform.position + boxOffset + Vector3.up * 0.5f;
        Quaternion orientation = transform.rotation;
        if (Physics.BoxCast(boxCenter + boxOffset, halfExtents, transform.forward, out RaycastHit hit, orientation, raycastLength))
        {
            if (hit.collider.CompareTag("Vehicle") && hit.collider.gameObject.name != "Bike Controller")
            {
                Debug.Log($"Vehicle Ahead name is {hit.collider.name}");
                return true;
            }
            else
            {
                return false;
            }
        }
        return false;
    }

    private void OnTriggerExit(Collider other)
    {
        signal = null;

    }
    private void OnDrawGizmosSelected()
    {
        if (bicycleSlowdown)
        {
            Gizmos.color = Color.red;

            Vector3 boxCenter = transform.position + boxOffset + Vector3.up * 0.5f;
            Quaternion orientation = transform.rotation;

            Matrix4x4 rotationMatrix = Matrix4x4.TRS(boxCenter + transform.forward * raycastLength * 0.5f, orientation, Vector3.one);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2 + new Vector3(0, 0, raycastLength)); 
        }
    }
}