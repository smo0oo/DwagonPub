using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LoreScreenEntry
{
    [Header("Content")]
    public string title;
    [TextArea(3, 10)] public string description;
    public Sprite backgroundImage;

    [Header("Requirements")]
    [Tooltip("If checked, this screen only appears if the player is visiting a specific Scene Type (e.g. Dungeons).")]
    public bool requireSceneType = false;
    public SceneType requiredSceneType;

    [Tooltip("Minimum party level required to see this hint (good for avoiding spoilers).")]
    public int minLevel = 1;

    [Tooltip("Maximum party level (good for hiding 'Tutorial' tips later on).")]
    public int maxLevel = 999;
}

[CreateAssetMenu(fileName = "LoadingScreenDatabase", menuName = "UI/Loading Screen Database")]
public class LoadingScreenData : ScriptableObject
{
    public List<LoreScreenEntry> entries = new List<LoreScreenEntry>();

    // Fallback for when no specific lore matches
    public Sprite defaultFallbackImage;
}