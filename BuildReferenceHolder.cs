using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BuildReferences", menuName = "Build/Reference Holder")]
public class BuildReferenceHolder : ScriptableObject
{
    [Header("Force Include Assets")]
    [Tooltip("Drag VFX Prefabs, Materials, or other assets here to force them into the build.")]
    public List<GameObject> vfxPrefabs;

    [Tooltip("Force specific materials (optional).")]
    public List<Material> materials;
}