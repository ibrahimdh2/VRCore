using UnityEngine;
using TMPro;
using KikiNgao.SimpleBikeControl;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using UnityEngine.SceneManagement;
public class ControllerSettings : MonoBehaviour
{
    [SerializeField] private SimpleBike bikeController;
    [SerializeField] private TextMeshProUGUI inputTurnAngleText;
    [SerializeField] private TextMeshProUGUI finalTurnAngleText;
    [SerializeField] private TextMeshProUGUI turnSensitivityText;
    [SerializeField] private TextMeshProUGUI straightAngleText;
    [SerializeField] private Slider senstivitySlider;
    [SerializeField] private GameObject settingsCanvas;
    [SerializeField] private InputAction settingsMenuInput;
    [SerializeField] private TextMeshProUGUI delayUI;
    [SerializeField] private Slider dataDelaySlider;
    

    [SerializeField] private DataManager dataManager;

    private void OnEnable()
    {
        settingsMenuInput.performed += EnableSettingsCanvas;
        settingsMenuInput.Enable();

    }

    private void EnableSettingsCanvas(InputAction.CallbackContext context)
    {
        settingsCanvas.SetActive(!settingsCanvas.activeSelf);
    }

    private void OnDisable()
    {
        settingsMenuInput.performed -= EnableSettingsCanvas;
        settingsMenuInput.Disable();
    }

    void Start()
    {
        if(bikeController == null)
        {
            bikeController = GetComponent<SimpleBike>();
        }
        if(dataManager == null)
        {
            dataManager = GameObject.FindAnyObjectByType<DataManager>();
        }
        LoadSettings();
    }

    void Update()
    {
        finalTurnAngleText.text = bikeController.frontWheelCollider.steerAngle.ToString();
        inputTurnAngleText.text = bikeController.calculatedTurnAngle.ToString();    
    }
    public void LoadSettings()
    {
        Debug.Log("Settings Loaded");
       senstivitySlider.value = bikeController.turnSensitivity = PlayerPrefs.GetFloat("TurnSensitivity", 1);
        turnSensitivityText.text = senstivitySlider.value.ToString("f2");
       // turnDeadZone.value = bikeController.turnDeadZone = PlayerPrefs.GetFloat("TurnDeadzone", 3);
        bikeController.straightAngle = PlayerPrefs.GetFloat("StraightAngle", 0);
        dataDelaySlider.value =  dataManager.delay = PlayerPrefs.GetFloat("Delay", 5);
        delayUI.text = dataManager.delay.ToString("f2");

    }
    public void SaveSettings()
    {
        Debug.Log("Settings Saved");
        PlayerPrefs.SetFloat("TurnSensitivity", senstivitySlider.value);
        PlayerPrefs.SetFloat("StraightAngle", bikeController.straightAngle);
        PlayerPrefs.SetFloat("Delay", dataManager.delay);


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
        bikeController.turnDeadZone = value;
    }
    public void SetStraightAngle()
    {
        bikeController.straightAngle = bikeController.calculatedTurnAngle;
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
}
