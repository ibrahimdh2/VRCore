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
    public InputAction action;
    public Button startButton;
    public GameObject rightController;
    public GameObject leftController;
    private void OnEnable()
    {
        action.performed += StartGame;
        action.Enable();
    }

    private void StartGame(InputAction.CallbackContext obj)
    {
        startButton.onClick.Invoke();
    }
  
    private void OnDisable()
    {
        action.performed -= StartGame;
        action.Disable();
    }
    public void SetBicycle()
    {
        xrRig.transform.position = bikeChild.position;
        xrRig.rotation = bikeChild.rotation;
        xrRig.SetParent(bikeChild.transform);
        receiver.enabled = true;

    }

}
