using UnityEngine;
using System.Collections.Generic;

public enum BodyPartSlot
{
    Head,
    Torso,
    Legs,
    Arms,
    Hands,
    Feet,
    FacialHair,
    Ears
}

[System.Serializable]
public class StartingBodyPart
{
    public BodyPartSlot slot;
    public GameObject prefab;
}

public class ModularBodyManager : MonoBehaviour
{
    [Header("Master Skeleton")]
    public Transform skeletonRoot;
    public SkinnedMeshRenderer referenceMesh;

    [Header("Default Loadout")]
    public List<StartingBodyPart> startingBodyParts = new List<StartingBodyPart>();

    private Dictionary<string, Transform> masterBoneMap = new Dictionary<string, Transform>();
    private Dictionary<BodyPartSlot, GameObject> activeBodyParts = new Dictionary<BodyPartSlot, GameObject>();

    void Awake()
    {
        CacheMasterSkeleton();
    }

    void Start()
    {
        foreach (var part in startingBodyParts)
        {
            if (part.prefab != null) SwapBodyPart(part.slot, part.prefab);
        }
    }

    private void CacheMasterSkeleton()
    {
        if (referenceMesh == null) return;

        masterBoneMap.Clear();
        foreach (Transform bone in referenceMesh.bones)
        {
            if (bone != null && !masterBoneMap.ContainsKey(bone.name))
            {
                masterBoneMap.Add(bone.name, bone);
            }
        }

        if (referenceMesh.rootBone != null && !masterBoneMap.ContainsKey(referenceMesh.rootBone.name))
        {
            masterBoneMap.Add(referenceMesh.rootBone.name, referenceMesh.rootBone);
        }
    }

    public void SwapBodyPart(BodyPartSlot slot, GameObject partPrefab)
    {
        if (partPrefab == null) return;
        if (masterBoneMap.Count == 0) CacheMasterSkeleton();

        if (activeBodyParts.ContainsKey(slot) && activeBodyParts[slot] != null)
        {
            Destroy(activeBodyParts[slot]);
        }

        GameObject newPartInstance = Instantiate(partPrefab, transform);
        newPartInstance.name = $"{slot}_{partPrefab.name}";
        newPartInstance.transform.localPosition = Vector3.zero;
        newPartInstance.transform.localRotation = Quaternion.identity;

        SkinnedMeshRenderer[] renderers = newPartInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
        List<GameObject> zombieSkeletonsToDestroy = new List<GameObject>();

        foreach (var renderer in renderers)
        {
            if (renderer.rootBone != null)
            {
                Transform prefabSkeletonRoot = renderer.rootBone;
                while (prefabSkeletonRoot.parent != null && prefabSkeletonRoot.parent != newPartInstance.transform)
                {
                    prefabSkeletonRoot = prefabSkeletonRoot.parent;
                }
                if (!zombieSkeletonsToDestroy.Contains(prefabSkeletonRoot.gameObject))
                {
                    zombieSkeletonsToDestroy.Add(prefabSkeletonRoot.gameObject);
                }
            }

            Transform[] remappedBones = new Transform[renderer.bones.Length];
            for (int i = 0; i < renderer.bones.Length; i++)
            {
                Transform originalBone = renderer.bones[i];
                if (originalBone == null) continue;

                if (masterBoneMap.TryGetValue(originalBone.name, out Transform matchingMasterBone))
                {
                    // Success! The bone matches perfectly.
                    remappedBones[i] = matchingMasterBone;
                }
                else
                {
                    // AAA FIX: EXTRA BONE GRAFTING
                    // This bone exists in the Head prefab, but not the Body prefab (e.g. a Jaw or Ear bone).
                    Debug.LogWarning($"<color=yellow>[ModularBody]</color> Extra bone '{originalBone.name}' found! Grafting it to the master skeleton...");

                    Transform zombieParent = originalBone.parent;

                    // Try to find where this bone *should* attach on the master rig
                    if (zombieParent != null && masterBoneMap.TryGetValue(zombieParent.name, out Transform masterParent))
                    {
                        originalBone.SetParent(masterParent, true);
                    }
                    else
                    {
                        // Fallback: attach it to the root of the character
                        originalBone.SetParent(skeletonRoot, true);
                    }

                    remappedBones[i] = originalBone;

                    // Add the newly grafted bone to our dictionary so its children can find it!
                    masterBoneMap[originalBone.name] = originalBone;
                }
            }

            renderer.bones = remappedBones;

            if (renderer.rootBone != null && masterBoneMap.TryGetValue(renderer.rootBone.name, out Transform matchingRoot))
                renderer.rootBone = matchingRoot;
            else
                renderer.rootBone = referenceMesh.rootBone;

            renderer.localBounds = referenceMesh.localBounds;
            renderer.updateWhenOffscreen = true;
        }

        // Now that all extra bones are safely grafted onto the master rig, we can safely delete the remaining empty zombie rig
        foreach (var root in zombieSkeletonsToDestroy)
        {
            Destroy(root);
        }

        activeBodyParts[slot] = newPartInstance;
    }

    public void RemoveBodyPart(BodyPartSlot slot)
    {
        if (activeBodyParts.ContainsKey(slot) && activeBodyParts[slot] != null)
        {
            Destroy(activeBodyParts[slot]);
            activeBodyParts[slot] = null;
        }
    }
}