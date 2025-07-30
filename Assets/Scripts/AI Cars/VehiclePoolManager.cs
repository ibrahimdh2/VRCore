using System.Collections.Generic;
using UnityEngine;

public class VehiclePoolManager : MonoBehaviour
{
    public static VehiclePoolManager Instance;

    [System.Serializable]
    public class CarPool
    {
        public GameObject carPrefab;
        public int poolSize = 10;
    }

    public List<CarPool> carTypes;

    private Dictionary<GameObject, List<GameObject>> pooledCarsByType = new Dictionary<GameObject, List<GameObject>>();
    private List<GameObject> prefabList = new List<GameObject>();

    void Awake()
    {
        Instance = this;
        InitializePool();
    }

    void InitializePool()
    {
        foreach (CarPool pool in carTypes)
        {
            prefabList.Add(pool.carPrefab);

            List<GameObject> carList = new List<GameObject>();

            for (int i = 0; i < pool.poolSize; i++)
            {
                GameObject car = Instantiate(pool.carPrefab);
                car.SetActive(false);
                carList.Add(car);
            }

            pooledCarsByType[pool.carPrefab] = carList;
        }
    }

    public GameObject GetRandomCar()
    {
        int attempts = 0;
        while (attempts < prefabList.Count)
        {
            GameObject randomPrefab = prefabList[Random.Range(0, prefabList.Count)];
            List<GameObject> carList = pooledCarsByType[randomPrefab];

            foreach (GameObject car in carList)
            {
                if (!car.activeInHierarchy)
                {
                    return car;
                }
            }

            attempts++;
        }

        return null; // All cars in all pools are used
    }

    public void ReturnCar(GameObject car)
    {
        car.SetActive(false);
    }
}
