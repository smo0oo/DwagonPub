using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LoadingScreenData", menuName = "Scene Management/Loading Screen Data")]
public class LoadingScreenData : ScriptableObject
{
    [Header("Backgrounds")]
    [Tooltip("The names of the sprite files inside a 'Resources/LoadingScreens' folder.")]
    public List<string> backgroundResourcesPaths;

    [Header("Tips & Lore")]
    [Tooltip("A list of all possible text snippets (tips, lore, etc.) to display.")]
    [TextArea(3, 5)]
    public List<string> loadingTips;
}