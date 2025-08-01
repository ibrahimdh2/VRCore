using System;
using System.Collections.Concurrent;
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

    private SubscriberSocket subSocket;

    private float lastReceivedTime = -1f;

    [Header("Speed Settings")]
    public float speedTimeout = 2f;           // Seconds before decay
    public float decelerationRate = 5f;       // km/h per second decay rate

    private ConcurrentQueue<float> speedQueue = new ConcurrentQueue<float>();

    public bool simulate;
    [SerializeField]private float simulateSpeed;

    void Start()
    {
        StartCoroutine(StartProcesses());
#if !UNITY_EDITOR
        simulate = false;
#else
        simulate = true;
#endif
    }

    public IEnumerator StartProcesses()
    {
        yield return new WaitForSeconds(5);
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void Update()
    {
        if(simulate)
        {
            latestSpeed = simulateSpeed;
            return;
        }
        // Apply any new speed received from the thread
        while (speedQueue.TryDequeue(out float receivedSpeed))
        {
            latestSpeed = receivedSpeed;
            lastReceivedTime = Time.time;
            if(simulate)
            {
                return;
            }
            Debug.Log($"[ZMQ] Received speed on main thread: {latestSpeed:F2} km/h");
        }

        float timeSinceLast = Time.time - lastReceivedTime;

        if (timeSinceLast > speedTimeout && lastReceivedTime > 0)
        {
            if (latestSpeed > 0f)
            {
                latestSpeed = Mathf.Max(0f, latestSpeed - decelerationRate * Time.deltaTime);
                Debug.Log($"[ZMQ] Decaying speed: {latestSpeed:F2} km/h");
            }
        }
    }

    void ReceiveData()
    {
        AsyncIO.ForceDotNet.Force();

        try
        {
            using (subSocket = new SubscriberSocket())
            {
                subSocket.Options.ReceiveHighWatermark = 1000;
                subSocket.Connect("tcp://127.0.0.1:5555");
                subSocket.Subscribe("");
                Debug.Log("[ZMQ] Subscribed to tcp://127.0.0.1:5555");

                while (running)
                {
                    try
                    {
                        string message = subSocket.ReceiveFrameString();

                        if (float.TryParse(message, out float speed))
                        {
                            speedQueue.Enqueue(speed); // Safe pass to Unity main thread
                        }
                    }
                    catch (TerminatingException)
                    {
                        Debug.LogWarning("[ZMQ] Socket was terminated.");
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ZMQ] Exception: {e.Message}");
        }

        NetMQConfig.Cleanup();
    }

    void OnApplicationQuit()
    {
        running = false;

        try
        {
            subSocket?.Close();
            subSocket?.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ZMQ] Exception during socket close: {e.Message}");
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000);
        }

        NetMQConfig.Cleanup();
    }
}
