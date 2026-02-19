using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class TacticalArena : MonoBehaviour
{
    public static TacticalArena ActiveArena { get; private set; }

    [Header("Arena Nodes")]
    public List<TacticalNode> arenaNodes = new List<TacticalNode>();

    void Awake()
    {
        arenaNodes.AddRange(GetComponentsInChildren<TacticalNode>());
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<CharacterRoot>() != null)
        {
            if (ActiveArena != this) ActiveArena = this;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (ActiveArena == this && (other.CompareTag("Player") || other.GetComponent<CharacterRoot>() != null))
        {
            ActiveArena = null;
        }
    }

    // [FIX] Updated parameter from NodeType to TacticalNodeType
    public TacticalNode GetBestAvailableNode(TacticalNodeType desiredType, Vector3 searcherPosition, GameObject searcher)
    {
        TacticalNode bestNode = null;
        float closestDistance = float.MaxValue;

        TacticalNode fallbackNode = null;
        float fallbackDistance = float.MaxValue;

        foreach (var node in arenaNodes)
        {
            if (node.isCurrentlySafe && node.currentOccupant == null)
            {
                float dist = Vector3.Distance(searcherPosition, node.transform.position);

                if (node.nodeType == desiredType && dist < closestDistance)
                {
                    closestDistance = dist;
                    bestNode = node;
                }
                else if (dist < fallbackDistance)
                {
                    fallbackDistance = dist;
                    fallbackNode = node;
                }
            }
        }

        TacticalNode finalNode = bestNode != null ? bestNode : fallbackNode;

        if (finalNode != null) finalNode.ClaimNode(searcher);

        return finalNode;
    }
}