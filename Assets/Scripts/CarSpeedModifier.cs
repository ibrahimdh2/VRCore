using UnityEngine;

public class CarSpeedModifier : MonoBehaviour
{
    public float speedToModify;
    public float rotateSpeed;
    public bool allowRotateSpeedModification;
    private void OnTriggerEnter(Collider other)
    {
       // Debug.Log("Speed Modification ");
        if (other.CompareTag("Vehicle"))
        {
            if (other.TryGetComponent<CarMovementController>(out CarMovementController controller))
            {
                controller.moveSpeed = speedToModify;
                if(allowRotateSpeedModification)
                {
                    controller.rotateSpeed = rotateSpeed;
                }
            }
        }
    }
}
