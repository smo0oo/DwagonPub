using UnityEngine;
using PixelCrushers.DialogueSystem;

public class EnemyDeathIncrementer : MonoBehaviour
{
    [Header("Dialogue System Settings")]
    [Tooltip("The exact name of the Variable in your Dialogue Database.")]
    public string variableName;

    [Tooltip("How much to add? Usually 1.")]
    public int incrementAmount = 1;

    [Tooltip("Refresh the Quest Tracker HUD immediately?")]
    public bool updateQuestTracker = true;

    // --- SAFETY FLAGS ---
    private bool isQuitting = false;
    private bool isProcessed = false;

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        // SAFETY 1: Don't run if the game is closing
        if (isQuitting) return;

        // SAFETY 2: Don't run if the scene is unloading (switching levels)
        if (!gameObject.scene.isLoaded) return;

        // SAFETY 3: Don't double-count (just in case)
        if (isProcessed) return;

        if (string.IsNullOrEmpty(variableName)) return;

        isProcessed = true;
        IncrementVariable();
    }

    private void IncrementVariable()
    {
        // 1. Get current value
        int currentValue = DialogueLua.GetVariable(variableName).asInt;

        // 2. Increment
        int newValue = currentValue + incrementAmount;

        // 3. Save back
        DialogueLua.SetVariable(variableName, newValue);

        Debug.Log($"<color=green>[Quest Update] Object Destroyed!</color> Variable '<b>{variableName}</b>' updated to {newValue}.");

        // 4. Update HUD
        if (updateQuestTracker)
        {
            DialogueManager.SendUpdateTracker();
        }
    }
}