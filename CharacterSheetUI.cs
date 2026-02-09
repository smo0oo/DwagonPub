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

    // The specific components for THIS character sheet
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

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks/errors when menu closes
        if (characterInventory != null) characterInventory.OnInventoryChanged -= RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged -= RefreshEquipmentSlot;
    }

    public void DisplayCharacter(GameObject playerObject)
    {
        // 1. Cleanup previous subscriptions
        if (characterInventory != null) characterInventory.OnInventoryChanged -= RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged -= RefreshEquipmentSlot;

        // 2. Get Roots
        CharacterRoot root = playerObject.GetComponent<CharacterRoot>();
        if (root == null)
        {
            // Fallback for older prefabs
            root = playerObject.GetComponentInChildren<CharacterRoot>();
        }

        if (root == null) return;

        // 3. Assign Components
        characterInventory = root.Inventory;
        characterEquipment = root.PlayerEquipment;
        characterStats = root.PlayerStats;

        // 4. Update Name
        if (characterNameText != null)
        {
            characterNameText.text = GetCharacterDisplayName(playerObject);
        }

        // 5. Subscribe & Refresh
        if (characterInventory != null) characterInventory.OnInventoryChanged += RefreshInventorySlots;
        if (characterEquipment != null) characterEquipment.OnEquipmentChanged += RefreshEquipmentSlot;

        InitializeInventorySlots();
        InitializeEquipmentSlots();
        RefreshEquipmentSlots();
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
            // Link slot to this SPECIFIC character's equipment
            slot.Initialize(mainEquipmentManager, characterEquipment);
        }
    }

    private void InitializeInventorySlots()
    {
        // Clear old slots
        foreach (Transform child in inventorySlotsParent) Destroy(child.gameObject);
        uiInventorySlots.Clear();

        if (characterInventory == null) return;

        // Create new slots linked to THIS character's inventory
        for (int i = 0; i < characterInventory.inventorySize; i++)
        {
            GameObject slotGO = Instantiate(inventorySlotPrefab, inventorySlotsParent);
            InventorySlot newSlot = slotGO.GetComponent<InventorySlot>();

            // Crucial: We pass 'characterInventory' here so the slot knows who owns it
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