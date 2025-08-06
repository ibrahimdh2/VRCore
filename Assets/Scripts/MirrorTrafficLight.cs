using NUnit.Framework;
using UnityEngine;

public class MirrorTrafficLight : MonoBehaviour
{
    public GameObject[] lightObject = new GameObject[3];

    public void ChangeSignal(LightState state)
    {
        for (int i = 0; i < lightObject.Length; i++)
        {
            lightObject[i].SetActive(i != (int)state);
        }
    }
}
