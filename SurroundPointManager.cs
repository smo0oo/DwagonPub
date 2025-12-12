using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A data class to hold the state of a single surround point.
/// </summary>
public class SurroundPoint
{
    public Vector3 position;
    public EnemyAI occupant = null;

    public bool IsOccupied() => occupant != null;
}

/// <summary>
/// A singleton manager that controls the assignment of surround points around ALL players.
/// </summary>
public class SurroundPointManager : MonoBehaviour
{
    public static SurroundPointManager instance;

    // --- UPDATED: Now tracks all players with surround points ---
    private List<PlayerSurroundPoints> registeredPlayers = new List<PlayerSurroundPoints>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }
    }

    public void RegisterPlayer(PlayerSurroundPoints playerPoints)
    {
        if (!registeredPlayers.Contains(playerPoints))
        {
            registeredPlayers.Add(playerPoints);
        }
    }

    public void UnregisterPlayer(PlayerSurroundPoints playerPoints)
    {
        if (registeredPlayers.Contains(playerPoints))
        {
            registeredPlayers.Remove(playerPoints);
        }
    }

    // --- UPDATED: This method now requires the AI to specify its target ---
    /// <summary>
    /// An enemy AI calls this to request a free surround point around a specific target.
    /// </summary>
 
    public SurroundPoint RequestPoint(EnemyAI enemy, Transform target)
    {
        if (target == null) return null;

        // --- FIX: Use the CharacterRoot pattern for a safe and accurate search ---

        // 1. Find the top-level root of the character that was targeted.
        CharacterRoot characterRoot = target.GetComponentInParent<CharacterRoot>();
        if (characterRoot == null)
        {
            // If the targeted object isn't part of a character, abort.
            return null;
        }

        // 2. Now, find the PlayerSurroundPoints component on that specific root object.
        PlayerSurroundPoints targetPoints = characterRoot.GetComponent<PlayerSurroundPoints>();

        if (targetPoints == null || !registeredPlayers.Contains(targetPoints))
        {
            // The character is not a registered player with surround points.
            return null;
        }

        // (The rest of the method remains the same)
        SurroundPoint bestPoint = targetPoints.points
            .Where(p => !p.IsOccupied())
            .OrderBy(p => Vector3.Distance(enemy.transform.position, p.position))
            .FirstOrDefault();

        if (bestPoint != null)
        {
            bestPoint.occupant = enemy;
        }

        return bestPoint;
    }

    // --- UPDATED: This method now searches all players' points ---
    /// <summary>
    /// An enemy AI calls this when it dies or leashes to free up its point.
    /// </summary>
    public void ReleasePoint(EnemyAI enemy)
    {
        if (enemy == null) return;

        // Search through every registered player to find the point this enemy was occupying.
        foreach (var playerPoints in registeredPlayers)
        {
            SurroundPoint occupiedPoint = playerPoints.points.FirstOrDefault(p => p.occupant == enemy);
            if (occupiedPoint != null)
            {
                occupiedPoint.occupant = null;
                return; // Exit once the point is found and released.
            }
        }
    }
}