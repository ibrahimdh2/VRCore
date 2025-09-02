using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
public class Initializer : MonoBehaviour
{
    public Transform xrRig;
    public Transform bikeChild;
    public SpeedReceiver receiver;
    public Button startButton;
    public GameObject rightController;
    public GameObject leftController;
    public GameObject secondaryCamera;
    public GameObject rawImage;
    public bool turnRenderTextureOn;

    public List<GameObject> gameObjectsToDisable = new List<GameObject>();
    public List<CarSpawner> spawners = new List<CarSpawner>();
    public int carSpawnLimit;
    public void SetBicycle()
    {
        xrRig.transform.position = bikeChild.position;
        xrRig.rotation = bikeChild.rotation;
        xrRig.SetParent(bikeChild.transform);
        receiver.enabled = true;
        if (turnRenderTextureOn)
        {
            secondaryCamera.SetActive(true);
            rawImage.SetActive(true);
        }

    }
    public void SeatCameraOnOff()
    {
        bool inverse = !secondaryCamera.activeSelf;
        secondaryCamera.SetActive(inverse);
        rawImage.SetActive(inverse);
    }
    [ContextMenu("Optimize")]
    public void EnableDisableObjects()
    {

        for (int i = 0; i < gameObjectsToDisable.Count; i++)
        {
            GameObject g = gameObjectsToDisable[i];
            g.SetActive(!g.activeSelf);
        }
        
    }
    public void LimitTraffic()
    {
            foreach (CarSpawner spawner in spawners)
            {
                spawner.maxActiveCars = carSpawnLimit;
                spawner.limitCars = !spawner.limitCars;
            }
    }

}
