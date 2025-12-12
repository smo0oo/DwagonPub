using UnityEngine;
using System.Collections.Generic;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler instance;

    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }
    }

    public void CreatePool(GameObject prefab, int initialSize)
    {
        if (prefab == null || poolDictionary.ContainsKey(prefab))
        {
            return;
        }

        Queue<GameObject> objectPool = new Queue<GameObject>();

        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = Instantiate(prefab, transform);

            // --- MODIFIED BLOCK ---
            // 1. Try to get the component first.
            PooledObject pooledObject = obj.GetComponent<PooledObject>();
            if (pooledObject == null)
            {
                // 2. Add it ONLY if it doesn't exist.
                pooledObject = obj.AddComponent<PooledObject>();
            }

            // 3. ALWAYS set the prefab key on the component we found/added.
            pooledObject.prefabKey = prefab;
            // --- END MODIFIED BLOCK ---

            obj.SetActive(false);
            objectPool.Enqueue(obj);
        }

        poolDictionary.Add(prefab, objectPool);
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("Object Pooler: Attempted to Get a null prefab.");
            return null;
        }

        if (!poolDictionary.ContainsKey(prefab))
        {
            CreatePool(prefab, 1);
        }

        Queue<GameObject> objectPool = poolDictionary[prefab];

        if (objectPool.Count == 0)
        {
            // --- MODIFIED BLOCK (Overflow Object) ---
            GameObject newObj = Instantiate(prefab, transform);

            // 1. Try to get the component first.
            PooledObject pooledObject = newObj.GetComponent<PooledObject>();
            if (pooledObject == null)
            {
                // 2. Add it ONLY if it doesn't exist.
                pooledObject = newObj.AddComponent<PooledObject>();
            }

            // 3. ALWAYS set the prefab key.
            pooledObject.prefabKey = prefab;
            // --- END MODIFIED BLOCK ---

            newObj.transform.position = position;
            newObj.transform.rotation = rotation;
            newObj.SetActive(true);
            return newObj;
        }

        GameObject obj = objectPool.Dequeue();

        if (obj == null)
        {
            return Get(prefab, position, rotation);
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        return obj;
    }

    public void Return(GameObject obj, GameObject prefabKey)
    {
        // This check was the one failing
        if (obj == null || prefabKey == null || !poolDictionary.ContainsKey(prefabKey))
        {
            if (obj != null)
            {
                Debug.LogError($"ObjectPooler: Tried to return an object '{obj.name}' but its prefabKey was null or not in the dictionary. The object will be destroyed.");
                Destroy(obj);
            }
            return;
        }

        obj.SetActive(false);
        poolDictionary[prefabKey].Enqueue(obj);
    }
}