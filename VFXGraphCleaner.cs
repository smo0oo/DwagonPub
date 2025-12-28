using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class VFXGraphCleaner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("If > 0, the script will force the effect to STOP spawning after this many seconds. Use this for Constant Spawners (e.g., Smoke that lasts 2 seconds).")]
    public float duration = 0f;

    private VisualEffect vfx;
    private PooledObject pooledObj;
    private float timer;
    private bool stopSignalSent = false;

    void Awake()
    {
        vfx = GetComponent<VisualEffect>();
        pooledObj = GetComponent<PooledObject>();
    }

    void OnEnable()
    {
        timer = 0f;
        stopSignalSent = false;

        if (vfx != null)
        {
            // Reset the graph completely so it plays from the start
            vfx.Reinit();
            vfx.Play();
        }
    }

    void Update()
    {
        if (vfx == null) return;

        timer += Time.deltaTime;

        // 1. Handle Explicit Duration (For Constant Spawners)
        // If the user set a duration, wait for it, then tell the graph to STOP emitting.
        if (duration > 0 && timer >= duration && !stopSignalSent)
        {
            StopAndFade();
        }

        // 2. Handle Cleanup (For Everyone)
        // We wait a tiny bit (0.1s) to let the VFX actually spawn its first particle.
        // Then, if the particle count hits 0, we know it's truly finished.
        if (timer > 0.1f)
        {
            if (vfx.aliveParticleCount == 0)
            {
                Cleanup();
            }
        }
    }

    public void StopAndFade()
    {
        if (stopSignalSent) return;

        stopSignalSent = true;
        if (vfx != null)
        {
            // This turns off the "Constant Spawn Rate" in the graph,
            // allowing existing particles to live out their life and then die.
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