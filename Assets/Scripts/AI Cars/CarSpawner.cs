using System.Collections;
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    public Transform spawnPointSameDirection;
    public Transform spawnPointOppositeDirection;
    public Transform[] waypointsSame;
    public Transform[] waypointsOpposite;

    [Header("Spawn Settings")]
    public float spawnRatePerSecond = 6f;
    public float spawnClearRadius = 2f; // clearance around spawn point
    public float forwardOffsetMin = 1f; // extra spacing along spawn forward
    public float forwardOffsetMax = 3f;

    void Start()
    {
        StartCoroutine(SpawnCarsRoutine());
    }

    public void SetSpawnRate(float spawnRate)
    {
        spawnRatePerSecond = spawnRate / 10f;
    }

    IEnumerator SpawnCarsRoutine()
    {
        float timeAccumulator = 0f;

        while (true)
        {
            yield return null; // wait for the next frame
            timeAccumulator += Time.deltaTime;

            float carsToSpawn = spawnRatePerSecond * timeAccumulator;

            if (carsToSpawn >= 1f)
            {
                int fullCars = Mathf.FloorToInt(carsToSpawn);
                timeAccumulator -= fullCars / spawnRatePerSecond;

                for (int i = 0; i < fullCars; i++)
                {
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

                    // Check if spawn area is clear
                    if (!IsSpawnAreaClear(spawnPoint.position, spawnClearRadius))
                    {
                        // area blocked, return car to pool
                        VehiclePoolManager.Instance.ReturnCar(car);
                        continue;
                    }

                    // Add a little random forward offset to avoid stacking
                    Vector3 spawnPos = spawnPoint.position + spawnPoint.forward * Random.Range(forwardOffsetMin, forwardOffsetMax);

                    car.transform.position = spawnPos;
                    car.transform.rotation = spawnPoint.rotation;
                    car.SetActive(true);

                    CarMovementController controller = car.GetComponent<CarMovementController>();
                    controller?.SetWaypoints(selectedWaypoints);
                }
            }
        }
    }

    /// <summary>
    /// Checks if the spawn area is clear of vehicles.
    /// </summary>
    private bool IsSpawnAreaClear(Vector3 position, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(position, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Vehicle"))
            { Debug.Log("Car is there don't spawn another"); return false; }
        }
        return true;
    }

    // Optional: Draw spawn clearance gizmo
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (spawnPointSameDirection != null)
            Gizmos.DrawWireSphere(spawnPointSameDirection.position, spawnClearRadius);

        if (spawnPointOppositeDirection != null)
            Gizmos.DrawWireSphere(spawnPointOppositeDirection.position, spawnClearRadius);
    }

}
