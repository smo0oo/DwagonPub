using UnityEngine;

public enum ForageEventType
{
    Bust,
    StandardLoot,
    Jackpot,
    Ambush,
    HiddenDungeon,
    DialogueEvent
}

[CreateAssetMenu(fileName = "New Forage Event", menuName = "World Map/Forage Event")]
public class ForageEventData : ScriptableObject
{
    [Header("Narrative")]
    public string eventName = "A Rustling in the Bushes";
    public ForageEventType eventType = ForageEventType.StandardLoot;

    [TextArea(4, 8)]
    public string storySnippet = "You push through the thick briar and discover...";
    public Sprite contextualArt;

    [Header("Rewards & Mechanics")]
    public LootTable rewardTable;
    public string linkedSceneName;

    [Header("Pixel Crushers Integration")]
    [Tooltip("The exact title of the conversation in your Dialogue Database.")]
    public string conversationTitle;

    [Header("UI Button Text")]
    public string acceptButtonText = "Take Loot and Leave";
}