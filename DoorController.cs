using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class DoorController : MonoBehaviour
{
    [Header("Destination")]
    public string targetSceneName;
    public string targetSpawnPointId;

    [Header("Interaction")]
    public float activationDistance = 4.0f;
    public Transform interactionPoint;

    // --- NEW SETTING ---
    [Header("Dual Mode Settings")]
    [Tooltip("If true, using this door will pause the dungeon run and switch to the Wagon Defense team.")]
    public bool isDualModeSwitch = false;
    // -------------------

    public void UseDoor()
    {
        if (GameManager.instance == null) return;

        if (!string.IsNullOrEmpty(targetSceneName) && !string.IsNullOrEmpty(targetSpawnPointId))
        {
            if (!GameManager.instance.IsTransitioning)
            {
                // --- MODIFIED LOGIC ---
                if (isDualModeSwitch && DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
                {
                    // Instead of loading immediately, hand off to DualModeManager
                    Debug.Log("Dual Mode Switch triggered: Switching to Defense Team.");
                    DualModeManager.instance.SetNextDungeonStep(targetSceneName, targetSpawnPointId);
                }
                else
                {
                    // Standard behavior
                    GameManager.instance.LoadLevel(targetSceneName, targetSpawnPointId);
                }
                // ----------------------
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