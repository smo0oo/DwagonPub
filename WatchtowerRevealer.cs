using UnityEngine;
using UnityEngine.Events;

public class WatchtowerRevealer : MonoBehaviour
{
    [Header("Watchtower Settings")]
    [Tooltip("How massive of a hole to punch in the fog (e.g., 250 units).")]
    public float revealRadius = 250f;

    [Tooltip("How close the wagon needs to get to automatically activate this (e.g., 20 units).")]
    public float autoActivateDistance = 20f;

    [Tooltip("Has this tower been activated by the player?")]
    public bool isActivated = false;

    [Header("Optional Polish")]
    public UnityEvent onTowerActivated;

    void Update()
    {
        // If already activated, stop checking to save CPU
        if (isActivated) return;

        // --- AAA FIX: Mathematical Distance Check ---
        // Bypasses Unity Physics entirely. Works flawlessly with Spline movement.
        if (WorldMapManager.instance != null && WorldMapManager.instance.wagonController != null)
        {
            float distanceToWagon = Vector3.Distance(transform.position, WorldMapManager.instance.wagonController.transform.position);

            if (distanceToWagon <= autoActivateDistance)
            {
                ActivateWatchtower();
            }
        }
    }

    /// <summary>
    /// Punches the hole in the fog. Can also be called manually by UI Buttons or Interaction scripts.
    /// </summary>
    public void ActivateWatchtower()
    {
        if (isActivated) return;

        isActivated = true;

        if (FogOfWarManager.instance != null)
        {
            // Command the manager to stamp a massive white circle at our exact location
            FogOfWarManager.instance.RevealArea(transform.position, revealRadius);
        }
        else
        {
            Debug.LogError("[Watchtower] FogOfWarManager is missing from the scene!");
        }

        // Fire off any particle effects, sound effects, or UI popups
        onTowerActivated?.Invoke();
    }
}