using UnityEngine;
using PixelCrushers.DialogueSystem;

public class ConditionalSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject prefabToSpawn;
    public Transform spawnLocation;

    [Header("Conditions")]
    [Tooltip("If true, this spawner runs automatically on Start.")]
    public bool spawnOnStart = true;

    [Tooltip("Lua condition. If TRUE, the object spawns.")]
    [TextArea(2, 3)]
    public string luaCondition;

    [Tooltip("If true, this spawner destroys itself after spawning (to prevent duplicates if the prefab handles its own persistence).")]
    public bool destroyAfterSpawn = true;

    private void Start()
    {
        if (spawnOnStart)
        {
            CheckAndSpawn();
        }
    }

    public void CheckAndSpawn()
    {
        if (prefabToSpawn == null) return;

        // 1. Check Condition
        if (!string.IsNullOrEmpty(luaCondition))
        {
            if (!Lua.IsTrue(luaCondition)) return; // Condition failed
        }

        // 2. Spawn
        Vector3 pos = spawnLocation != null ? spawnLocation.position : transform.position;
        Quaternion rot = spawnLocation != null ? spawnLocation.rotation : transform.rotation;

        GameObject instance = Instantiate(prefabToSpawn, pos, rot);

        // 3. Optional: Register parent if needed
        // instance.transform.SetParent(transform); 

        if (destroyAfterSpawn)
        {
            Destroy(gameObject);
        }
    }
}