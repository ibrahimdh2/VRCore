using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WaypointMovement : MonoBehaviour
{
    public float moveSpeed;
    public float rotateSpeed;
    public GameObject[] wayPoints;
    public int wayPointIndex;
    public Vector3 currentWayPoint;

    public enum WaypointTraversalOrder { Ascending, Descending, Loop };
    public WaypointTraversalOrder currentWayPointTraversalOrder;


    public float yPos;
    [SerializeField] private float tiltIntensity;
    [SerializeField] private float tiltSpeed;
    [SerializeField] private SpeedReceiver receiver;
    [SerializeField] private TextMeshProUGUI speedText;

    private void Start()
    {


        switch (currentWayPointTraversalOrder)
        {
            case WaypointTraversalOrder.Ascending:
                wayPointIndex = 0;
                break;
            case WaypointTraversalOrder.Descending:
                wayPointIndex = wayPoints.Length - 1;
                break;
            case WaypointTraversalOrder.Loop:
                wayPointIndex = 0;
                break;
        }

    }

    void Update()
    {

        Vector3 wayPointPosVec = new Vector3(wayPoints[wayPointIndex].transform.position.x, transform.position.y, wayPoints[wayPointIndex].transform.position.z);
        moveSpeed = receiver.speedKph;
        speedText.text = $"{receiver.speedKph}/kmH";

        if (transform.position == wayPointPosVec)
        {

            switch (currentWayPointTraversalOrder)
            {
                case WaypointTraversalOrder.Ascending:
                    wayPointIndex += 1;
                    break;
                case WaypointTraversalOrder.Descending:
                    wayPointIndex -= 1;
                    break;
                case WaypointTraversalOrder.Loop:
                    if (wayPointIndex >= wayPoints.Length - 1)
                    {
                        wayPointIndex = 0;
                    }
                    else
                    {
                        wayPointIndex += 1;
                    }
                    break;
            }
        }

        switch (currentWayPointTraversalOrder)
        {
            case WaypointTraversalOrder.Ascending:
                if (wayPointIndex > wayPoints.Length - 1)
                {
                    currentWayPointTraversalOrder = WaypointTraversalOrder.Descending;
                    wayPointIndex = wayPoints.Length - 1;


                }
                break;
            case WaypointTraversalOrder.Descending:
                if (wayPointIndex < 0)
                {
                    currentWayPointTraversalOrder = WaypointTraversalOrder.Ascending;
                    wayPointIndex = 0;

                }
                break;
        }
        // Calculate the direction towards the target
        Vector3 direction = (wayPointPosVec - transform.position);
        direction.y = 0; // Ignore vertical movement
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // Check if the current rotation is different from the target rotation
        if (transform.rotation != targetRotation)
        {
            Vector3 currentForward = transform.forward;
            Vector3 targetForward = targetRotation * Vector3.forward;

            // Calculate the difference in angle between the current and target rotation
            float angleDifference = Vector3.Angle(currentForward, targetForward);

            // Calculate the direction (left or right) based on the cross product
            Vector3 crossProduct = Vector3.Cross(currentForward, targetForward);
            float tiltDirection = (crossProduct.y > 0) ? -1f : 1f;

            // Apply tilt based on the angle difference and intensity factor
            float tiltAmount = angleDifference * tiltDirection * tiltIntensity; // tiltIntensity is a control variable

            // Smoothly interpolate the Z-axis tilt
            float targetTilt = Mathf.LerpAngle(transform.eulerAngles.z, tiltAmount, tiltSpeed * Time.deltaTime);

            // Smoothly interpolate the rotation
            Quaternion smoothRotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);

            // Apply the smooth rotation and tilt (keeping the Z-axis tilt)
            Vector3 smoothEulerAngles = smoothRotation.eulerAngles;
            smoothEulerAngles.z = targetTilt;

            // Apply the final rotation and position update
            transform.rotation = Quaternion.Euler(smoothEulerAngles);
        }

        // Move towards the waypoint (same as before)
        transform.position = Vector3.MoveTowards(transform.position, new Vector3(wayPoints[wayPointIndex].transform.position.x,
            transform.position.y, wayPoints[wayPointIndex].transform.position.z), moveSpeed * Time.deltaTime);



    }
}
