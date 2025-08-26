using KikiNgao.SimpleBikeControl;
using UnityEngine;

public class StopWhenBikeNear : MonoBehaviour
{
    public Transform bike;
    public SpeedReceiver speedReceiver;
    public float allowedDistance;

    private void Start()
    {
        bike ??= FindAnyObjectByType<SimpleBike>().transform;
    }
    void Update()
    {
        
    }
}
