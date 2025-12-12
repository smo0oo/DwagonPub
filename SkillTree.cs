using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Skill Tree", menuName = "RPG/Skill Tree")]
public class SkillTree : ScriptableObject
{
    public List<SkillNode> skillNodes = new List<SkillNode>();
}