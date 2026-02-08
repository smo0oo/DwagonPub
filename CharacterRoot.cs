using UnityEngine;

/// <summary>
/// A central hub component placed on the root of a character prefab.
/// It finds and caches references to all core components, providing a single,
/// efficient point of access for other scripts.
/// the search 'upstream'  on any character should always stop at the first 'character root' component
/// </summary>
public class CharacterRoot : MonoBehaviour
{
    // Public properties provide read-only access to cached components.
    public PlayerStats PlayerStats { get; private set; }
    public Health Health { get; private set; }
    public Inventory Inventory { get; private set; }
    public PlayerEquipment PlayerEquipment { get; private set; }
    public PlayerAbilityHolder PlayerAbilityHolder { get; private set; }
    public PlayerMovement PlayerMovement { get; private set; }
    public PartyMemberAI PartyMemberAI { get; private set; }
    public EnemyAI EnemyAI { get; private set; } // Added EnemyAI reference
    public Animator Animator { get; private set; }

    void Awake()
    {
        // Find and cache all core components. 
        // Using GetComponentInChildren(true) ensures we find them even if the sub-object is inactive.
        PlayerStats = GetComponentInChildren<PlayerStats>(true);
        Health = GetComponentInChildren<Health>(true);
        Inventory = GetComponentInChildren<Inventory>(true);
        PlayerEquipment = GetComponentInChildren<PlayerEquipment>(true);
        PlayerAbilityHolder = GetComponentInChildren<PlayerAbilityHolder>(true);

        // Movement/AI are typically on the root object itself
        PlayerMovement = GetComponent<PlayerMovement>();
        PartyMemberAI = GetComponent<PartyMemberAI>();
        EnemyAI = GetComponent<EnemyAI>();

        Animator = GetComponentInChildren<Animator>(true);

        // --- VALIDATION ---
        // Every combatant (Player or Enemy) MUST have Health.
        if (Health == null)
        {
            Debug.LogError($"CharacterRoot on {gameObject.name} could not find a Health component. Combat will fail.", this);
        }

        // Logic Check: Inform developer if a character has neither AI nor Movement.
        if (PlayerMovement == null && PartyMemberAI == null && EnemyAI == null)
        {
            Debug.LogWarning($"CharacterRoot on {gameObject.name} has no movement or AI handlers.", this);
        }
    }
}