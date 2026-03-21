using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class MeleeHitbox : MonoBehaviour
{
    [Header("Debug")]
    public bool debugMode = false;

    private Ability sourceAbility;
    private GameObject caster;
    private List<Health> hitTargets = new List<Health>();
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        GetComponent<BoxCollider>().isTrigger = true;
    }

    public void Setup(Ability ability, GameObject casterObject)
    {
        sourceAbility = ability;
        caster = casterObject;
        hitTargets.Clear();

        int projectileLayer = LayerMask.NameToLayer("FriendlyRanged");
        if (projectileLayer != -1)
        {
            gameObject.layer = projectileLayer;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("ClothPhysics")) return;

        if (debugMode) Debug.Log($"[MeleeHitbox] Hit: {other.name} on Layer: {LayerMask.LayerToName(other.gameObject.layer)}");

        Health targetHealth = other.GetComponentInParent<Health>();

        if (targetHealth == null)
        {
            if (debugMode) Debug.LogWarning($"[MeleeHitbox] Ignored {other.name}: No Health component found.");
            return;
        }

        if (hitTargets.Contains(targetHealth)) return;

        if (sourceAbility != null && !sourceAbility.canHitCaster)
        {
            Health casterHealth = caster.transform.root.GetComponentInChildren<Health>();
            if (casterHealth != null && casterHealth == targetHealth) return;
        }

        hitTargets.Add(targetHealth);

        if (sourceAbility != null)
        {
            CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
            CharacterRoot targetRoot = other.GetComponentInParent<CharacterRoot>();

            if (casterRoot == null) return;

            bool isAlly = false;

            if (targetRoot != null)
            {
                isAlly = (casterRoot.gameObject.layer == targetRoot.gameObject.layer);
            }

            if (debugMode) Debug.Log($"[MeleeHitbox] Applying Effect to {targetHealth.name}. IsAlly: {isAlly}");

            // --- AAA FIX: Play the true Impact Sound at the exact point of contact! ---
            if (sourceAbility.impactSound != null)
            {
                // We use SFXManager so it obeys volume sliders and 3D space perfectly
                SFXManager.PlayAtPoint(sourceAbility.impactSound, other.transform.position);
            }
            // -----------------------------------------------------------------------

            var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

            foreach (var effect in effectsToApply)
            {
                effect.Apply(caster, targetHealth.gameObject);
            }
        }
    }
}