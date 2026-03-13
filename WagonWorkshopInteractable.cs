using UnityEngine;
using Cinemachine;
using PixelCrushers.DialogueSystem;
using System.Collections;

public class WagonWorkshopInteractable : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("A Virtual Camera positioned to look nicely at the Wagon.")]
    public CinemachineVirtualCamera workshopCamera;

    [Header("Shop Dialogue Settings")]
    [Tooltip("The conversation to play the FIRST time the player enters.")]
    public string firstTimeConversationTitle;
    [Tooltip("The conversation to play on all SUBSEQUENT visits.")]
    public string defaultConversationTitle;
    [Tooltip("A Lua variable name (e.g., 'MetWagoneer'). Tracked automatically.")]
    public string hasMetVariableName = "MetWagoneer";

    public void OpenShop()
    {
        // 1. Activate Camera
        if (workshopCamera != null)
        {
            workshopCamera.Priority = 100;
            workshopCamera.gameObject.SetActive(true);
        }

        // 2. Open the Shop UI visually
        if (WagonWorkshopUI.instance != null)
        {
            WagonWorkshopUI.instance.OpenWorkshop(CloseShop);
        }

        // 3. Trigger the appropriate Dialogue
        PlayShopkeeperDialogue();
    }

    private void PlayShopkeeperDialogue()
    {
        string convoToPlay = defaultConversationTitle;

        // Check if we have a first-time conversation setup
        if (!string.IsNullOrEmpty(firstTimeConversationTitle) && !string.IsNullOrEmpty(hasMetVariableName))
        {
            bool hasMet = DialogueLua.GetVariable(hasMetVariableName).asBool;
            if (!hasMet)
            {
                convoToPlay = firstTimeConversationTitle;
                // Immediately set it to true so the NEXT visit uses the default conversation
                DialogueLua.SetVariable(hasMetVariableName, true);
            }
        }

        // Start the conversation if a valid title exists
        if (!string.IsNullOrEmpty(convoToPlay))
        {
            StartCoroutine(WaitForDialogueSystem(convoToPlay));
        }
    }

    private IEnumerator WaitForDialogueSystem(string convoToPlay)
    {
        // Wait until any previous conversations are completely closed
        while (DialogueManager.isConversationActive)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();

        // --- THE FIX: Identify the true player and NPC ---
        GameObject activePlayer = PartyManager.instance != null ? PartyManager.instance.ActivePlayer : null;
        Transform pTransform = activePlayer != null ? activePlayer.transform : null;
        Transform nTransform = this.transform;

        // Pass Player First, NPC Second!
        DialogueManager.StartConversation(convoToPlay, pTransform, nTransform);
    }

    private void CloseShop()
    {
        // 1. Reset Camera
        if (workshopCamera != null)
        {
            workshopCamera.Priority = 0;
            workshopCamera.gameObject.SetActive(false);
        }

        // 2. Forcefully end the dialogue
        if (DialogueManager.isConversationActive)
        {
            DialogueManager.StopConversation();
        }
    }
}