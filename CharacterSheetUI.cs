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
    public List<EquipmentSlot> equipmentSlots;
    public GameObject inventorySlotPrefab;

    private Inventory characterInventory;
    private PlayerEquipment characterEquipment;
    private PlayerStats characterStats;
    private List<InventorySlot> uiInventorySlots = new List<InventorySlot>();

    private InventoryManager mainInventoryManager;
    private EquipmentManager mainEquipmentManager;

    void Awake()
    {
        mainInventoryManager = InventoryManager.instance;
        mainEquipmentManager = EquipmentManager.instance;
    }

    // --- THIS METHOD HAS BEEN UPDATED ---
    public void DisplayCharacter(GameObject playerObject)
    {
        if (characterInventory != null) characterInventory.OnInventoryChanged -= RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged -= RefreshEquipmentSlot;

        // Get the CharacterRoot to efficiently access all components
        CharacterRoot root = playerObject.GetComponent<CharacterRoot>();
        if (root == null)
        {
            Debug.LogError($"CharacterSheetUI cannot display character: {playerObject.name} is missing a CharacterRoot.", playerObject);
            return;
        }

        // Using CharacterRoot properties for direct component access
        characterInventory = root.Inventory;
        characterEquipment = root.PlayerEquipment;
        characterStats = root.PlayerStats;

        if (characterNameText != null)
        {
            string characterDisplayName = playerObject.name;
            DialogueActor dialogueActor = playerObject.GetComponentInChildren<DialogueActor>();
            if (dialogueActor != null && !string.IsNullOrEmpty(dialogueActor.actor))
            {
                Actor actorRecord = DialogueManager.MasterDatabase.GetActor(dialogueActor.actor);
                if (actorRecord != null)
                {
                    string displayNameFromDB = actorRecord.LookupValue("Display Name");
                    if (!string.IsNullOrEmpty(displayNameFromDB))
                    {
                        characterDisplayName = displayNameFromDB;
                    }
                    else
                    {
                        characterDisplayName = actorRecord.Name;
                    }
                }
            }
            characterNameText.text = characterDisplayName;
        }

        if (characterInventory != null) characterInventory.OnInventoryChanged += RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged += RefreshEquipmentSlot;

        InitializeInventorySlots();
        InitializeEquipmentSlots();
        RefreshEquipmentSlots();
    }

    // ... (rest of the script is unchanged) ...
    #region Unchanged Code
    private void OnDisable()
    {
        if (characterInventory != null) characterInventory.OnInventoryChanged -= RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged -= RefreshEquipmentSlot;
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
        foreach (Transform child in inventorySlotsParent)
        {
            Destroy(child.gameObject);
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

        RefreshInventorySlots();
    }

    private void RefreshInventorySlots()
    {
        if (characterInventory == null) return;
        for (int i = 0; i < uiInventorySlots.Count; i++)
        {
            if (i < characterInventory.items.Count)
            {
                uiInventorySlots[i].UpdateSlot(characterInventory.items[i]);
            }
            else
            {
                uiInventorySlots[i].UpdateSlot(null);
            }
        }
    }

    private void RefreshEquipmentSlots()
    {
        foreach (var slot in equipmentSlots)
        {
            RefreshEquipmentSlot(slot.slotType);
        }
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
    #endregion
}