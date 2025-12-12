using UnityEngine;
using System.Collections.Generic;

// Helper class for the Inspector
[System.Serializable]
public class PoolDefinition
{
    public GameObject prefab;
    public int initialSize;
}

public class PoolWarmer : MonoBehaviour
{
    public List<PoolDefinition> poolsToCreate;

    void Start()
    {
        if (ObjectPooler.instance == null)
        {
            Debug.LogError("PoolWarmer: ObjectPooler instance not found!");
            return;
        }

        foreach (var pool in poolsToCreate)
        {
            if (pool.prefab != null)
            {
                ObjectPooler.instance.CreatePool(pool.prefab, pool.initialSize);
            }
        }
    }
}