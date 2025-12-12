using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class PlaceableTrap : MonoBehaviour
{
    [Header("Trap Settings")]
    [Tooltip("The radius of the trigger. This should match the radius of the SphereCollider.")]
    public float triggerRadius = 3f;
    [Tooltip("Which layers this trap will be triggered by.")]
    public LayerMask targetLayers;

    [Header("Owner")]
    [Tooltip("The character that placed this trap. This is set automatically.")]
    public GameObject owner;

    [Header("Feedback")]
    [Tooltip("Visual effect to play when the trap is triggered.")]
    public GameObject triggerVFX;

    [Header("Friendly Effects (Applied to allies)")]
    [SerializeReference]
    public List<IAbilityEffect> friendlyEffects = new List<IAbilityEffect>();

    [Header("Hostile Effects (Applied to enemies)")]
    [SerializeReference]
    public List<IAbilityEffect> hostileEffects = new List<IAbilityEffect>();

    private bool hasBeenTriggered = false;
    private List<CharacterRoot> affectedCharacters = new List<CharacterRoot>();

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenTriggered) return;

        // Check if the entering object is on a layer we care about
        if ((targetLayers.value & (1 << other.gameObject.layer)) > 0)
        {
            // Make sure the triggering object is a character
            if (other.GetComponentInParent<CharacterRoot>() != null)
            {
                TriggerTrap();
            }
        }
    }

    // --- THIS METHOD CONTAINS THE FIX ---
    private void TriggerTrap()
    {
        hasBeenTriggered = true;

        if (triggerVFX != null)
        {
            Instantiate(triggerVFX, transform.position, Quaternion.identity);
        }

        if (owner == null)
        {
            Debug.LogError("PlaceableTrap has no owner! Cannot determine friend/foe.", this);
            Destroy(gameObject);
            return;
        }

        // Get the layer of the character who PLACED the trap.
        int ownerLayer = owner.layer;

        Collider[] hits = Physics.OverlapSphere(transform.position, triggerRadius);

        foreach (var hit in hits)
        {
            CharacterRoot hitCharacter = hit.GetComponentInParent<CharacterRoot>();

            // If we hit a character that we haven't already affected...
            if (hitCharacter != null && !affectedCharacters.Contains(hitCharacter))
            {
                affectedCharacters.Add(hitCharacter);

                int targetLayer = hitCharacter.gameObject.layer;
                bool isAlly = ownerLayer == targetLayer;

                var effectsToApply = isAlly ? friendlyEffects : hostileEffects;

                foreach (var effect in effectsToApply)
                {
                    // Apply the effect from the owner to the hit character's root object.
                    effect.Apply(owner, hitCharacter.gameObject);
                }
            }
        }

        Destroy(gameObject);
    }

    private void OnValidate()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null)
        {
            if (!col.isTrigger)
            {
                Debug.LogWarning("The SphereCollider on this trap was not set to 'Is Trigger'. It has been set automatically.", this);
                col.isTrigger = true;
            }
            col.radius = triggerRadius;
        }
    }
}