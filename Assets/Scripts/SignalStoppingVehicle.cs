using UnityEngine;

public class SignalStoppingVehicle : MonoBehaviour
{

    public TrafficLight signal = null;
    public bool bicycleSlowdown;
    public SpeedReceiver speedReceiver;
    private float lastReceivedTime;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Signal Set");
        if (other.CompareTag("Stopper"))
        {
            if (signal == null)
            {
                if (other.gameObject.TryGetComponent<TrafficLightStopper>(out TrafficLightStopper stopper))
                {
                    if (!bicycleSlowdown)
                    {
                        signal = stopper.trafficLight; 
                    }
                    else
                    {
                        speedReceiver.Stop();
                    }
                }
            }


        }
        
    }


    private void OnTriggerExit(Collider other)
    {
        signal = null;
        if (speedReceiver != null)
        {
            speedReceiver.Resume();
        }
    }
}