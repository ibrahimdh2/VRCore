using System.Collections;
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    public Transform spawnPointSameDirection;
    public Transform spawnPointOppositeDirection;
    public Transform[] waypointsSame;
    public Transform[] waypointsOpposite;

    public float spawnRatePerSecond = 6;

    void Start()
    {
        StartCoroutine(SpawnCarsRoutine());
    }
    public void SetSpawnRate(float spawnRate)
    {
        spawnRatePerSecond = spawnRate/10f;
    }
    IEnumerator SpawnCarsRoutine()
    {
        float timeAccumulator = 0f;

        while (true)
        {
            yield return null; // Wait for the next frame
            timeAccumulator += Time.deltaTime;

            float carsToSpawn = spawnRatePerSecond * timeAccumulator;

            if (carsToSpawn >= 1f)
            {
                int fullCars = Mathf.FloorToInt(carsToSpawn);
                timeAccumulator -= fullCars / spawnRatePerSecond; // subtract time used for spawned cars

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

                    car.transform.position = spawnPoint.position;
                    car.transform.rotation = spawnPoint.rotation;
                    car.SetActive(true);

                    CarMovementController controller = car.GetComponent<CarMovementController>();
                    controller?.SetWaypoints(selectedWaypoints);
                }
            }
        }
    }


}
