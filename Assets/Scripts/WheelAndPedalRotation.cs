using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelAndPedalRotation : MonoBehaviour
{
    [SerializeField] private Transform frontWheel;
    [SerializeField] private Transform backWheel;
    [SerializeField] private Transform leftPedal;
    [SerializeField] private Transform rightPedal;
    [SerializeField] private Transform pedalGear;

    [SerializeField] private float wheelRotationSpeed;
    [SerializeField] private float pedalRotationSpeed;
    [SerializeField] private float pedalRotationSpeedModifier;
    [SerializeField] private float wheelRotationSpeedModifier;


    [SerializeField] private WaypointMovement waypointMovement;
    [SerializeField] private Animator anim;

    void Update()
    {
        anim.speed = waypointMovement.moveSpeed / 10f;
        pedalRotationSpeed = (waypointMovement.moveSpeed * pedalRotationSpeedModifier) * Time.deltaTime;
        wheelRotationSpeed = (pedalRotationSpeed * wheelRotationSpeedModifier) * Time.deltaTime;
        pedalGear.Rotate(Vector3.right, pedalRotationSpeed);
        leftPedal.Rotate(-Vector3.right, pedalRotationSpeed);
        rightPedal.Rotate(Vector3.right, pedalRotationSpeed);
        frontWheel.Rotate(Vector3.forward, wheelRotationSpeed);
        backWheel.Rotate(Vector3.forward, wheelRotationSpeed);
    }
}
