using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewGeneratorConfig", menuName = "Dungeon/Generator Config")]
public class GeneratorConfig : ScriptableObject
{
    public List<ColorToPrefabMapping> mappings = new List<ColorToPrefabMapping>();
}