using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HotbarSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IDropTarget
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI quantityText;
    public Image cooldownOverlay;
    public TextMeshProUGUI keybindText;

    [Header("State Colors")]
    public Color requirementNotMetColor = new Color(1f, 0.5f, 0.5f);
    public Color temporarilyUnusableColor = Color.gray;
    public Color usableColor = Color.white;

    public bool IsOutOfRange { get; set; } = false;
    public int SlotIndex { get; private set; } // For locked slot logic

    private HotbarManager hotbarManager;
    private InventoryManager inventoryManager;
    private PlayerAbilityHolder abilityHolder;
    private PlayerStats playerStats;
    private Button button;
    private HotbarAssignment currentAssignment;
    private UIDragDropController dragDropController;

    // Called by HotbarManager
    public void Initialize(int index)
    {
        SlotIndex = index;
    }

    void Start()
    {
        // Get singletons for robustness
        hotbarManager = HotbarManager.instance;
        inventoryManager = InventoryManager.instance;
        dragDropController = FindAnyObjectByType<UIDragDropController>();

        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(() => TriggerSlot(null));
    }

    void Update()
    {
        if (abilityHolder == null || currentAssignment == null || currentAssignment.type != HotbarAssignment.AssignmentType.Ability || currentAssignment.ability == null)
        {
            if (button != null) button.interactable = true;
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0;
            if (iconImage != null && iconImage.enabled)
            {
                iconImage.color = usableColor;
            }
            return;
        }

        Ability ability = currentAssignment.ability;

        bool hasEnoughMana = playerStats != null && playerStats.currentMana >= ability.manaCost;
        bool hasCorrectWeapon = true;
        if (ability.requiresWeaponType)
        {
            hasCorrectWeapon = abilityHolder.IsCorrectWeaponEquipped(ability.requiredWeaponCategories);
        }

        if (!hasEnoughMana || !hasCorrectWeapon)
        {
            iconImage.color = requirementNotMetColor;
            if (button != null) button.interactable = false;
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0;
            return;
        }

        bool onCooldown = abilityHolder.GetCooldownStatus(ability, out float remaining);
        bool onGlobalCooldown = ability.triggersGlobalCooldown && abilityHolder.IsOnGlobalCooldown();
        bool isLockedByOtherAbility = HotbarManager.instance != null && HotbarManager.instance.LockingAbility != null && HotbarManager.instance.LockingAbility != ability;

        if (onCooldown)
        {
            cooldownOverlay.fillAmount = remaining / ability.cooldown;
        }
        else
        {
            cooldownOverlay.fillAmount = 0;
        }

        bool isTemporarilyUnusable = onCooldown || onGlobalCooldown || isLockedByOtherAbility || IsOutOfRange;

        if (isTemporarilyUnusable)
        {
            iconImage.color = temporarilyUnusableColor;
            if (button != null) button.interactable = false;
        }
        else
        {
            iconImage.color = usableColor;
            if (button != null) button.interactable = true;
        }
    }

    public void TriggerSlot(GameObject hoverTarget)
    {
        if (button != null && button.interactable)
        {
            OnHotbarSlotClicked(hoverTarget);
        }
    }

    private void OnHotbarSlotClicked(GameObject hoverTarget)
    {
        PlayerAbilityHolder activeAbilityHolder = InventoryUIController.instance.ActivePlayerAbilityHolder;
        Inventory activeInventory = InventoryUIController.instance.ActivePlayerInventory;
        GameObject currentPlayer = InventoryUIController.instance.ActivePlayer;
        if (currentAssignment == null || activeAbilityHolder == null || currentPlayer == null) return;
        if (HotbarManager.instance.LockingAbility != null && HotbarManager.instance.LockingAbility != currentAssignment.ability) return;
        PlayerMovement playerMovement = currentPlayer.GetComponent<PlayerMovement>();

        if (currentAssignment.type == HotbarAssignment.AssignmentType.Item)
        {
            ItemStack itemStack = activeInventory.items[currentAssignment.inventorySlotIndex];
            if (itemStack != null && itemStack.itemData != null && itemStack.itemData.stats is ItemConsumableStats consumableStats)
            {
                Ability usageAbility = consumableStats.usageAbility;
                if (usageAbility != null)
                {
                    activeAbilityHolder.UseAbility(usageAbility, currentPlayer);
                    activeInventory.RemoveItem(currentAssignment.inventorySlotIndex, 1);
                }
            }
        }
        else if (currentAssignment.type == HotbarAssignment.AssignmentType.Ability)
        {
            Ability abilityToUse = currentAssignment.ability;
            switch (abilityToUse.abilityType)
            {
                case AbilityType.Charge:
                    GameObject chargeTarget = hoverTarget != null ? hoverTarget : playerMovement?.TargetObject;
                    if (chargeTarget != null && playerMovement != null)
                    {
                        playerMovement.InitiateCharge(chargeTarget, abilityToUse);
                    }
                    break;
                case AbilityType.TargetedProjectile:
                case AbilityType.TargetedMelee:
                    GameObject finalTarget = hoverTarget != null ? hoverTarget : playerMovement?.TargetObject;
                    if (finalTarget != null && playerMovement != null)
                    {
                        playerMovement.StartFollowingTarget(finalTarget, abilityToUse);
                    }
                    break;

                case AbilityType.DirectionalMelee:
                    // This type does not need a target and fires immediately.
                    activeAbilityHolder.UseAbility(abilityToUse, currentPlayer);
                    break;

                case AbilityType.GroundAOE:
                case AbilityType.GroundPlacement:
                case AbilityType.Leap:
                case AbilityType.Teleport:
                    if (TargetingController.instance != null && TargetingController.instance.IsTargeting)
                    {
                        TargetingController.instance.ConfirmTargetingWithKey();
                    }
                    else
                    {
                        if (playerMovement != null) playerMovement.IsGroundTargeting = true;
                        if (abilityToUse.locksPlayerActivity) HotbarManager.instance.LockingAbility = abilityToUse;
                        TargetingController.instance.StartTargeting(abilityToUse, activeAbilityHolder);
                    }
                    break;
                case AbilityType.ChanneledBeam:
                    activeAbilityHolder.UseAbility(abilityToUse, (GameObject)null);
                    break;
                case AbilityType.Self:
                case AbilityType.ForwardProjectile:
                    if (abilityToUse.abilityType == AbilityType.ForwardProjectile)
                    {
                        activeAbilityHolder.UseAbility(abilityToUse, (GameObject)null);
                    }
                    else
                    {
                        activeAbilityHolder.UseAbility(abilityToUse, currentPlayer);
                    }
                    break;
            }
        }
    }

    public void SetKeybindText(string text) { if (keybindText != null) { keybindText.text = text; } }
    public void LinkToPlayer(PlayerAbilityHolder newHolder, PlayerStats newStats) { this.abilityHolder = newHolder; this.playerStats = newStats; }
    public void Assign(HotbarAssignment assignment) => currentAssignment = assignment;
    public void UpdateSlot(ItemStack itemStack) { if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0; if (itemStack != null && itemStack.itemData != null && itemStack.quantity > 0) { iconImage.sprite = itemStack.itemData.icon; iconImage.enabled = true; iconImage.color = usableColor; quantityText.text = itemStack.quantity > 1 ? itemStack.quantity.ToString() : ""; quantityText.enabled = true; } else { iconImage.sprite = null; iconImage.enabled = false; quantityText.text = ""; quantityText.enabled = false; } }
    public void UpdateSlot(Ability ability) { quantityText.text = ""; quantityText.enabled = false; if (ability != null) { iconImage.sprite = ability.icon; iconImage.enabled = true; } else { iconImage.sprite = null; iconImage.enabled = false; } }

    public bool CanReceiveDrop(object item)
    {
        // Prevents dropping on the locked default attack slot
        if (SlotIndex == 0) return false;

        if (item is ItemStack itemStack)
        {
            return itemStack.itemData.itemType == ItemType.Consumable;
        }
        return item is Ability;
    }

    public void OnDrop(PointerEventData eventData) { if (dragDropController == null || dragDropController.currentSource == null) return; object draggedItem = dragDropController.currentSource.GetItem(); if (CanReceiveDrop(draggedItem)) { OnDrop(draggedItem); dragDropController.NotifyDropSuccessful(this); } }
    public void OnDrop(object item) { int hotbarIndex = transform.GetSiblingIndex(); if (item is ItemStack) { var source = dragDropController.currentSource; if (source is InventorySlot sourceSlot) { hotbarManager.SetHotbarSlotWithItem(hotbarIndex, sourceSlot.slotIndex); } } else if (item is Ability ability) { hotbarManager.SetHotbarSlotWithAbility(hotbarIndex, ability); } }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Prevents clearing the locked default attack slot
            if (SlotIndex != 0 && currentAssignment != null && currentAssignment.type != HotbarAssignment.AssignmentType.Unassigned)
            {
                HotbarManager.instance.ClearHotbarSlot(transform.GetSiblingIndex());
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { if (inventoryManager == null || currentAssignment == null) return; if (currentAssignment.type == HotbarAssignment.AssignmentType.Item) { if (currentAssignment.inventorySlotIndex >= 0) { Inventory activeInventory = InventoryUIController.instance.ActivePlayerInventory; if (activeInventory != null) { ItemStack item = activeInventory.items[currentAssignment.inventorySlotIndex]; inventoryManager.ShowTooltipForExternalItem(item, playerStats); } } } else if (currentAssignment.type == HotbarAssignment.AssignmentType.Ability) { inventoryManager.ShowTooltipForAbility(currentAssignment.ability); } }
    public void OnPointerExit(PointerEventData eventData) { if (inventoryManager != null) { inventoryManager.HideTooltip(); } }
}