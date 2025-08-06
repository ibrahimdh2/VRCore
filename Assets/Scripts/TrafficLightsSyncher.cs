using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TrafficLightsSyncher : MonoBehaviour
{
    public List<TrafficLight> forwardOnly;
    public List<TrafficLight> rightOnly;
    public List<TrafficLight> leftOnly;
    public List<TrafficLight> back;

    public float greenDuration = 10f;
    public float yellowDuration = 3f;
    public float redBuffer = 1f;

    private void Start()
    {
        StartCoroutine(TrafficCycle());
    }

    private IEnumerator TrafficCycle()
    {
        while (true)
        {
            // Step 1: Forward + Back GREEN
            SetLights(forwardOnly, LightState.Green);
            SetLights(back, LightState.Green);
            SetLights(leftOnly, LightState.Red);
            SetLights(rightOnly, LightState.Red);

            yield return new WaitForSeconds(greenDuration);

            SetLights(forwardOnly, LightState.Yellow);
            SetLights(back, LightState.Yellow);

            yield return new WaitForSeconds(yellowDuration);

            SetLights(forwardOnly, LightState.Red);
            SetLights(back, LightState.Red);

            yield return new WaitForSeconds(redBuffer);

            // Step 2: Left Turn GREEN
            SetLights(leftOnly, LightState.Green);

            yield return new WaitForSeconds(greenDuration * 0.5f);

            SetLights(leftOnly, LightState.Yellow);

            yield return new WaitForSeconds(yellowDuration);

            SetLights(leftOnly, LightState.Red);

            yield return new WaitForSeconds(redBuffer);

            // Step 3: Right Turn GREEN
            SetLights(rightOnly, LightState.Green);

            yield return new WaitForSeconds(greenDuration * 0.5f);

            SetLights(rightOnly, LightState.Yellow);

            yield return new WaitForSeconds(yellowDuration);

            SetLights(rightOnly, LightState.Red);

            yield return new WaitForSeconds(redBuffer);
        }
    }

    private void SetLights(List<TrafficLight> lights, LightState state)
    {
        foreach (TrafficLight light in lights)
        {
            light.ChangeState(state);
        }
    }
}
