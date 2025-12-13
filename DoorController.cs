using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class DoorController : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("The exact name of the scene file to load (e.g., 'Tavern_Interior').")]
    public string targetSceneName;
    [Tooltip("The ID of the PlayerSpawnPoint in the target scene where the player should appear.")]
    public string targetSpawnPointId;

    [Header("Interaction")]
    [Tooltip("How close (in meters) the player needs to be to use the door. Default is 4m.")]
    public float activationDistance = 4.0f;

    [Tooltip("The specific spot the player should walk to in order to use this door. If null, the door's pivot point is used.")]
    public Transform interactionPoint;

    public void UseDoor()
    {
        if (GameManager.instance == null) return;
        if (!string.IsNullOrEmpty(targetSceneName) && !string.IsNullOrEmpty(targetSpawnPointId))
        {
            if (!GameManager.instance.IsTransitioning)
            {
                GameManager.instance.LoadLevel(targetSceneName, targetSpawnPointId);
            }
        }
        else
        {
            Debug.LogWarning("DoorController is missing Target Scene Name or Spawn Point ID.", this);
        }
    }

    private void OnValidate()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}