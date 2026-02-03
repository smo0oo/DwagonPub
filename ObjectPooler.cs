using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // Required for the automation to work
#endif

public class ObjectPooler : MonoBehaviour
{
    [System.Serializable]
    public class Pool
    {
        public string tag; // For organization in the Inspector
        public GameObject prefab;
        public int size;
    }

    public static ObjectPooler instance;

    [Header("Pre-Warmer")]
    [Tooltip("This list is populated manually or via the Auto-Fill button below.")]
    public List<Pool> pools;

    [Header("Editor Automation")]
    [Tooltip("The path to scan for prefabs. Example: Assets/Prefabs/Projectiles")]
    public string autoFillFolderPath = "Assets/Prefabs/Projectiles";
    public int defaultPoolSize = 20;

    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
    private Transform poolRoot;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        GameObject root = new GameObject("--- OBJECT POOL ---");
        poolRoot = root.transform;

        // --- NEW: Initialize pools defined in the Inspector list ---
        if (pools != null)
        {
            foreach (Pool pool in pools)
            {
                CreatePool(pool.prefab, pool.size);
            }
        }
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
            // Removed SetActive(true) to allow Holder to setup offsets first
        }

        return objectToSpawn;
    }

    public void ReturnToPool(PooledObject pooledObject)
    {
        if (pooledObject == null || pooledObject.prefab == null) return;

        // Ensure dictionary key exists (safety check)
        if (!poolDictionary.ContainsKey(pooledObject.prefab))
            poolDictionary.Add(pooledObject.prefab, new Queue<GameObject>());

        pooledObject.gameObject.SetActive(false);
        pooledObject.transform.SetParent(poolRoot);
        poolDictionary[pooledObject.prefab].Enqueue(pooledObject.gameObject);
    }

    // --- EDITOR AUTOMATION CODE ---
    // This code only exists in the Unity Editor and is stripped from the final game build.
#if UNITY_EDITOR
    [ContextMenu("Auto-Fill From Folder")]
    public void AutoFillPools()
    {
        // 1. Find all GameObjects in the specified folder
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { autoFillFolderPath });
        
        if (pools == null) pools = new List<Pool>();

        int newCount = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab != null)
            {
                // 2. Check if we already have this pool (to avoid duplicates)
                bool alreadyExists = false;
                foreach (var existingPool in pools)
                {
                    if (existingPool.prefab == prefab)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                // 3. Add if new
                if (!alreadyExists)
                {
                    Pool newPool = new Pool
                    {
                        tag = prefab.name,
                        prefab = prefab,
                        size = defaultPoolSize
                    };
                    pools.Add(newPool);
                    newCount++;
                }
            }
        }

        Debug.Log($"<color=cyan>ObjectPooler:</color> Auto-filled {newCount} new pools from '{autoFillFolderPath}'. Total pools: {pools.Count}");
        
        // Mark object as dirty so Unity saves the changes to the scene/prefab
        EditorUtility.SetDirty(this);
    }
    
    [ContextMenu("Clear All Pools")]
    public void ClearPools()
    {
        pools.Clear();
        EditorUtility.SetDirty(this);
        Debug.Log("ObjectPooler: Cleared all pools.");
    }
#endif
}