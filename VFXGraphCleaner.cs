using UnityEngine;
using UnityEngine.VFX;
using System.Collections; // Required for IEnumerator

[RequireComponent(typeof(VisualEffect))]
public class VFXGraphCleaner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("If > 0, the script will force the effect to STOP spawning after this many seconds. Use this for Constant Spawners (e.g., Smoke that lasts 2 seconds).")]
    public float duration = 0f;

    private VisualEffect vfx;
    private PooledObject pooledObj;
    private bool stopSignalSent = false;

    void Awake()
    {
        vfx = GetComponent<VisualEffect>();
        pooledObj = GetComponent<PooledObject>();
    }

    void OnEnable()
    {
        stopSignalSent = false;

        if (vfx != null)
        {
            // Reset the graph completely so it plays from the start
            vfx.Reinit();
            vfx.Play();
        }

        // --- OPTIMIZATION: Start logic routines ---
        if (duration > 0)
        {
            StartCoroutine(DurationRoutine());
        }
        StartCoroutine(CheckAliveRoutine());
    }

    // --- OPTIMIZATION: Replaced Update() with Coroutines ---

    private IEnumerator DurationRoutine()
    {
        yield return new WaitForSeconds(duration);
        StopAndFade();
    }

    private IEnumerator CheckAliveRoutine()
    {
        // Wait a tiny bit to let particles spawn initially
        yield return new WaitForSeconds(0.1f);

        WaitForSeconds wait = new WaitForSeconds(0.5f); // Check 2 times per second

        while (true)
        {
            if (vfx != null && !stopSignalSent)
            {
                // Only check if we are stopping or if it's a burst effect
                if (vfx.aliveParticleCount == 0)
                {
                    Cleanup();
                    yield break; // Exit coroutine
                }
            }
            // If stop signal was sent, we specifically wait for particles to die
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
    // -------------------------------------------------------

    public void StopAndFade()
    {
        if (stopSignalSent) return;

        stopSignalSent = true;
        if (vfx != null)
        {
            // This turns off the "Constant Spawn Rate" in the graph
            vfx.Stop();
        }
    }

    private void Cleanup()
    {
        if (pooledObj != null)
        {
            pooledObj.ReturnToPool();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}