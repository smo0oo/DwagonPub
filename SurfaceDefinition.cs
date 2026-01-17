using UnityEngine;
using UnityEngine.VFX; // Required for VisualEffectAsset
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Surface Definition", menuName = "RPG/Surface Definition")]
public class SurfaceDefinition : ScriptableObject
{
    [Header("Configuration")]
    [Tooltip("A generic prefab with a VisualEffect component. Used to play raw .vfx assets.")]
    public GameObject vfxContainerPrefab;

    [System.Serializable]
    public struct HitReaction
    {
        public DamageEffect.DamageType damageType;
        [Tooltip("Standard GameObject prefab (Legacy/Complex effects).")]
        public GameObject vfxPrefab;
        [Tooltip("Raw VFX Graph asset. If set, this overrides the prefab and uses the Container.")]
        public VisualEffectAsset vfxGraph;
        public AudioClip[] impactSounds;
    }

    [Header("Default Reaction")]
    // Removed 'defaultHitVFX' GameObject to enforce VFX Graph usage
    [Tooltip("The default VFX Graph to play if no specific damage type match is found.")]
    public VisualEffectAsset defaultHitGraph;
    public AudioClip[] defaultImpactSounds;

    [Header("Specific Reactions")]
    public List<HitReaction> hitReactions;

    public void GetReaction(DamageEffect.DamageType type, out GameObject prefab, out VisualEffectAsset graph, out AudioClip sound)
    {
        // Default values
        prefab = null; // No default GameObject anymore
        graph = defaultHitGraph;
        sound = GetRandomSound(defaultImpactSounds);

        foreach (var reaction in hitReactions)
        {
            if (reaction.damageType == type)
            {
                // If a specific one exists, use it
                if (reaction.vfxPrefab != null || reaction.vfxGraph != null)
                {
                    prefab = reaction.vfxPrefab;
                    graph = reaction.vfxGraph;
                }

                if (reaction.impactSounds != null && reaction.impactSounds.Length > 0)
                {
                    sound = GetRandomSound(reaction.impactSounds);
                }
                return;
            }
        }
    }

    private AudioClip GetRandomSound(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }
}