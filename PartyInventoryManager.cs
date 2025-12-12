using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class PartyInventoryManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PartyManager singleton from your scene.")]
    public PartyManager partyManager;
    [Tooltip("A list of all the CharacterSheetUI components (the sub-panels for each player).")]
    public List<CharacterSheetUI> characterSheets;

    void OnEnable()
    {
        RefreshAllCharacterSheets();
    }

    public void RefreshAllCharacterSheets()
    {
        if (partyManager == null)
        {
            Debug.LogError("PartyInventoryManager: PartyManager reference is not set!");
            return;
        }

        // --- FIX: Get the list of player GameObjects directly from the C# list ---
        List<GameObject> allPlayers = partyManager.partyMembers;

        if (allPlayers == null)
        {
            Debug.LogError("Could not retrieve player list from PartyManager!");
            return;
        }

        for (int i = 0; i < characterSheets.Count; i++)
        {
            if (i < allPlayers.Count)
            {
                characterSheets[i].gameObject.SetActive(true);
                characterSheets[i].DisplayCharacter(allPlayers[i]);
            }
            else
            {
                characterSheets[i].gameObject.SetActive(false);
            }
        }
    }
}