using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;

public class CharacterSheetUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI characterNameText;
    public Transform inventorySlotsParent;
    public Transform equipmentSlotsParent;
    public List<EquipmentSlot> equipmentSlots;
    public GameObject inventorySlotPrefab;

    [Header("Raycast Blockers")]
    [Tooltip("The UI Image placed over the inventory slots to intercept mouse clicks.")]
    public GameObject inventoryBlocker;

    // The specific components for THIS character sheet
    private Inventory characterInventory;
    private PlayerEquipment characterEquipment;
    private PlayerStats characterStats;
    private List<InventorySlot> uiInventorySlots = new List<InventorySlot>();

    private InventoryManager mainInventoryManager;
    private EquipmentManager mainEquipmentManager;

    private int partySlotIndex = -1;

    void Awake()
    {
        mainInventoryManager = InventoryManager.instance;
        mainEquipmentManager = EquipmentManager.instance;
    }

    void OnDisable()
    {
        if (characterInventory != null) characterInventory.OnInventoryChanged -= RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged -= RefreshEquipmentSlot;
    }

    public void DisplayCharacter(GameObject playerObject, int slotIndex)
    {
        partySlotIndex = slotIndex;

        if (characterInventory != null) characterInventory.OnInventoryChanged -= RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged -= RefreshEquipmentSlot;

        CharacterRoot root = playerObject.GetComponent<CharacterRoot>();
        if (root == null)
        {
            root = playerObject.GetComponentInChildren<CharacterRoot>();
        }

        if (root == null) return;

        characterInventory = root.Inventory;
        characterEquipment = root.PlayerEquipment;
        characterStats = root.PlayerStats;

        if (characterNameText != null)
        {
            characterNameText.text = GetCharacterDisplayName(playerObject);
        }

        if (characterInventory != null) characterInventory.OnInventoryChanged += RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged += RefreshEquipmentSlot;

        InitializeInventorySlots();
        InitializeEquipmentSlots();
        RefreshEquipmentSlots();

        // --- DETERMINE SCENE STATE ---
        bool isFullyControllable = true;

        SceneInfo currentSceneInfo = FindAnyObjectByType<SceneInfo>();
        if (currentSceneInfo != null && partySlotIndex >= 0 && partySlotIndex < currentSceneInfo.playerConfigs.Count)
        {
            PlayerSceneState state = currentSceneInfo.playerConfigs[partySlotIndex].state;

            if (state == PlayerSceneState.Inactive || state == PlayerSceneState.SpawnAtMarker || state == PlayerSceneState.Hidden)
            {
                isFullyControllable = false;
            }
        }

        // --- 1. INVENTORY LOCK (Physical Raycast Blocker) ---
        if (inventoryBlocker != null)
        {
            inventoryBlocker.SetActive(!isFullyControllable);
        }

        // --- 2. EQUIPMENT LOCK (Visual Fade + Code Lock) ---
        if (equipmentSlotsParent != null)
        {
            CanvasGroup eqCG = GetOrAddCanvasGroup(equipmentSlotsParent.gameObject);
            eqCG.alpha = isFullyControllable ? 1.0f : 0.5f;
        }

        foreach (var slot in equipmentSlots)
        {
            if (slot != null) slot.isLocked = !isFullyControllable;
        }

        // --- 3. NAME TEXT FADE ---
        if (characterNameText != null)
        {
            CanvasGroup nameCG = GetOrAddCanvasGroup(characterNameText.gameObject);
            nameCG.alpha = isFullyControllable ? 1.0f : 0.5f;
        }
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = target.AddComponent<CanvasGroup>();
        }
        return cg;
    }

    private string GetCharacterDisplayName(GameObject player)
    {
        DialogueActor actor = player.GetComponentInChildren<DialogueActor>();
        if (actor != null && !string.IsNullOrEmpty(actor.actor))
        {
            var dbActor = DialogueManager.MasterDatabase.GetActor(actor.actor);
            if (dbActor != null)
            {
                string display = dbActor.LookupValue("Display Name");
                return string.IsNullOrEmpty(display) ? dbActor.Name : display;
            }
        }
        return player.name.Replace("(Clone)", "").Trim();
    }

    private void InitializeEquipmentSlots()
    {
        foreach (EquipmentSlot slot in equipmentSlots)
        {
            slot.Initialize(mainEquipmentManager, characterEquipment);
        }
    }

    private void InitializeInventorySlots()
    {
        // Destroy all old slots, but DO NOT destroy the blocker image!
        foreach (Transform child in inventorySlotsParent)
        {
            if (inventoryBlocker == null || child.gameObject != inventoryBlocker)
            {
                Destroy(child.gameObject);
            }
        }

        uiInventorySlots.Clear();

        if (characterInventory == null) return;

        for (int i = 0; i < characterInventory.inventorySize; i++)
        {
            GameObject slotGO = Instantiate(inventorySlotPrefab, inventorySlotsParent);
            InventorySlot newSlot = slotGO.GetComponent<InventorySlot>();
            newSlot.Initialize(mainInventoryManager, i, characterInventory);
            uiInventorySlots.Add(newSlot);
        }

        // Force the blocker to render ON TOP of the newly spawned slots
        if (inventoryBlocker != null)
        {
            inventoryBlocker.transform.SetAsLastSibling();
        }

        RefreshInventorySlots();
    }

    private void RefreshInventorySlots()
    {
        if (characterInventory == null) return;

        for (int i = 0; i < uiInventorySlots.Count; i++)
        {
            if (i < characterInventory.items.Count)
                uiInventorySlots[i].UpdateSlot(characterInventory.items[i]);
            else
                uiInventorySlots[i].UpdateSlot(null);
        }
    }

    private void RefreshEquipmentSlots()
    {
        foreach (var slot in equipmentSlots) RefreshEquipmentSlot(slot.slotType);
    }

    private void RefreshEquipmentSlot(EquipmentType slotType)
    {
        EquipmentSlot slot = equipmentSlots.Find(s => s.slotType == slotType);
        if (slot != null && characterEquipment != null)
        {
            characterEquipment.equippedItems.TryGetValue(slotType, out ItemStack item);
            slot.UpdateSlot(item);
        }
    }
}