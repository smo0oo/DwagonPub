using UnityEngine;
using System.Collections.Generic;

public class PartyInventoryManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PartyManager singleton.")]
    public PartyManager partyManager;

    [Tooltip("Assign your CharacterSheetUI panels here (e.g. Player 1 Sheet, Player 2 Sheet...).")]
    public List<CharacterSheetUI> characterSheets;

    void OnEnable()
    {
        if (partyManager == null) partyManager = PartyManager.instance;
        RefreshAllCharacterSheets();
    }

    public void RefreshAllCharacterSheets()
    {
        if (partyManager == null)
        {
            Debug.LogError("[PartyInventoryManager] PartyManager reference is missing!");
            return;
        }

        List<GameObject> allPlayers = partyManager.partyMembers;

        if (allPlayers == null) return;

        for (int i = 0; i < characterSheets.Count; i++)
        {
            if (i < allPlayers.Count && allPlayers[i] != null)
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