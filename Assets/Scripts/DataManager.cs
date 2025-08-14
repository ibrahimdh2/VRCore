using UnityEngine;
using ClosedXML.Excel;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using UnityEngine.InputSystem;
using KikiNgao.SimpleBikeControl;

public class DataManager : MonoBehaviour
{
    public OrderedDictionary orderedDictionary = new();
    public SpeedReceiver speedReceiver;
    public SimpleBike bikeController;
    private float lastRecordedTime;
    public float delay = 1f;
    public bool startRecording;

    [SerializeField] private InputAction startRecord;
    [SerializeField] private GameObject recordingCanvas;

    private void OnEnable()
    {
        startRecord.performed += StartStopRecording;
        startRecord.Enable();
        
    }
    private void OnDisable()
    {
        startRecord.performed -= StartStopRecording;
        startRecord.Disable();
    }

    public void StartStopRecording(InputAction.CallbackContext context)
    {
        StartStopRecording();
    }

    public void StartRecording()
    {
        startRecording = true;
    }
    public void StopRecording()
    {
        startRecording = false;
        WriteExcelFile();

    }

    public void StartStopRecording()
    {
        if(startRecording)
        {
            recordingCanvas.SetActive(false);
            StopRecording();
        }
        else
        {
            recordingCanvas.SetActive(true);
            StartRecording();
        }
    }

    void Update()
    {
        if (startRecording)
        {
            if (Time.time > lastRecordedTime + delay)
            {
                string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                float speed = speedReceiver.speedKph;

                // Fix: Use timeStamp as key more safely - timestamps should be unique enough
                if (!orderedDictionary.Contains(timeStamp))
                {
                    orderedDictionary.Add(timeStamp, (sensorSpeed: speed, bikeSpeed: bikeController.GetBicycleVelocity()));
                    lastRecordedTime = Time.time;
                    Debug.Log($"Recorded: {timeStamp} - {speed} KPH"); // Debug logging
                }
            } 
        }
    }

    private void OnApplicationQuit()
    {
        if(startRecording)
        {
            StopRecording();
        }
    }

    [ContextMenu("Write Excel File")]
    public void WriteExcelFile()
    {
        try
        {
            if (orderedDictionary.Count == 0)
            {
                Debug.LogWarning("No data to save!");
                return;
            }

            string fileName = $"CycleData_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx";
            string path = Path.Combine(Application.persistentDataPath, fileName);

            Debug.Log($"Attempting to save to: {path}");
            Debug.Log($"Directory exists: {Directory.Exists(Application.persistentDataPath)}");

            // Ensure directory exists
            Directory.CreateDirectory(Application.persistentDataPath);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Cycle Data");

                // Headers
                worksheet.Cell(1, 1).Value = "Time";
                worksheet.Cell(1, 2).Value = "Sensor Speed (KPH)";
                worksheet.Cell(1, 3).Value = "In-game Speed (KPH)";

                int row = 2;
                foreach (DictionaryEntry entry in orderedDictionary)
                {
                    worksheet.Cell(row, 1).Value = entry.Key.ToString();

                    var speeds = (ValueTuple<float, float>)entry.Value;



                    worksheet.Cell(row, 2).Value = Convert.ToSingle(speeds.Item1);
                    worksheet.Cell(row, 3).Value = Convert.ToSingle(speeds.Item2);
                    row++;
                }

                workbook.SaveAs(path);
            }

            Debug.Log($"Excel file saved successfully to: {path}");
            Debug.Log($"Total records saved: {orderedDictionary.Count}");

            // Fixed explorer opening - use proper arguments
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = Application.persistentDataPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to open explorer: {e.Message}");
                // Alternative method for opening folder
                Application.OpenURL("file://" + Application.persistentDataPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save Excel file: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
    }

    // Add this method to test if data is being collected
    [ContextMenu("Debug Data Count")]
    public void DebugDataCount()
    {
        Debug.Log($"Current data entries: {orderedDictionary.Count}");
        if (orderedDictionary.Count > 0)
        {
            Debug.Log("Sample entries:");
            int count = 0;
            foreach (DictionaryEntry entry in orderedDictionary)
            {
                Debug.Log($"{entry.Key}: {entry.Value}");
                count++;
                if (count >= 5) break; // Show first 5 entries
            }
        }
    }
}