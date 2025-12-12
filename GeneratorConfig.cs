using UnityEngine;
using System.Collections.Generic; // <-- Add this line

[CreateAssetMenu(fileName = "NewGeneratorConfig", menuName = "Level Generation/Generator Config")]
public class GeneratorConfig : ScriptableObject
{
    public List<ColorToPrefabMapping> mappings;
}