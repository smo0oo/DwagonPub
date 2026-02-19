using UnityEngine;

// [FIX] Renamed to TacticalNodeType to avoid conflict with LocationNode.cs
public enum TacticalNodeType
{
    Cover,
    RangedVantage,
    HealerSpot,
    MeleeAnchor
}

public class TacticalNode : MonoBehaviour
{
    [Header("Node Configuration")]
    public TacticalNodeType nodeType = TacticalNodeType.Cover; // [FIX] Updated type

    [Tooltip("If true, this node protects against the specific ability currently being cast.")]
    public bool isCurrentlySafe = true;

    [Header("State (Read Only)")]
    public GameObject currentOccupant;

    private void OnDrawGizmos()
    {
        if (currentOccupant != null) Gizmos.color = Color.red;
        else if (!isCurrentlySafe) Gizmos.color = new Color(1f, 0.5f, 0f);
        else Gizmos.color = nodeType == TacticalNodeType.Cover ? Color.blue : Color.green;

        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1f);
    }

    public bool ClaimNode(GameObject claimant)
    {
        if (currentOccupant != null && currentOccupant != claimant) return false;
        currentOccupant = claimant;
        return true;
    }

    public void ReleaseNode(GameObject claimant)
    {
        if (currentOccupant == claimant) currentOccupant = null;
    }
}