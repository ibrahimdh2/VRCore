using System;
using TMPro;
using UnityEngine;

public class RigidBodyController : MonoBehaviour
{
    [Header("VR Handle References")]
    public Transform leftController;
    public Transform rightController;

    [Header("Bike Parts")]
    public Transform frontHandleAssembly;   // handlebars + fork root
    public Transform frontFork;             // fork object that turns (Y rotation)
    public Transform frontWheelSpin;        // object that spins around X
    public Transform backWheel;             // back wheel (spins only)
    public Transform turningPivot;          // pivot point of bike

    [Header("Physics & Control")]
    public SpeedReceiver speedReceiver;
    public float multiplier = 1f;
    public float turnSensitivity = 1f;      // how responsive the bike is
    public Rigidbody rb;

    [Header("Steering Settings")]
    public float maxVisualSteer = 45f;      // max degrees handlebars can turn visually
    public float turnSharpness = 2f;        // higher = sharper real turning

    [Header("Debug")]
    public float handlebarAngle;            // raw VR handlebar angle
    public float straightAngle;

    private float wheelRadius = 0.35f; // in meters
    [Header("Visual Steering Correction")]
    public float visualSteerMultiplier = 1f;   // scale the visual steering
    public float visualSteerOffset = 0f;       // shift if visuals feel off
    public float CurrentAngleRaw { get; private set; }
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI simulationSpeedText;
   

    void Update()
    {
        speedText.text = speedReceiver.speedKph.ToString("f2");
        simulationSpeedText.text = rb.linearVelocity.magnitude.ToString("f2");
        // Get VR handlebar input
        if (!TryGetHandlebarYawWrapped(out float wrappedYawDeg))
            return;

        // Visual steering (direct from VR)
        CurrentAngleRaw = wrappedYawDeg; // store raw yaw before offset
        handlebarAngle = (wrappedYawDeg - straightAngle) * turnSensitivity;

        // Calculate visual steering separately from bike turning
        float visualAngle = handlebarAngle * visualSteerMultiplier + visualSteerOffset;
        visualAngle = Mathf.Clamp(visualAngle, -maxVisualSteer, maxVisualSteer);

        // Apply visual rotation to handlebars + fork
        if (frontHandleAssembly != null)
            frontHandleAssembly.localRotation = Quaternion.Euler(0, visualAngle, 0);

        if (frontFork != null)
            frontFork.localRotation = Quaternion.Euler(0, visualAngle, 0);

        // Calculate effective turning angle for bike body (removed speed reduction)
        float turningAngle = (visualAngle / maxVisualSteer) * turnSharpness;

        // Rotate bike around pivot
        if (!(speedReceiver.speedKph < 1))
        {
            if (turningPivot != null)
                transform.RotateAround(turningPivot.position, Vector3.up, turningAngle * Time.deltaTime * 50f);
            else
                transform.Rotate(Vector3.up, turningAngle * Time.deltaTime * 50f); 
        }

        // Spin wheels visually
        RotateWheels();
    }

    private void FixedUpdate()
    {
        // Apply forward velocity
        rb.linearVelocity = transform.forward * multiplier * speedReceiver.speedKph;
       // Debug.Log($"{rb.linearVelocity}");
    }

    private void RotateWheels()
    {
        float speedMS = speedReceiver.speedKph / 3.6f; // kph → m/s
        float angularVel = speedMS / wheelRadius;      // rad/s
        float degPerFrame = angularVel * Mathf.Rad2Deg * Time.deltaTime;

        if (frontWheelSpin != null)
            frontWheelSpin.Rotate(Vector3.right, degPerFrame, Space.Self);

        if (backWheel != null)
            backWheel.Rotate(Vector3.right, degPerFrame, Space.Self);
    }

    private bool TryGetHandlebarYawWrapped(out float angleDeg)
    {
        angleDeg = 0f;
        if (!leftController || !rightController) return false;

        Vector3 leftLocal = transform.InverseTransformPoint(leftController.position);
        Vector3 rightLocal = transform.InverseTransformPoint(rightController.position);
        leftLocal.y = 0f; rightLocal.y = 0f;

        Vector3 barRight = rightLocal - leftLocal;
        if (barRight.sqrMagnitude < 1e-6f) return false;

        Vector3 barForward = Vector3.Cross(barRight, Vector3.up).normalized;
        angleDeg = Vector3.SignedAngle(Vector3.forward, barForward, Vector3.up);
        return true;
    }

    
    internal float GetBicycleVelocity()
    {
        return rb.linearVelocity.magnitude * multiplier;
    }
}