using UnityEngine;
using PixelCrushers.DialogueSystem;
using System.Collections.Generic;

[System.Serializable]
public class QuestIndicatorData
{
    [Tooltip("The exact name of the quest in the Pixel Crushers database.")]
    public string questName;

    [Tooltip("Leave blank to just check the Quest State. Or, add a Lua condition like: Variable['WolfPelts'] >= 5")]
    public string luaCondition;
}

public class NPCQuestIndicator : MonoBehaviour
{
    [Header("Overhead Visuals")]
    [Tooltip("The prefab/UI with the Exclamation Mark (!)")]
    public GameObject questAvailableIcon;
    [Tooltip("The prefab/UI with the Question Mark (?)")]
    public GameObject questTurnInIcon;

    [Header("NPC Quests")]
    [Tooltip("Quests this NPC can hand out to the player.")]
    public List<QuestIndicatorData> questsToGive;

    [Tooltip("Quests this NPC receives to complete.")]
    public List<QuestIndicatorData> questsToTurnIn;

    void Start()
    {
        RefreshIndicators();
    }

    // Pixel Crushers built-in event: Fires automatically when a conversation finishes
    void OnConversationEnd(Transform actor)
    {
        RefreshIndicators();
    }

    // Call this manually if the player loots a quest item to update the UI instantly!
    public void RefreshIndicators()
    {
        if (DialogueManager.instance == null) return;

        bool showTurnIn = false;
        bool showAvailable = false;

        // 1. Check for Turn-Ins First (Highest Priority: ? overrides !)
        foreach (var q in questsToTurnIn)
        {
            if (QuestLog.GetQuestState(q.questName) == QuestState.Active)
            {
                // Use Lua.IsTrue to evaluate the condition string!
                if (string.IsNullOrEmpty(q.luaCondition) || Lua.IsTrue(q.luaCondition))
                {
                    showTurnIn = true;
                    break;
                }
            }
        }

        // 2. Check for Available Quests Second
        if (!showTurnIn)
        {
            foreach (var q in questsToGive)
            {
                if (QuestLog.GetQuestState(q.questName) == QuestState.Unassigned)
                {
                    // Use Lua.IsTrue here as well
                    if (string.IsNullOrEmpty(q.luaCondition) || Lua.IsTrue(q.luaCondition))
                    {
                        showAvailable = true;
                        break;
                    }
                }
            }
        }

        // 3. Toggle the Visuals
        if (questTurnInIcon != null) questTurnInIcon.SetActive(showTurnIn);
        if (questAvailableIcon != null) questAvailableIcon.SetActive(showAvailable);
    }
}