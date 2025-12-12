using UnityEngine;

/// <summary>
/// A contract for any object that the player can interact with.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// This method is called when the player successfully interacts with this object.
    /// </summary>
    /// <param name="interactor">The GameObject that initiated the interaction (e.g., the player).</param>
    void Interact(GameObject interactor);
}