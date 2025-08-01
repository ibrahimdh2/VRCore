using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
public class Initializer : MonoBehaviour
{
    public Transform xrRig;
    public Transform bikeChild;
    public SpeedReceiver receiver;
    public InputAction action;
    public Button startButton;
    public GameObject rightController;
    public GameObject leftController;


    public List<GameObject> bikes = new();
    public List<Transform> rigTransform = new();
    public int activeIndex;

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
    [ContextMenu("SetBicycle")]
    public void SetBicycle()
    {
        for (int i = 0; i < bikes.Count; i++)
        {
            if (i == activeIndex)
            {
                GameObject g = bikes[i];
                g.SetActive(true);
                g.GetComponent<SpeedReceiver>().enabled = true;
                bikeChild= rigTransform[i];
                Debug.Log($"{xrRig.name} - {rigTransform[i].name}");

            }

        }
        xrRig.transform.position = bikeChild.position;
        xrRig.rotation = bikeChild.rotation;
        xrRig.SetParent(bikeChild.transform);
        receiver.enabled = true;

    }
    [ContextMenu("Reload")]
    public void Reload()
    {
        var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(s.name);
    }

}
