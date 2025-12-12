using UnityEngine;

/// <summary>
/// A central hub component placed on the root of a character prefab.
/// It finds and caches references to all core components, providing a single,
/// efficient point of access for other scripts.
/// </summary>
public class CharacterRoot : MonoBehaviour
{
    // Public properties provide read-only access to the cached components from other scripts.
    public PlayerStats PlayerStats { get; private set; }
    public Health Health { get; private set; }
    public Inventory Inventory { get; private set; }
    public PlayerEquipment PlayerEquipment { get; private set; }
    public PlayerAbilityHolder PlayerAbilityHolder { get; private set; }
    public PlayerMovement PlayerMovement { get; private set; }
    public PartyMemberAI PartyMemberAI { get; private set; }
    public Animator Animator { get; private set; }

    void Awake()
    {
        // Find and cache all components that might exist on this character.
        PlayerStats = GetComponentInChildren<PlayerStats>(true);
        Health = GetComponentInChildren<Health>(true);
        Inventory = GetComponentInChildren<Inventory>(true);
        PlayerEquipment = GetComponentInChildren<PlayerEquipment>(true);
        PlayerAbilityHolder = GetComponentInChildren<PlayerAbilityHolder>(true);
        PlayerMovement = GetComponent<PlayerMovement>();
        PartyMemberAI = GetComponent<PartyMemberAI>();
        Animator = GetComponentInChildren<Animator>(true);

        // --- MODIFIED VALIDATION ---
        // We only validate components that EVERY character (Player and Enemy) MUST have.
        // Player-specific components like PlayerStats and Inventory are now optional and will
        // simply be null for non-player characters, which is correct.
        if (Health == null) Debug.LogError($"CharacterRoot on {gameObject.name} could not find a Health component.", this);
    }
}