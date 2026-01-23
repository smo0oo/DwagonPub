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
    private GameObject rootObject;
    private bool stopSignalSent = false;

    void Awake()
    {
        vfx = GetComponent<VisualEffect>();
    }

    void OnEnable()
    {
        // Find the root and the component upon activation.
        if (pooledObj == null)
        {
            pooledObj = GetComponentInParent<PooledObject>();
            rootObject = (pooledObj != null) ? pooledObj.gameObject : transform.root.gameObject;
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
        yield return new WaitForSeconds(0.1f);
        WaitForSeconds wait = new WaitForSeconds(0.5f);

        while (true)
        {
            if (vfx != null)
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
        if (pooledObj == null) pooledObj = GetComponentInParent<PooledObject>();

        if (pooledObj != null)
        {
            pooledObj.ReturnToPool();
        }
        else
        {
            Destroy(rootObject != null ? rootObject : gameObject);
        }
    }
}