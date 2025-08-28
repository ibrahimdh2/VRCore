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
    [SerializeField] private InputAction forwardBack;
    [SerializeField] private InputAction leftRight;
    [SerializeField] private InputAction upDown;
   
    [SerializeField] private TextMeshProUGUI delayUI;
    [SerializeField] private Slider dataDelaySlider;
    [SerializeField] private Transform XRRigParentTransform;

    public float rotationSpeed = 10;

    public bool bothEyes;
    [SerializeField] private DataManager dataManager;
    public bool allowRotationSetting;

    private void OnEnable()
    {
        settingsMenuInput.performed += EnableSettingsCanvas;
        rotateRoll.Enable();
        upDown.Enable();
        leftRight.Enable();
        forwardBack.Enable();
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
        leftRight.Disable();
        forwardBack.Disable();
        rotatePitch.Disable();
        upDown.Disable();
        settingsMenuInput.Disable();
        settingsMenuInput.performed -= EnableSettingsCanvas;
        settingsMenuInput.Disable();
    }

    void Start()
    {
        if (bothEyes)
        {
            XRSettings.showDeviceView = true; // ensure VR is mirrored
            XRSettings.gameViewRenderMode = GameViewRenderMode.RightEye; 
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
        if (allowRotationSetting)
        {
            // --- rotation stuff ---
            float pitchInput = rotatePitch.ReadValue<float>();
            float yawInput = rotateYaw.ReadValue<float>();
            float rollInput = rotateRoll.ReadValue<float>();

            if (pitchInput + yawInput + rollInput != 0)
            {
                rotX += pitchInput * rotationSpeed * Time.deltaTime;
                rotY += yawInput * rotationSpeed * Time.deltaTime;
                rotZ += rollInput * rotationSpeed * Time.deltaTime;
                UpdateRotation();
            }

            // --- movement stuff ---
            float forwardInput = forwardBack.ReadValue<float>();
            float strafeInput = leftRight.ReadValue<float>();
            float yMovement = upDown.ReadValue<float>();

            // Determine dominant axis of forward vector (X or Z)
            Vector3 forward = XRRigParentTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 moveDir = Vector3.zero;

            if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
            {
                // Facing more along world X → forward/back uses X, strafe uses Z
                moveDir = new Vector3(forwardInput * Mathf.Sign(forward.x),
                                      yMovement,
                                      strafeInput);
            }
            else
            {
                // Facing more along world Z → forward/back uses Z, strafe uses X
                moveDir = new Vector3(strafeInput,
                                      yMovement,
                                      forwardInput * Mathf.Sign(forward.z));
            }

            // 🔹 Fix strafe inversion: flip depending on forward sign
            if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
            {
                // If facing -X, flip strafe so right stays right
                moveDir.z = strafeInput * Mathf.Sign(forward.x);
            }
            else
            {
                // If facing -Z, flip strafe so right stays right
                moveDir.x = strafeInput * Mathf.Sign(forward.z);
            }

            if (moveDir != Vector3.zero)
            {
                XRRigParentTransform.position += moveDir * (rotationSpeed / 10f) * Time.deltaTime;
            } 
        }
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
        XRRigParentTransform.localRotation = Quaternion.Euler(eulers);

        // Keep internal state in sync
        rotX = eulers.x;
        rotY = eulers.y;
        rotZ = eulers.z;
        XRRigParentTransform.localPosition = new Vector3(PlayerPrefs.GetFloat("XPosition", 0.1602979f), PlayerPrefs.GetFloat("YPosition", 1.990758f), PlayerPrefs.GetFloat("ZPosition", 0.0114429f));
        Debug.Log($"XR Parent position set {XRRigParentTransform.position}");
        Debug.Log($"XR Parent rotation set {XRRigParentTransform.rotation.eulerAngles}");
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
        PlayerPrefs.SetFloat("XPosition", XRRigParentTransform.position.x);
        PlayerPrefs.SetFloat("ZPosition", XRRigParentTransform.position.z);
        PlayerPrefs.SetFloat("YPosition", XRRigParentTransform.position.y);
        


    }
    public void ResetSettings()
    {
        PlayerPrefs.DeleteAll();

        // Apply your intended reset defaults
        XRRigParentTransform.position = new Vector3(0.1476246f, 1.951878f, 0.0114379f);
        XRRigParentTransform.rotation = Quaternion.Euler(0, 90, 0);

        rotX = 0;
        rotY = 90;
        rotZ = 0;


        LoadSettings();   // now reload them into the system

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
