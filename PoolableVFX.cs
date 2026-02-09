using UnityEngine;
using UnityEngine.VFX; // Required for Visual Effect Graph

[RequireComponent(typeof(VisualEffect))]
public class PoolableVFX : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Time in seconds before this VFX returns to the pool. Match this to your VFX Graph's actual duration.")]
    public float lifetime = 2.0f;

    private VisualEffect _vfx;
    private PooledObject _pooledObj;

    void Awake()
    {
        _vfx = GetComponent<VisualEffect>();
        _pooledObj = GetComponent<PooledObject>();
    }

    void OnEnable()
    {
        if (_vfx != null)
        {
            // Reset the simulation to the beginning
            _vfx.Reinit();
            _vfx.Play();
        }

        // Schedule the return to pool
        Invoke(nameof(ReturnToPool), lifetime);
    }

    void OnDisable()
    {
        CancelInvoke();
        if (_vfx != null) _vfx.Stop();
    }

    private void ReturnToPool()
    {
        if (_pooledObj != null)
        {
            _pooledObj.ReturnToPool();
        }
        else
        {
            gameObject.SetActive(false); // Fallback
        }
    }
}