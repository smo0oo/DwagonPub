using UnityEngine;
using System.Collections;
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
    private BoxCollider boxCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    public void Setup(Ability ability, GameObject casterObject)
    {
        sourceAbility = ability;
        caster = casterObject;

        int projectileLayer = LayerMask.NameToLayer("FriendlyRanged");
        if (projectileLayer != -1)
        {
            gameObject.layer = projectileLayer;
        }
    }

    void OnEnable()
    {
        hitTargets.Clear();

        if (sourceAbility == null) return;

        if (sourceAbility.useDynamicHitbox)
        {
            StartCoroutine(DynamicHitboxRoutine());
        }
        else
        {
            boxCollider.size = sourceAbility.attackBoxSize;
            boxCollider.center = sourceAbility.attackBoxCenter;
        }
    }

    private IEnumerator DynamicHitboxRoutine()
    {
        float duration = sourceAbility.hitboxCloseDelay - sourceAbility.hitboxOpenDelay;
        if (duration <= 0.01f) duration = 0.1f;

        float elapsed = 0f;
        Vector3 startSize = sourceAbility.attackBoxSize;
        Vector3 endSize = sourceAbility.endAttackBoxSize;
        Vector3 startCenter = sourceAbility.attackBoxCenter;
        Vector3 endCenter = sourceAbility.endAttackBoxCenter;

        boxCollider.size = startSize;
        boxCollider.center = startCenter;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Normalize time from 0 to 1
            float t = elapsed / duration;

            // AAA FIX: Process the normalized time through the Animation Curve
            float curveValue = sourceAbility.hitboxScaleCurve != null ? sourceAbility.hitboxScaleCurve.Evaluate(t) : t;

            // Apply the mathematically curved value to the lerp
            boxCollider.size = Vector3.Lerp(startSize, endSize, curveValue);
            boxCollider.center = Vector3.Lerp(startCenter, endCenter, curveValue);

            yield return null;
        }

        boxCollider.size = endSize;
        boxCollider.center = endCenter;
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

            if (sourceAbility.impactSound != null)
            {
                SFXManager.PlayAtPoint(sourceAbility.impactSound, other.transform.position);
            }

            var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

            foreach (var effect in effectsToApply)
            {
                effect.Apply(caster, targetHealth.gameObject);
            }
        }
    }
}