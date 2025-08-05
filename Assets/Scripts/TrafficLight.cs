using UnityEngine;
using static TrafficLight;

public  enum LightState { Red, Yellow, Green };
public class TrafficLight : MonoBehaviour
{
    [SerializeField] private LightState testLightState;
    private LightState state;
    public LightState State
    {
        get
        {
            return state;
        }
        set
        {

            state = value;
            ChangeSignal();
        }
    }
    public GameObject[] lightObject = new GameObject[3];
    public void ChangeState(LightState state)
    {
        State = state;
    }
    

    public void ChangeSignal()
    {
        for (int i = 0; i < lightObject.Length; i++)
        {
            GameObject light = lightObject[i];
            if (i != (int) State)
            {
                lightObject[i].SetActive(true);    
            }
            else
            {
                lightObject[i].SetActive(false);
            }
        }

       
    }

    [ContextMenu("ChangeLightState")]
    public void ChangeStateToTestLightState()
    {
        
        ChangeState(testLightState);
    }
}
