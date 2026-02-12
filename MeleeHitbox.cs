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
        // AAA FIX: Ensure this Hitbox has a Rigidbody.
        // Triggers interacting with Static Colliders (like Barrels) REQUIRE one side to have a Rigidbody.
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // Lightweight, doesn't fall/react to gravity
            rb.useGravity = false;
        }

        // Ensure collider is a trigger
        GetComponent<BoxCollider>().isTrigger = true;
    }

    public void Setup(Ability ability, GameObject casterObject)
    {
        sourceAbility = ability;
        caster = casterObject;
        hitTargets.Clear();

        // AAA FIX: Force the Hitbox Layer
        // Often Hitboxes inherit "Player" layer, which might not collide with "Destructible".
        // We switch it to "FriendlyRanged" (Layer 12 usually) or "Default" to ensure it hits everything.
        // Adjust "FriendlyRanged" to match your Projectile layer name exactly.
        int projectileLayer = LayerMask.NameToLayer("FriendlyRanged");
        if (projectileLayer != -1)
        {
            gameObject.layer = projectileLayer;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (debugMode) Debug.Log($"[MeleeHitbox] Hit: {other.name} on Layer: {LayerMask.LayerToName(other.gameObject.layer)}");

        // 1. Find Health (Check Parent to handle Colliders on child meshes)
        Health targetHealth = other.GetComponentInParent<Health>();

        if (targetHealth == null)
        {
            if (debugMode) Debug.LogWarning($"[MeleeHitbox] Ignored {other.name}: No Health component found.");
            return;
        }

        if (hitTargets.Contains(targetHealth)) return;

        // 2. Prevent Friendly Fire (Self)
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

            // 3. Alliance Logic
            bool isAlly = false;

            // If the target is a Character, check layers.
            // If the target is a Prop (targetRoot == null), it defaults to Hostile (isAlly = false).
            if (targetRoot != null)
            {
                isAlly = (casterRoot.gameObject.layer == targetRoot.gameObject.layer);
            }

            if (debugMode) Debug.Log($"[MeleeHitbox] Applying Effect to {targetHealth.name}. IsAlly: {isAlly}");

            var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

            foreach (var effect in effectsToApply)
            {
                effect.Apply(caster, targetHealth.gameObject);
            }
        }
    }
}