using System.Collections;
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    public Transform spawnPointSameDirection;
    public Transform spawnPointOppositeDirection;
    public Transform[] waypointsSame;
    public Transform[] waypointsOpposite;

    [Header("Spawn Settings")]
    [Tooltip("Cars per second (fractional allowed)")]
    public float spawnRatePerSecond = 6f;

    [Tooltip("Local half-extents of the spawn check box (x = width, y = height, z = length)")]
    public Vector3 spawnBoxHalfExtents = new Vector3(1.25f, 1.0f, 2.0f);

    [Tooltip("Extra spacing along spawn forward (min/max)")]
    public float forwardOffsetMin = 1f;
    public float forwardOffsetMax = 3f;

    [Header("Collision/Filter Settings")]
    [Tooltip("Layers that should block a spawn (e.g., Vehicles)")]
    public LayerMask blockMask = ~0; // default: everything
    [Tooltip("Also require hit colliders to have this tag (leave empty to ignore tag filtering)")]
    public string vehicleTag = "Vehicle";
    [Tooltip("Include triggers in blocking checks")]
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Car Limit Settings")]
    public bool limitCars = false;
    public int maxActiveCars = 15;

    void Start()
    {
        StartCoroutine(SpawnCarsRoutine());
    }

    public void SetSpawnRate(float spawnRate)
    {
        // original caller seems to pass 0..60; keep your mapping
        spawnRatePerSecond = spawnRate / 10f;
    }

    IEnumerator SpawnCarsRoutine()
    {
        float timeAccumulator = 0f;

        while (true)
        {
            yield return null; // next frame
            timeAccumulator += Time.deltaTime;

            float carsToSpawn = spawnRatePerSecond * timeAccumulator;
            if (carsToSpawn >= 1f)
            {
                int fullCars = Mathf.FloorToInt(carsToSpawn);
                timeAccumulator -= fullCars / spawnRatePerSecond;

                for (int i = 0; i < fullCars; i++)
                {
                    // Car limit gate
                    if (limitCars && CountActiveCars() >= maxActiveCars)
                        continue;

                    GameObject car = VehiclePoolManager.Instance.GetRandomCar();
                    if (car == null)
                    {
                        Debug.LogWarning("Car pool exhausted.");
                        break;
                    }

                    bool sameDirection = Random.value > 0.5f;
                    Transform spawnPoint = sameDirection ? spawnPointSameDirection : spawnPointOppositeDirection;
                    Transform[] selectedWaypoints = sameDirection ? waypointsSame : waypointsOpposite;

                    if (spawnPoint == null || selectedWaypoints == null || selectedWaypoints.Length == 0)
                    {
                        Debug.LogError("Missing spawn points or waypoints.");
                        VehiclePoolManager.Instance.ReturnCar(car);
                        continue;
                    }

                    // BoxCast-based clearance check (sweeps through the forward offset distance)
                    if (!IsSpawnPathClear(spawnPoint, forwardOffsetMax))
                    {
                        VehiclePoolManager.Instance.ReturnCar(car);
                        continue;
                    }

                    // Add random forward offset to avoid stacking
                    float offset = Random.Range(forwardOffsetMin, forwardOffsetMax);
                    Vector3 spawnPos = spawnPoint.position + spawnPoint.forward * offset;
                    car.transform.SetPositionAndRotation(spawnPos, spawnPoint.rotation);
                    car.SetActive(true);

                    CarMovementController controller = car.GetComponent<CarMovementController>();
                    controller?.SetWaypoints(selectedWaypoints);
                }
            }
        }
    }

    /// <summary>
    /// Checks the path (a box volume) from spawnPoint forward by 'castDistance' to ensure no blocking objects are present.
    /// Uses Physics.BoxCast with the box aligned to the spawnPoint's rotation.
    /// </summary>
    private bool IsSpawnPathClear(Transform spawnPoint, float castDistance)
    {
        // Start slightly centered at spawn, cast forward to cover the offset space where the car will appear
        Vector3 origin = spawnPoint.position;
        Vector3 direction = spawnPoint.forward;

        // We'll collect ALL hits to filter by tag if needed
        RaycastHit[] hits = Physics.BoxCastAll(
            origin,
            spawnBoxHalfExtents,
            direction,
            spawnPoint.rotation,
            castDistance,
            blockMask,
            triggerInteraction
        );

        if (hits == null || hits.Length == 0)
            return true;

        // Optional tag filter: if vehicleTag is empty, any hit blocks. If set, only hits with that tag block.
        if (string.IsNullOrEmpty(vehicleTag))
            return false;

        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.CompareTag(vehicleTag))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Counts currently active cars in the scene.
    /// </summary>
    private int CountActiveCars()
    {
        GameObject[] cars = GameObject.FindGameObjectsWithTag("Vehicle");
        int count = 0;
        foreach (var car in cars)
        {
            if (car.activeInHierarchy)
                count++;
        }
        return count;
    }

    // -----------------
    // Gizmos (boxes)
    // -----------------
    private void OnDrawGizmosSelected()
    {
        // Draw gizmos for both spawn points
        if (spawnPointSameDirection != null)
            DrawSpawnGizmos(spawnPointSameDirection, forwardOffsetMax);

        if (spawnPointOppositeDirection != null)
            DrawSpawnGizmos(spawnPointOppositeDirection, forwardOffsetMax);
    }

    private void DrawSpawnGizmos(Transform spawnPoint, float castDistance)
    {
        // Save and set oriented matrix so cubes align with spawn rotation
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(spawnPoint.position, spawnPoint.rotation, Vector3.one);

        // Start box at origin (local space)
        Gizmos.color = new Color(0f, 1f, 0f, 0.85f);
        Gizmos.DrawWireCube(Vector3.zero, spawnBoxHalfExtents * 2f);

        // End box at the far end of cast
        Vector3 endLocal = Vector3.forward * castDistance;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.85f);
        Gizmos.DrawWireCube(endLocal, spawnBoxHalfExtents * 2f);

        // Mid “path” box just to visualize the sweep path envelope (optional)
        Vector3 midLocal = Vector3.forward * (castDistance * 0.5f);
        Vector3 pathSize = new Vector3(
            spawnBoxHalfExtents.x * 2f,
            spawnBoxHalfExtents.y * 2f,
            castDistance + spawnBoxHalfExtents.z * 2f
        );

        Gizmos.color = new Color(0f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireCube(midLocal, pathSize);

        // Restore matrix
        Gizmos.matrix = oldMatrix;
    }
}
