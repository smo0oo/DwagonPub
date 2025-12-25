using UnityEngine;
using PixelCrushers.DialogueSystem;

public class DialogueGameStateListener : MonoBehaviour
{
    // Automatically hook into Dialogue System events
    void OnEnable()
    {
        DialogueManager.instance.conversationStarted += OnConversationStarted;
        DialogueManager.instance.conversationEnded += OnConversationEnded;
    }

    void OnDisable()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted -= OnConversationStarted;
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
        }
    }

    private void OnConversationStarted(Transform actor)
    {
        if (GameManager.instance != null)
        {
            // Enter Dialogue Mode (Disable controls, Hide HUD)
            GameManager.instance.SetDialogueState(true);
        }
    }

    private void OnConversationEnded(Transform actor)
    {
        if (GameManager.instance != null)
        {
            // Exit Dialogue Mode (Restore controls exactly as they were)
            GameManager.instance.SetDialogueState(false);
        }
    }
}