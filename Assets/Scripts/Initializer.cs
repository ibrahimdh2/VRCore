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


    public bool optimized;
    public bool limitTraffic;
    public Image optimizeBtnSprite;
    public Image limitTrafficBtnSprite;
    public Image renderTextureCameraBtnSprite;
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
            //renderTextureCameraBtnSprite.color = Color.green;
        }

    }
    public void SeatCameraOnOff()
    {
        bool inverse = !secondaryCamera.activeSelf;
        secondaryCamera.SetActive(inverse);
        rawImage.SetActive(inverse);
        renderTextureCameraBtnSprite.color = inverse ? Color.green : Color.white;
    }
    [ContextMenu("Optimize")]
    public void EnableDisableObjects()
    {

        for (int i = 0; i < gameObjectsToDisable.Count; i++)
        {
            GameObject g = gameObjectsToDisable[i];
            
            g.SetActive(!g.activeSelf);

        }
        optimized = !((gameObjectsToDisable[0].activeSelf) == true);
        optimizeBtnSprite.color = optimized ? Color.green : Color.white;
            

    }
    public void EnableDisableObjects(bool b)
    {
        for (int i = 0; i < gameObjectsToDisable.Count; i++)
        {
            GameObject g = gameObjectsToDisable[i];
            g.SetActive(b);
        }
        optimized = b;
        optimizeBtnSprite.color = optimized ? Color.green : Color.white;

    }
    public void LimitTraffic()
    {
            foreach (CarSpawner spawner in spawners)
            {
                spawner.maxActiveCars = carSpawnLimit;
               
                spawner.limitCars = !spawner.limitCars;
            }
        limitTraffic = (spawners[0].limitCars);
        limitTrafficBtnSprite.color = limitTraffic ? Color.green : Color.white;
    }
    public void LimitTraffic(bool b)
    {
        foreach(CarSpawner spawner in spawners)
        {
            spawner.maxActiveCars = carSpawnLimit;
            spawner.limitCars = b;
        }
        limitTraffic = b;
        limitTrafficBtnSprite.color = limitTraffic ? Color.green : Color.white;

    }

}
