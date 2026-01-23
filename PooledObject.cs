using UnityEngine;

public class PooledObject : MonoBehaviour
{
    public GameObject prefab;

    public void ReturnToPool()
    {
        if (ObjectPooler.instance != null)
        {
            // AAA FIX: Crucial for parented VFX. 
            // Detaches from bones so the object isn't lost if the character is destroyed.
            transform.SetParent(null);

            ObjectPooler.instance.ReturnToPool(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}