using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler instance;
    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
    private Transform poolRoot;

    void Awake()
    {
        instance = this;
        GameObject root = new GameObject("--- OBJECT POOL ---");
        poolRoot = root.transform;
    }

    public void CreatePool(GameObject prefab, int count)
    {
        if (prefab == null) return;
        if (!poolDictionary.ContainsKey(prefab)) poolDictionary.Add(prefab, new Queue<GameObject>());

        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            obj.transform.SetParent(poolRoot);
            PooledObject pooled = obj.GetComponent<PooledObject>() ?? obj.AddComponent<PooledObject>();
            pooled.prefab = prefab;
            poolDictionary[prefab].Enqueue(obj);
        }
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        if (!poolDictionary.ContainsKey(prefab)) poolDictionary.Add(prefab, new Queue<GameObject>());

        GameObject objectToSpawn;

        if (poolDictionary[prefab].Count == 0)
        {
            objectToSpawn = Instantiate(prefab, position, rotation);
            PooledObject pooled = objectToSpawn.GetComponent<PooledObject>() ?? objectToSpawn.AddComponent<PooledObject>();
            pooled.prefab = prefab;
        }
        else
        {
            objectToSpawn = poolDictionary[prefab].Dequeue();
            objectToSpawn.transform.SetParent(null); // Detach from pool root immediately
            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;
            // Removed SetActive(true) from here to allow Holder to setup offsets first
        }

        return objectToSpawn;
    }

    public void ReturnToPool(PooledObject pooledObject)
    {
        if (pooledObject == null || pooledObject.prefab == null) return;
        if (!poolDictionary.ContainsKey(pooledObject.prefab)) poolDictionary.Add(pooledObject.prefab, new Queue<GameObject>());

        pooledObject.gameObject.SetActive(false);
        pooledObject.transform.SetParent(poolRoot);
        poolDictionary[pooledObject.prefab].Enqueue(pooledObject.gameObject);
    }
}