using UnityEngine;

public class SoulOrb : MonoBehaviour, IInteractable
{
    public int memberIndexToRevive = -1; // Default to invalid

    public void Interact(GameObject interactor)
    {
        if (DualModeManager.instance != null && memberIndexToRevive != -1)
        {
            DualModeManager.instance.ReviveMember(memberIndexToRevive, transform.position);

            if (FloatingTextManager.instance != null)
                FloatingTextManager.instance.ShowEvent("Soul Restored!", transform.position + Vector3.up * 2);

            Destroy(gameObject);
        }
        else
        {
            Debug.LogError($"SoulOrb Error: Invalid member index ({memberIndexToRevive}) or missing Manager.");
        }
    }
}