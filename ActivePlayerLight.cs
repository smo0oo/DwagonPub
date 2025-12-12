using UnityEngine;

/// <summary>
/// Enables a Light component only when this character is the active player.
/// </summary>
public class ActivePlayerLight : MonoBehaviour
{
    [Header("Component Reference")]
    [Tooltip("Assign the Point Light component from this prefab that should be toggled.")]
    public Light playerLight;

    void OnEnable()
    {
        // Subscribe to the event that fires whenever the active player changes.
        PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged;
    }

    void OnDisable()
    {
        // Unsubscribe to prevent errors and memory leaks when this object is disabled.
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
    }

    void Start()
    {
        // When the scene starts, check the current active player and set the light's initial state.
        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
        {
            HandleActivePlayerChanged(PartyManager.instance.ActivePlayer);
        }
    }

    /// <summary>
    /// This method is called automatically by the PartyManager's event.
    /// </summary>
    private void HandleActivePlayerChanged(GameObject newActivePlayer)
    {
        if (playerLight == null) return;

        // Check if this character's GameObject is the one that just became active.
        if (newActivePlayer == this.gameObject)
        {
            // If it is, turn the light on.
            playerLight.enabled = true;
        }
        else
        {
            // If it's any other character, turn the light off.
            playerLight.enabled = false;
        }
    }
}