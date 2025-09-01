using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class CarBrakeModifier : MonoBehaviour
{
    [Header("Brake Settings")]
    [Range(0f, 1f)]
    public float brakeStrength = 0.5f;

    [Header("Speed Control")]
    public bool useAbsoluteSpeed = false;
    public float targetSpeed = 2f;

    [Header("Brake Behavior")]
    public bool gradualBraking = true;
    public float brakeTransitionSpeed = 5f;
    public bool maintainBrakeOnExit = false;
    public float exitDelayTime = 2f;

    [Header("Visual Feedback")]
    public bool showDebugLogs = false;
    public Color gizmoColor = Color.yellow;

    [Header("Advanced")]
    public bool onlyAffectTaggedVehicles = true;
    public string vehicleTag = "Vehicle";

    // Internal tracking
    private Dictionary<CarMovementController, float> originalSpeeds = new Dictionary<CarMovementController, float>();
    private Dictionary<CarMovementController, System.Collections.IEnumerator> exitCoroutines = new Dictionary<CarMovementController, System.Collections.IEnumerator>();

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"CarBrakeModifier on {gameObject.name} requires a Collider!");
            return;
        }
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (onlyAffectTaggedVehicles && !other.CompareTag(vehicleTag)) return;

        var car = other.GetComponent<CarMovementController>();
        if (car == null) return;

        if (exitCoroutines.ContainsKey(car) && exitCoroutines[car] != null)
        {
            StopCoroutine(exitCoroutines[car]);
            exitCoroutines.Remove(car);
        }

        ApplyBrake(car);
    }

    private void OnTriggerExit(Collider other)
    {
        if (onlyAffectTaggedVehicles && !other.CompareTag(vehicleTag)) return;

        var car = other.GetComponent<CarMovementController>();
        if (car == null) return;

        if (maintainBrakeOnExit && exitDelayTime > 0f)
        {
            var coroutine = DelayedBrakeRemoval(car);
            exitCoroutines[car] = coroutine;
            StartCoroutine(coroutine);
        }
        else if (!maintainBrakeOnExit)
        {
            RemoveBrake(car);
        }
    }

    private void ApplyBrake(CarMovementController car)
    {
        if (!originalSpeeds.ContainsKey(car))
        {
            originalSpeeds[car] = GetCarMaxSpeed(car);
        }

        float newSpeed = useAbsoluteSpeed
            ? targetSpeed
            : originalSpeeds[car] * (1f - brakeStrength);

        if (gradualBraking)
        {
            SetCarMaxSpeed(car, newSpeed);
        }
        else
        {
            float originalChangeRate = GetCarSpeedChangeRate(car);
            SetCarSpeedChangeRate(car, 50f);
            SetCarMaxSpeed(car, newSpeed);
            StartCoroutine(RestoreChangeRate(car, originalChangeRate));
        }
    }

    private void RemoveBrake(CarMovementController car)
    {
        if (originalSpeeds.TryGetValue(car, out float originalSpeed))
        {
            SetCarMaxSpeed(car, originalSpeed);
            originalSpeeds.Remove(car);
        }
    }

    private System.Collections.IEnumerator DelayedBrakeRemoval(CarMovementController car)
    {
        yield return new WaitForSeconds(exitDelayTime);
        RemoveBrake(car);
        exitCoroutines.Remove(car);
    }

    private System.Collections.IEnumerator RestoreChangeRate(CarMovementController car, float originalRate)
    {
        yield return null;
        SetCarSpeedChangeRate(car, originalRate);
    }

    // --- ADAPTERS (no need to edit CarMovementController) ---
    private float GetCarMaxSpeed(CarMovementController car)
    {
        return car.GetTargetMaxSpeed(); // if method exists
    }

    private void SetCarMaxSpeed(CarMovementController car, float value)
    {
        car.SetMaxSpeed(value); // if method exists
    }

    private float GetCarSpeedChangeRate(CarMovementController car)
    {
        return car.speedChangeRate; // if field exists
    }

    private void SetCarSpeedChangeRate(CarMovementController car, float value)
    {
        car.speedChangeRate = value;
    }
}
