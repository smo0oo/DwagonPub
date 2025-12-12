using UnityEngine;

/// <summary>
/// A helper component attached to all pooled objects.
/// It knows its "prefab key" and how to return itself to the pool.
/// </summary>
public class PooledObject : MonoBehaviour
{
    // This will be assigned by the ObjectPooler when the object is created.
    public GameObject prefabKey;

    /// <summary>
    /// Returns this object to the ObjectPooler.
    /// </summary>
    public void ReturnToPool()
    {
        if (ObjectPooler.instance != null)
        {
            ObjectPooler.instance.Return(this.gameObject, prefabKey);
        }
        else
        {
            // Failsafe in case the pooler is gone for some reason
            Destroy(gameObject);
        }
    }
}