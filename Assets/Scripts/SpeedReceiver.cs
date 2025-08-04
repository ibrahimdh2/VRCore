using System;
using System.Threading;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Collections;

public class SpeedReceiver : MonoBehaviour
{
    private Thread receiveThread;
    private bool running = true;
    private float latestSpeed = 0f;
    public float speedKph => latestSpeed;
    public float speedReducer;

    // Keep a reference to the socket so we can terminate it from outside
    private SubscriberSocket subSocket;


    void Start()
    {
        StartCoroutine(StartProcesses());
        
    }


    public IEnumerator StartProcesses()
    {
        //process = new Process();
        //process.StartInfo = new ProcessStartInfo() {FileName=$"{Application.streamingAssetsPath}/speedreceiver.exe" };
        //process.Start();
        yield return new WaitForSeconds(5);
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    
    void ReceiveData()
    {
        AsyncIO.ForceDotNet.Force(); // Required for Unity
        try
        {
            using (subSocket = new SubscriberSocket())
            {
                subSocket.Options.ReceiveHighWatermark = 1000;
                subSocket.Connect("tcp://127.0.0.1:5555");
                subSocket.Subscribe(""); // Subscribe to all messages
                UnityEngine.Debug.Log("[ZMQ] Subscribed to tcp://127.0.0.1:5555");

                while (running)
                {
                    try
                    {
                        string message = subSocket.ReceiveFrameString(); // blocks here
                        if (float.TryParse(message, out float speed))
                        {
                            latestSpeed = speed;
                            UnityEngine.Debug.Log($"[ZMQ] Received speed: {speed} km/h");
                        }
                        
                    }
                    catch (TerminatingException)
                    {
                        UnityEngine.Debug.LogWarning("[ZMQ] Socket was terminated.");
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[ZMQ] Exception: {e.Message}");
        }

        NetMQConfig.Cleanup();
    }

    void OnApplicationQuit()
    {
        running = false;

        // Force socket to exit blocking receive
        try
        {
            subSocket?.Close();
            subSocket?.Dispose();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[ZMQ] Exception during socket close: {e.Message}");
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000); // give it a second to shut down cleanly
        }

        NetMQConfig.Cleanup();
    }
}
