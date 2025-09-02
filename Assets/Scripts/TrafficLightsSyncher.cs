using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum MainFlowDirection
{
    ForwardBack,
    LeftRight
}

public class TrafficLightsSyncher : MonoBehaviour
{
    [Header("Traffic Light Groups")]
    public List<TrafficLight> forwardOnly;
    public List<TrafficLight> rightOnly;
    public List<TrafficLight> leftOnly;
    public List<TrafficLight> back;

    [Header("Timing Settings")]
    public float greenDuration = 20f;   // Main lane green
    public float yellowDuration = 4f;
    public float redBuffer = 2f;
    public float sideGreenMultiplier = 0.5f; // Side lanes are shorter

    [Header("Main Flow Setting")]
    public MainFlowDirection mainFlow = MainFlowDirection.ForwardBack;

    private void Start()
    {
        StartCoroutine(TrafficCycle());
    }

    private IEnumerator TrafficCycle()
    {
        while (true)
        {
            if (mainFlow == MainFlowDirection.ForwardBack)
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

                yield return new WaitForSeconds(greenDuration * sideGreenMultiplier);

                SetLights(leftOnly, LightState.Yellow);

                yield return new WaitForSeconds(yellowDuration);

                SetLights(leftOnly, LightState.Red);

                yield return new WaitForSeconds(redBuffer);

                // Step 3: Right Turn GREEN
                SetLights(rightOnly, LightState.Green);

                yield return new WaitForSeconds(greenDuration * sideGreenMultiplier);

                SetLights(rightOnly, LightState.Yellow);

                yield return new WaitForSeconds(yellowDuration);

                SetLights(rightOnly, LightState.Red);

                yield return new WaitForSeconds(redBuffer);
            }
            else if (mainFlow == MainFlowDirection.LeftRight)
            {
                // Step 1: Left + Right GREEN
                SetLights(leftOnly, LightState.Green);
                SetLights(rightOnly, LightState.Green);
                SetLights(forwardOnly, LightState.Red);
                SetLights(back, LightState.Red);

                yield return new WaitForSeconds(greenDuration);

                SetLights(leftOnly, LightState.Yellow);
                SetLights(rightOnly, LightState.Yellow);

                yield return new WaitForSeconds(yellowDuration);

                SetLights(leftOnly, LightState.Red);
                SetLights(rightOnly, LightState.Red);

                yield return new WaitForSeconds(redBuffer);

                // Step 2: Forward GREEN
                SetLights(forwardOnly, LightState.Green);

                yield return new WaitForSeconds(greenDuration * sideGreenMultiplier);

                SetLights(forwardOnly, LightState.Yellow);

                yield return new WaitForSeconds(yellowDuration);

                SetLights(forwardOnly, LightState.Red);

                yield return new WaitForSeconds(redBuffer);

                // Step 3: Back GREEN
                SetLights(back, LightState.Green);

                yield return new WaitForSeconds(greenDuration * sideGreenMultiplier);

                SetLights(back, LightState.Yellow);

                yield return new WaitForSeconds(yellowDuration);

                SetLights(back, LightState.Red);

                yield return new WaitForSeconds(redBuffer);
            }
        }
    }

    private void SetLights(List<TrafficLight> lights, LightState state)
    {
        foreach (TrafficLight light in lights)
        {
            if (light != null)
                light.ChangeState(state);
        }
    }
}
