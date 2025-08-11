using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.UI;
public class Initializer : MonoBehaviour
{
    public Transform xrRig;
    public Transform bikeChild;
    public SpeedReceiver receiver;
    public Button startButton;
    public GameObject rightController;
    public GameObject leftController;
    

  
    
    public void SetBicycle()
    {
        xrRig.transform.position = bikeChild.position;
        xrRig.rotation = bikeChild.rotation;
        xrRig.SetParent(bikeChild.transform);
        receiver.enabled = true;

    }

}
