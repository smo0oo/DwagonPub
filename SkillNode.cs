using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SkillNode
{
    [Tooltip("The display name of this skill in the tree.")]
    public string skillName;
    [Tooltip("The icon to display in the skill tree UI.")]
    public Sprite skillIcon;
    [TextArea]
    public string description;

    [Tooltip("The list of Ability assets that represent the ranks of this skill.")]
    public List<Ability> skillRanks;

    // --- THIS IS THE KEY CHANGE ---
    [Tooltip("The INDEX of the node that must be unlocked (Element 0, 1, 2...). Set to -1 for no prerequisite.")]
    public int prerequisiteIndex = -1;
    // --- END OF CHANGE ---

    [Tooltip("The rank the prerequisite node must be at to unlock this skill.")]
    public int prerequisiteRank = 1;

    [Tooltip("The (X, Y) coordinate of this node in the skill tree grid.")]
    public Vector2Int gridPosition;
}