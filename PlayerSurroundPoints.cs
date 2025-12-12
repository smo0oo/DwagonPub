using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Creates and manages the 8 surround points around the player.
/// </summary>
public class PlayerSurroundPoints : MonoBehaviour
{
    [Tooltip("The distance from the player where surround points will be created.")]
    public float surroundDistance = 4f;

    public List<SurroundPoint> points = new List<SurroundPoint>();
    private readonly Vector3[] directions =
    {
        new Vector3(0, 0, 1),   // North
        new Vector3(1, 0, 1).normalized,  // North-East
        new Vector3(1, 0, 0),   // East
        new Vector3(1, 0, -1).normalized, // South-East
        new Vector3(0, 0, -1),  // South
        new Vector3(-1, 0, -1).normalized,// South-West
        new Vector3(-1, 0, 0),  // West
        new Vector3(-1, 0, 1).normalized // North-West
    };

    void Awake()
    {
        // Create the 8 points on startup.
        for (int i = 0; i < 8; i++)
        {
            points.Add(new SurroundPoint());
        }
    }

    void OnEnable()
    {
        // Register this player with the manager when it becomes active.
        if (SurroundPointManager.instance != null)
        {
            SurroundPointManager.instance.RegisterPlayer(this);
        }
    }

    void OnDisable()
    {
        // Unregister when the player is disabled or destroyed.
        if (SurroundPointManager.instance != null)
        {
            SurroundPointManager.instance.UnregisterPlayer(this);
        }
    }

    void LateUpdate()
    {
        // Update the position of all 8 points every frame to follow the player.
        for (int i = 0; i < points.Count; i++)
        {
            points[i].position = transform.position + (directions[i] * surroundDistance);
        }
    }

    // Optional: Draw gizmos in the editor to see the points.
void OnDrawGizmos()
{
    // --- THIS METHOD IS UPDATED ---
    // We now loop through the actual points list to check their status.

    if (points == null) return;

    foreach (var point in points)
    {
        // Set the color based on the IsOccupied() method of the point.
        if (point.IsOccupied())
        {
            Gizmos.color = Color.red; // Red for occupied
        }
        else
        {
            Gizmos.color = Color.magenta; // Magenta for free
        }
        
        // Use the point's updated position for drawing.
        Gizmos.DrawWireSphere(point.position, 0.5f);
    }
}
}