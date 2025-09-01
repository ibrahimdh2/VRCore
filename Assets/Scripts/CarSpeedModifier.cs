using UnityEngine;
using System.Collections.Generic;

public class CarBrakeModifier : MonoBehaviour
{
    [Header("Brake Settings")]
    [Range(0f, 1f)]
    public float brakeStrength = 0.5f; // 0 = no effect, 1 = full stop

    [Header("Speed Control")]
    public bool useAbsoluteSpeed = false; // If true, set exact speed instead of multiplier
    public float targetSpeed = 2f; // Used when useAbsoluteSpeed is true

    [Header("Brake Behavior")]
    public bool gradualBraking = true; // Smooth transition vs instant
    public float brakeTransitionSpeed = 5f; // How fast to apply/remove brake
    public bool maintainBrakeOnExit = false; // Keep braking after leaving trigger
    public float exitDelayTime = 2f; // Delay before removing brake on exit

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
        // Ensure we have a trigger collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"CarBrakeModifier on {gameObject.name} requires a Collider component!");
            return;
        }

        if (!col.isTrigger)
        {
            Debug.LogWarning($"CarBrakeModifier on {gameObject.name}: Collider should be set as Trigger!");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if it's a vehicle we should affect
        if (onlyAffectTaggedVehicles && !other.CompareTag(vehicleTag))
            return;

        CarMovementController car = other.GetComponent<CarMovementController>();
        if (car == null)
            return;

        // Cancel any pending exit coroutine for this car
        if (exitCoroutines.ContainsKey(car) && exitCoroutines[car] != null)
        {
            StopCoroutine(exitCoroutines[car]);
            exitCoroutines.Remove(car);
        }

        ApplyBrake(car);

        if (showDebugLogs)
            Debug.Log($"CarBrakeModifier: Applied brake to {other.name} with strength {brakeStrength}");
    }

    private void OnTriggerExit(Collider other)
    {
        // Check if it's a vehicle we should affect
        if (onlyAffectTaggedVehicles && !other.CompareTag(vehicleTag))
            return;

        CarMovementController car = other.GetComponent<CarMovementController>();
        if (car == null)
            return;

        if (maintainBrakeOnExit && exitDelayTime > 0f)
        {
            // Start delayed removal of brake
            var coroutine = DelayedBrakeRemoval(car);
            exitCoroutines[car] = coroutine;
            StartCoroutine(coroutine);
        }
        else if (!maintainBrakeOnExit)
        {
            // Remove brake immediately
            RemoveBrake(car);
        }

        if (showDebugLogs)
            Debug.Log($"CarBrakeModifier: {other.name} exited brake zone");
    }

    private void ApplyBrake(CarMovementController car)
    {
        // Store original speed if not already stored
        if (!originalSpeeds.ContainsKey(car))
        {
            originalSpeeds[car] = car.GetTargetMaxSpeed();
        }

        float newSpeed;

        if (useAbsoluteSpeed)
        {
            // Set to specific speed
            newSpeed = targetSpeed;
        }
        else
        {
            // Apply brake as multiplier
            float originalSpeed = originalSpeeds[car];
            newSpeed = originalSpeed * (1f - brakeStrength);
        }

        // Apply the new speed
        if (gradualBraking)
        {
            car.SetMaxSpeed(newSpeed);
        }
        else
        {
            // For instant braking, we need to modify the speed change rate temporarily
            float originalChangeRate = car.speedChangeRate;
            car.speedChangeRate = 50f; // Very fast transition
            car.SetMaxSpeed(newSpeed);

            // Restore original change rate after a frame
            StartCoroutine(RestoreChangeRate(car, originalChangeRate));
        }
    }

    private void RemoveBrake(CarMovementController car)
    {
        if (originalSpeeds.ContainsKey(car))
        {
            // Restore original speed
            float originalSpeed = originalSpeeds[car];
            car.SetMaxSpeed(originalSpeed);

            // Clean up stored data
            originalSpeeds.Remove(car);

            if (showDebugLogs)
                Debug.Log($"CarBrakeModifier: Restored {car.name} to original speed {originalSpeed}");
        }
    }

    private System.Collections.IEnumerator DelayedBrakeRemoval(CarMovementController car)
    {
        yield return new WaitForSeconds(exitDelayTime);

        RemoveBrake(car);

        // Clean up coroutine tracking
        if (exitCoroutines.ContainsKey(car))
            exitCoroutines.Remove(car);
    }

    private System.Collections.IEnumerator RestoreChangeRate(CarMovementController car, float originalRate)
    {
        yield return null; // Wait one frame
        if (car != null)
            car.speedChangeRate = originalRate;
    }

    // Public methods for runtime control
    public void SetBrakeStrength(float strength)
    {
        brakeStrength = Mathf.Clamp01(strength);

        // Update all currently affected cars
        foreach (var kvp in originalSpeeds)
        {
            if (kvp.Key != null)
                ApplyBrake(kvp.Key);
        }
    }

    public void SetTargetSpeed(float speed)
    {
        targetSpeed = Mathf.Max(0f, speed);

        if (useAbsoluteSpeed)
        {
            // Update all currently affected cars
            foreach (var kvp in originalSpeeds)
            {
                if (kvp.Key != null)
                    ApplyBrake(kvp.Key);
            }
        }
    }

    public void EnableBraking()
    {
        enabled = true;
    }

    public void DisableBraking()
    {
        // Restore all cars before disabling
        var carsToRestore = new List<CarMovementController>(originalSpeeds.Keys);
        foreach (var car in carsToRestore)
        {
            if (car != null)
                RemoveBrake(car);
        }

        enabled = false;
    }

    // Cleanup when destroyed
    private void OnDestroy()
    {
        // Restore all cars when the modifier is destroyed
        var carsToRestore = new List<CarMovementController>(originalSpeeds.Keys);
        foreach (var car in carsToRestore)
        {
            if (car != null)
                RemoveBrake(car);
        }
    }

    // Visual debugging
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                // Approximation for capsule
                Gizmos.DrawWireSphere(capsule.center, capsule.radius);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw a more visible version when selected
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Color selectedColor = gizmoColor;
            selectedColor.a = 0.3f;
            Gizmos.color = selectedColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
        }
    }
}