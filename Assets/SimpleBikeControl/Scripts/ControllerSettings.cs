using UnityEngine;
using TMPro;
using KikiNgao.SimpleBikeControl;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
public class ControllerSettings : MonoBehaviour
{
    [SerializeField] private RigidBodyController bikeController;
    [SerializeField] private TextMeshProUGUI turnSensitivityText;
    [SerializeField] private TextMeshProUGUI straightAngleText;
    [SerializeField] private Slider senstivitySlider;
    [SerializeField] private GameObject settingsCanvas;
    [SerializeField] private InputAction settingsMenuInput;
    [SerializeField] private InputAction rotatePitch;
    [SerializeField] private InputAction rotateYaw;
    [SerializeField] private InputAction rotateRoll;
   
    [SerializeField] private TextMeshProUGUI delayUI;
    [SerializeField] private Slider dataDelaySlider;
    [SerializeField] private Transform XRRigParentTransform;

    public float rotationSpeed = 10;

    public bool bothEyes;
    [SerializeField] private DataManager dataManager;

    private void OnEnable()
    {
        settingsMenuInput.performed += EnableSettingsCanvas;
        rotateRoll.Enable();
        rotateYaw.Enable();
        rotatePitch.Enable();
        settingsMenuInput.Enable();

    }

    

    private void EnableSettingsCanvas(InputAction.CallbackContext context)
    {
        settingsCanvas.SetActive(!settingsCanvas.activeSelf);
        Vector3 eulers = XRRigParentTransform.rotation.eulerAngles;
     
    }

    private void OnDisable()
    {

        settingsMenuInput.performed -= EnableSettingsCanvas;
        rotateRoll.Disable();
        rotateYaw.Disable();
        rotatePitch.Disable();
        settingsMenuInput.Disable();
        settingsMenuInput.performed -= EnableSettingsCanvas;
        settingsMenuInput.Disable();
    }

    void Start()
    {
        if (bothEyes)
        {
            XRSettings.showDeviceView = true; // ensure VR is mirrored
            XRSettings.gameViewRenderMode = GameViewRenderMode.BothEyes; 
        }
        else
        {
           
            XRSettings.gameViewRenderMode = GameViewRenderMode.LeftEye;
        }

        if (bikeController == null)
        {
            bikeController = GetComponent<RigidBodyController>();
        }
        if(dataManager == null)
        {
            dataManager = GameObject.FindAnyObjectByType<DataManager>();
        }
        LoadSettings();
    }

    void Update()
    {
        // Continuous input reading
        float pitchInput = rotatePitch.ReadValue<float>();
        float yawInput = rotateYaw.ReadValue<float>();
        float rollInput = rotateRoll.ReadValue<float>();

        rotX += pitchInput * rotationSpeed * Time.deltaTime;
        rotY += yawInput * rotationSpeed * Time.deltaTime;
        rotZ += rollInput * rotationSpeed * Time.deltaTime;

        UpdateRotation();
    }
    public void LoadSettings()
    {
        Debug.Log("Settings Loaded");

        senstivitySlider.value = bikeController.turnSensitivity = PlayerPrefs.GetFloat("TurnSensitivity", 0.1485f);
        turnSensitivityText.text = senstivitySlider.value.ToString("f2");
        bikeController.straightAngle = PlayerPrefs.GetFloat("StraightAngle", 0);

        // Load saved Euler angles
        Vector3 eulers = new Vector3(
            PlayerPrefs.GetFloat("XRotation", 0),
            PlayerPrefs.GetFloat("YRotation", 90),
            PlayerPrefs.GetFloat("ZRotation", 0)
        );

        // Apply to transform
        XRRigParentTransform.rotation = Quaternion.Euler(eulers);

        // Keep internal state in sync
        rotX = eulers.x;
        rotY = eulers.y;
        rotZ = eulers.z;

        delayUI.text = dataManager.delay.ToString("f2");
    }

    public void SaveSettings()
    {
        Debug.Log("Settings Saved");
        PlayerPrefs.SetFloat("TurnSensitivity", senstivitySlider.value);
        PlayerPrefs.SetFloat("StraightAngle", bikeController.straightAngle);
        PlayerPrefs.SetFloat("Delay", dataManager.delay);
        Vector3 eulers = XRRigParentTransform.rotation.eulerAngles;
        PlayerPrefs.SetFloat("XRotation", eulers.x);
        PlayerPrefs.SetFloat("YRotation", eulers.y);
        PlayerPrefs.SetFloat("ZRotation", eulers.z);


    }
    public void ResetSettings()
    {
        PlayerPrefs.DeleteAll();
        LoadSettings();
        Debug.Log("Settings Reset");

    }
    public void AdjustSensitivity(float value)
    {
      bikeController.turnSensitivity = value;
        turnSensitivityText.text = value.ToString("f4");
    }
    public void AdjustDeadZone(float value)
    {
       // bikeController.turnDeadZone = value;
    }
    public void SetStraightAngle()
    {
        bikeController.straightAngle = bikeController.CurrentAngleRaw;

        straightAngleText.text = bikeController.straightAngle.ToString("f2");

    }
    public void Close()
    {

    }
    public void ReloadLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void DelaySlider(float v)
    {
        dataManager.delay = v;
        delayUI.text = v.ToString("f4");
    }
    public void SetFasterTheSpeedSlowerTheTurn(bool isOn)
    {
        //bikeController.fasterTheSpeedSlowerTheTurn = isOn;

    }
    private float rotX, rotY, rotZ;

    

    private void UpdateRotation()
    {
        XRRigParentTransform.rotation = Quaternion.Euler(rotX, rotY, rotZ);
    }

}
