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
        // DO NOT search for PooledObject here. 
        // During the very first instantiation, ObjectPooler adds the component AFTER Awake runs.
    }

    void OnEnable()
    {
        // --- AAA FIX: Get the component here ---
        // OnEnable runs when the pooler calls SetActive(true).
        // By this time, the Pooler has definitely finished adding the component.
        if (pooledObj == null)
        {
            pooledObj = GetComponent<PooledObject>();
        }

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
        // Give the VFX a frame to actually spawn particles
        yield return new WaitForSeconds(0.1f);

        WaitForSeconds wait = new WaitForSeconds(0.5f);

        while (true)
        {
            if (vfx != null)
            {
                // If particles are finished and we haven't cleaned up yet
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
        // Final safety check: If we somehow still don't have the reference, try one last time.
        if (pooledObj == null) pooledObj = GetComponent<PooledObject>();

        if (pooledObj != null)
        {
            // Return to pool instead of destroying
            pooledObj.ReturnToPool();
        }
        else
        {
            // Only destroy if this object was manually placed in the scene and NOT pooled.
            Destroy(gameObject);
        }
    }
}