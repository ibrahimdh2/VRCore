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

                    signal = stopper.trafficLight;
                    
                    
                }
            }


        }

    }
    private void OnTriggerStay(Collider other)
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
                    Debug.Log("Should Start Moving Again");
                    speedReceiver.Resume();
                }
            } 
        }
    }


    private void OnTriggerExit(Collider other)
    {
        signal = null;

    }
}