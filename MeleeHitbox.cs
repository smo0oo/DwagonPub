using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class MeleeHitbox : MonoBehaviour
{
    private Ability sourceAbility;
    private GameObject caster;
    private List<Health> hitTargets = new List<Health>();

    public void Setup(Ability ability, GameObject casterObject)
    {
        sourceAbility = ability;
        caster = casterObject;
        hitTargets.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        Health targetHealth = other.GetComponentInChildren<Health>();
        if (targetHealth == null || hitTargets.Contains(targetHealth)) return;

        if (sourceAbility != null && !sourceAbility.canHitCaster)
        {
            Health casterHealth = caster.transform.root.GetComponentInChildren<Health>();
            if (casterHealth != null && casterHealth == targetHealth)
            {
                return;
            }
        }

        hitTargets.Add(targetHealth);

        if (sourceAbility != null)
        {
            // --- FIX: Get the target's layer from their CharacterRoot for an accurate comparison ---
            CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
            CharacterRoot targetRoot = other.GetComponentInParent<CharacterRoot>();

            if (casterRoot == null || targetRoot == null) return;

            int casterLayer = casterRoot.gameObject.layer;
            int targetLayer = targetRoot.gameObject.layer;

            bool isAlly = casterLayer == targetLayer;

            var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

            foreach (var effect in effectsToApply)
            {
                effect.Apply(caster, other.gameObject);
            }
        }
    }
}