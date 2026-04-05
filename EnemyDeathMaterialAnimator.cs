using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(Health))]
public class EnemyDeathMaterialAnimator : MonoBehaviour
{
    [Header("Shader Properties (Exact Reference Names)")]
    public string alphaPropertyName = "_DeathAlpha";
    public string noisePropertyName = "_DeathNoise";

    [Header("Target Renderers")]
    [Tooltip("Leave empty to auto-find 3D meshes.")]
    public Renderer[] targetRenderers;

    [Header("VFX Settings")]
    [Tooltip("Optional: A VFX prefab to spawn.")]
    public GameObject deathVFXPrefab;
    [Tooltip("How high off the ground the VFX should spawn.")]
    public Vector3 vfxOffset = new Vector3(0, 1.5f, 0);
    [Tooltip("How long to wait before spawning the VFX (in seconds).")]
    public float vfxSpawnDelay = 0f;

    [Header("Alpha Cut Settings")]
    public float alphaCutDuration = 2.5f;
    public float alphaCutStart = 0f;
    public float alphaCutEnd = 1f;
    public AnimationCurve alphaCutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Noise Contrast Settings")]
    public float noiseContrastDuration = 1.5f;
    public float noiseContrastStart = 0f;
    public float noiseContrastEnd = 5f;
    public AnimationCurve noiseContrastCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private MaterialPropertyBlock propBlock;
    private int alphaCutPropID;
    private int noiseContrastPropID;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        alphaCutPropID = Shader.PropertyToID(alphaPropertyName);
        noiseContrastPropID = Shader.PropertyToID(noisePropertyName);

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            List<Renderer> validRenderers = new List<Renderer>();
            validRenderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>());
            validRenderers.AddRange(GetComponentsInChildren<MeshRenderer>());
            targetRenderers = validRenderers.ToArray();
        }
    }

    public void PlayDeathAnimation()
    {
        if (targetRenderers.Length == 0) return;

        if (deathVFXPrefab != null)
        {
            StartCoroutine(SpawnVFXDelayed());
        }

        StartCoroutine(AnimateBothProperties());
    }

    private IEnumerator SpawnVFXDelayed()
    {
        if (vfxSpawnDelay > 0f)
        {
            yield return new WaitForSeconds(vfxSpawnDelay);
        }

        GameObject vfxInstance = null;

        // Extract only the Y rotation to keep the VFX flat relative to the ground
        Quaternion spawnRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (ObjectPooler.instance != null)
        {
            vfxInstance = ObjectPooler.instance.Get(deathVFXPrefab, transform.position + vfxOffset, spawnRotation);
        }
        else
        {
            vfxInstance = Instantiate(deathVFXPrefab, transform.position + vfxOffset, spawnRotation);
        }

        if (vfxInstance != null)
        {
            vfxInstance.SetActive(true);

            ParticleSystem ps = vfxInstance.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }

            VisualEffect vfx = vfxInstance.GetComponentInChildren<VisualEffect>();
            if (vfx != null)
            {
                vfx.Reinit();
                vfx.Play();
            }
        }
    }

    private IEnumerator AnimateBothProperties()
    {
        float elapsed = 0f;

        float maxDuration = Mathf.Max(alphaCutDuration, noiseContrastDuration);

        while (elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            float alphaTime = Mathf.Clamp01(elapsed / alphaCutDuration);
            float alphaEval = alphaCutCurve.Evaluate(alphaTime);
            float currentAlpha = Mathf.Lerp(alphaCutStart, alphaCutEnd, alphaEval);

            float noiseTime = Mathf.Clamp01(elapsed / noiseContrastDuration);
            float noiseEval = noiseContrastCurve.Evaluate(noiseTime);
            float currentNoise = Mathf.Lerp(noiseContrastStart, noiseContrastEnd, noiseEval);

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                {
                    targetRenderers[i].GetPropertyBlock(propBlock);

                    propBlock.SetFloat(alphaCutPropID, currentAlpha);
                    propBlock.SetFloat(noiseContrastPropID, currentNoise);

                    targetRenderers[i].SetPropertyBlock(propBlock);
                }
            }

            yield return null;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
            {
                targetRenderers[i].GetPropertyBlock(propBlock);

                propBlock.SetFloat(alphaCutPropID, alphaCutEnd);
                propBlock.SetFloat(noiseContrastPropID, noiseContrastEnd);

                targetRenderers[i].SetPropertyBlock(propBlock);
            }
        }
    }
}