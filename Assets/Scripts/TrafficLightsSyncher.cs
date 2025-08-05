using UnityEngine;
using System.Collections.Generic;

public class TrafficLightsSyncher : MonoBehaviour
{
    public List<TrafficLight> forwardOrRight;
    public List<TrafficLight> forwardOnly;
    public List<TrafficLight> leftOrBack;
    
    public void ChangeSignals(LightState state)
    {
       
    }
}
