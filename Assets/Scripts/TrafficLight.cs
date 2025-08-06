using UnityEngine;
using UnityEngine.Events;

public enum LightState { Red, Yellow, Green };

public class TrafficLight : MonoBehaviour
{
    [SerializeField] private LightState testLightState;
    [SerializeField] private float waitTime = 5f;
    private float lastChangeTime;
    private LightState state;
    private LightState lastPrimaryState; // Track Red or Green (not Yellow)
    public UnityEvent<LightState> OnLightChanged = new();
    public bool autoOnOff;

    public GameObject[] lightObject = new GameObject[3];

    public LightState State
    {
        get => state;
        set
        {
            state = value;
            ChangeSignal();
        }
    }

    public void ChangeState(LightState newState)
    {
        State = newState;
        if (newState == LightState.Red || newState == LightState.Green)
        {
            lastPrimaryState = newState; // Update last known Red/Green state
        }
    }

    public void ChangeSignal()
    {
        for (int i = 0; i < lightObject.Length; i++)
        {
            lightObject[i].SetActive(i != (int)state);
        }
        OnLightChanged.Invoke(State);
    }

    [ContextMenu("ChangeLightState")]
    public void ChangeStateToTestLightState()
    {
        ChangeState(testLightState);
    }

    private void Start()
    {
        lastChangeTime = Time.time;
        lastPrimaryState = state; // Initialize
    }

    private void Update()
    {
        if (!autoOnOff) return;

        if (Time.time - lastChangeTime >= waitTime)
        {
            lastChangeTime = Time.time; // Reset timer

            // Start the yellow-light coroutine
            StartCoroutine(TransitionWithYellow());
        }
    }

    private System.Collections.IEnumerator TransitionWithYellow()
    {
        ChangeState(LightState.Yellow);
        yield return new WaitForSeconds(0.5f); // Wait during yellow

        // Switch to the opposite of the last Red/Green
        if (lastPrimaryState == LightState.Red)
        {
            ChangeState(LightState.Green);
        }
        else if (lastPrimaryState == LightState.Green)
        {
            ChangeState(LightState.Red);
        }
    }
}
