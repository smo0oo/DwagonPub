using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

[RequireComponent(typeof(VisualEffect))]
public class VFXGraphCleaner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("If > 0, the script will force the effect to STOP spawning after this many seconds.")]
    public float duration = 0f;

    private VisualEffect vfx;
    private PooledObject pooledObj;
    private bool stopSignalSent = false;

    void Awake()
    {
        vfx = GetComponent<VisualEffect>();
        // Do NOT cache PooledObject here. It might not exist yet if instantiated by the pooler.
    }

    void OnEnable()
    {
        // --- FIX: Get the component here ---
        // OnEnable runs when the pooler calls SetActive(true), 
        // which is GUARANTEED to happen after AddComponent<PooledObject>().
        if (pooledObj == null)
        {
            pooledObj = GetComponent<PooledObject>();
        }
        // -----------------------------------

        stopSignalSent = false;

        if (vfx != null)
        {
            vfx.Reinit();
            vfx.Play();
        }

        if (duration > 0)
        {
            StartCoroutine(DurationRoutine());
        }
        StartCoroutine(CheckAliveRoutine());
    }

    private IEnumerator DurationRoutine()
    {
        yield return new WaitForSeconds(duration);
        StopAndFade();
    }

    private IEnumerator CheckAliveRoutine()
    {
        yield return new WaitForSeconds(0.1f);

        WaitForSeconds wait = new WaitForSeconds(0.5f);

        while (true)
        {
            if (vfx != null && !stopSignalSent)
            {
                if (vfx.aliveParticleCount == 0)
                {
                    Cleanup();
                    yield break;
                }
            }
            else if (vfx != null && stopSignalSent)
            {
                if (vfx.aliveParticleCount == 0)
                {
                    Cleanup();
                    yield break;
                }
            }
            yield return wait;
        }
    }

    public void StopAndFade()
    {
        if (stopSignalSent) return;

        stopSignalSent = true;
        if (vfx != null)
        {
            vfx.Stop();
        }
    }

    private void Cleanup()
    {
        // Final safety check in case cached reference is somehow missing
        if (pooledObj == null) pooledObj = GetComponent<PooledObject>();

        if (pooledObj != null)
        {
            // Successfully return to pool
            pooledObj.ReturnToPool();
        }
        else
        {
            // If it truly has no pool component, destroy it to prevent errors
            Destroy(gameObject);
        }
    }
}