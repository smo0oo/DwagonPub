using UnityEngine;
using MagicaCloth2; // Requires Magica Cloth 2 package

public class ClothLayerSync : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The physics layer created specifically for cloth colliders.")]
    public string clothLayerName = "ClothPhysics";

    private PlayerEquipment equipment;

    void Awake()
    {
        equipment = GetComponentInChildren<PlayerEquipment>();
    }

    void OnEnable()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged += RefreshClothLayers;
    }

    void OnDisable()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged -= RefreshClothLayers;
    }

    /// <summary>
    /// Finds all Magica Cloth colliders on this character and moves them to the cloth layer.
    /// Called automatically when equipment is swapped.
    /// </summary>
    public void RefreshClothLayers(EquipmentType slot)
    {
        int targetLayer = LayerMask.NameToLayer(clothLayerName);
        if (targetLayer == -1)
        {
            Debug.LogError($"[ClothSync] Layer '{clothLayerName}' does not exist in Project Settings!");
            return;
        }

        // Find all Magica colliders in children (including newly instantiated armor)
        var capsuleColliders = GetComponentsInChildren<MagicaCapsuleCollider>(true);
        var sphereColliders = GetComponentsInChildren<MagicaSphereCollider>(true);
        var planeColliders = GetComponentsInChildren<MagicaPlaneCollider>(true);

        foreach (var col in capsuleColliders) col.gameObject.layer = targetLayer;
        foreach (var col in sphereColliders) col.gameObject.layer = targetLayer;
        foreach (var col in planeColliders) col.gameObject.layer = targetLayer;
    }
}