using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Added CanvasGroup requirement for Raycast blocking during drag
[RequireComponent(typeof(CanvasGroup))]
public class HotbarSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IDropTarget, IDragSource, IBeginDragHandler, IDragHandler, IEndDragHandler
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
    public int SlotIndex { get; private set; }

    private HotbarManager hotbarManager;
    private InventoryManager inventoryManager;
    private PlayerAbilityHolder abilityHolder;
    private PlayerStats playerStats;
    private Button button;
    private HotbarAssignment currentAssignment;
    private UIDragDropController dragDropController;
    private CanvasGroup canvasGroup;

    private UIIconEffect _iconEffect;

    // --- IDragSource Implementation ---
    public object GetItem()
    {
        if (currentAssignment == null) return null;
        if (currentAssignment.type == HotbarAssignment.AssignmentType.Ability) return currentAssignment.ability;
        if (currentAssignment.type == HotbarAssignment.AssignmentType.Item)
        {
            if (hotbarManager != null && hotbarManager.playerInventory != null)
            {
                if (currentAssignment.inventorySlotIndex < hotbarManager.playerInventory.items.Count)
                    return hotbarManager.playerInventory.items[currentAssignment.inventorySlotIndex];
            }
        }
        return null;
    }

    public void OnDropSuccess(IDropTarget target)
    {
        if (target is TrashSlot) hotbarManager.ClearHotbarSlot(SlotIndex);
    }
    // ----------------------------------

    public void Initialize(HotbarManager manager, int index)
    {
        hotbarManager = manager;
        SlotIndex = index;
    }

    void Start()
    {
        if (hotbarManager == null) hotbarManager = HotbarManager.instance;
        inventoryManager = InventoryManager.instance;
        dragDropController = Object.FindFirstObjectByType<UIDragDropController>();
        canvasGroup = GetComponent<CanvasGroup>();

        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(() => TriggerSlot(null));

        if (iconImage != null)
        {
            _iconEffect = iconImage.GetComponent<UIIconEffect>();
            if (_iconEffect == null) Debug.LogError($"[Slot {SlotIndex}] 'UIIconEffect' missing!", this);
        }
    }

    // --- DRAG IMPLEMENTATION (NEW) ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Only drag if we actually have an assignment
        if (currentAssignment == null || currentAssignment.type == HotbarAssignment.AssignmentType.Unassigned) return;

        if (dragDropController != null && iconImage != null)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

            // Start the drag
            dragDropController.OnBeginDrag(this, iconImage.sprite);

            // Optional: Hide tooltip
            if (inventoryManager != null) inventoryManager.HideTooltip();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // INTENTIONALLY EMPTY
        // Movement is handled by UIDragDropController.LateUpdate
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        if (dragDropController != null) dragDropController.OnEndDrag();
    }

    // ---------------------------------

    void Update()
    {
        if (abilityHolder == null || currentAssignment == null || currentAssignment.type == HotbarAssignment.AssignmentType.Unassigned)
        {
            if (button != null) button.interactable = true;
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0;
            return;
        }

        Ability ability = currentAssignment.ability;
        if (ability == null)
        {
            SetVisualState(usableColor, 0f, true);
            return;
        }

        bool hasEnoughMana = playerStats != null && playerStats.currentMana >= ability.manaCost;
        bool hasCorrectWeapon = ability.requiresWeaponType ? abilityHolder.IsCorrectWeaponEquipped(ability.requiredWeaponCategories) : true;

        if (!hasEnoughMana || !hasCorrectWeapon)
        {
            SetVisualState(requirementNotMetColor, 1f, false);
            return;
        }

        bool onCooldown = abilityHolder.GetCooldownStatus(ability, out float remaining);
        bool onGlobalCooldown = ability.triggersGlobalCooldown && abilityHolder.IsOnGlobalCooldown();
        bool isLockedByOtherAbility = HotbarManager.instance != null && HotbarManager.instance.LockingAbility != null && HotbarManager.instance.LockingAbility != ability;

        float fill = 0f;
        if (onCooldown) fill = remaining / ability.cooldown;

        if (onCooldown || onGlobalCooldown || isLockedByOtherAbility || IsOutOfRange)
        {
            SetVisualState(temporarilyUnusableColor, 1f, false);
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = fill;
        }
        else
        {
            SetVisualState(usableColor, 0f, true);
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0;
        }
    }

    private void SetVisualState(Color tint, float desaturation, bool interactable)
    {
        if (iconImage != null) iconImage.color = tint;
        if (_iconEffect != null) _iconEffect.SetDesaturation(desaturation);
        if (button != null) button.interactable = interactable;
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
        if (InventoryUIController.instance == null) return;

        PlayerAbilityHolder activeAbilityHolder = InventoryUIController.instance.ActivePlayerAbilityHolder;
        Inventory activeInventory = InventoryUIController.instance.ActivePlayerInventory;
        GameObject currentPlayer = InventoryUIController.instance.ActivePlayer;

        if (currentAssignment == null || activeAbilityHolder == null || currentPlayer == null) return;
        if (HotbarManager.instance.LockingAbility != null && HotbarManager.instance.LockingAbility != currentAssignment.ability) return;

        PlayerMovement playerMovement = currentPlayer.GetComponent<PlayerMovement>();

        if (currentAssignment.type == HotbarAssignment.AssignmentType.Item)
        {
            if (activeInventory != null && currentAssignment.inventorySlotIndex < activeInventory.items.Count)
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
        }
        else if (currentAssignment.type == HotbarAssignment.AssignmentType.Ability)
        {
            Ability abilityToUse = currentAssignment.ability;
            if (abilityToUse == null) return;

            switch (abilityToUse.abilityType)
            {
                case AbilityType.Charge:
                    GameObject chargeTarget = hoverTarget != null ? hoverTarget : playerMovement?.TargetObject;
                    if (chargeTarget != null && playerMovement != null) playerMovement.InitiateCharge(chargeTarget, abilityToUse);
                    break;
                case AbilityType.TargetedProjectile:
                case AbilityType.TargetedMelee:
                    GameObject finalTarget = hoverTarget != null ? hoverTarget : playerMovement?.TargetObject;
                    if (finalTarget != null && playerMovement != null) playerMovement.StartFollowingTarget(finalTarget, abilityToUse);
                    break;
                case AbilityType.DirectionalMelee:
                    activeAbilityHolder.UseAbility(abilityToUse, currentPlayer);
                    break;
                case AbilityType.GroundAOE:
                case AbilityType.GroundPlacement:
                case AbilityType.Leap:
                case AbilityType.Teleport:
                    if (TargetingController.instance != null)
                    {
                        if (TargetingController.instance.IsTargeting && TargetingController.instance.GetCurrentTargetingAbility() == abilityToUse)
                            TargetingController.instance.ConfirmTargetingWithKey();
                        else
                        {
                            if (playerMovement != null) playerMovement.IsGroundTargeting = true;
                            if (abilityToUse.locksPlayerActivity) HotbarManager.instance.LockingAbility = abilityToUse;
                            TargetingController.instance.StartTargeting(abilityToUse, activeAbilityHolder);
                        }
                    }
                    break;
                case AbilityType.ChanneledBeam:
                    activeAbilityHolder.UseAbility(abilityToUse, (GameObject)null);
                    break;
                case AbilityType.Self:
                case AbilityType.ForwardProjectile:
                    if (abilityToUse.abilityType == AbilityType.ForwardProjectile) activeAbilityHolder.UseAbility(abilityToUse, (GameObject)null);
                    else activeAbilityHolder.UseAbility(abilityToUse, currentPlayer);
                    break;
            }
        }
    }

    public void SetKeybindText(string text) { if (keybindText != null) { keybindText.text = text; } }
    public void LinkToPlayer(PlayerAbilityHolder newHolder, PlayerStats newStats) { this.abilityHolder = newHolder; this.playerStats = newStats; }
    public void Assign(HotbarAssignment assignment) => currentAssignment = assignment;

    public void UpdateSlot(HotbarAssignment assignment)
    {
        currentAssignment = assignment;

        if (_iconEffect != null) _iconEffect.SetDesaturation(0f);

        if (assignment == null || assignment.type == HotbarAssignment.AssignmentType.Unassigned)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.clear;
                iconImage.enabled = true;
                if (_iconEffect != null) _iconEffect.SetTexture(null);
            }
            if (quantityText != null) { quantityText.text = ""; quantityText.enabled = false; }
            return;
        }

        if (assignment.type == HotbarAssignment.AssignmentType.Item)
        {
            ItemStack stack = null;
            if (hotbarManager != null && hotbarManager.playerInventory != null)
            {
                if (assignment.inventorySlotIndex < hotbarManager.playerInventory.items.Count)
                    stack = hotbarManager.playerInventory.items[assignment.inventorySlotIndex];
            }

            if (stack != null && stack.itemData != null)
            {
                if (iconImage != null)
                {
                    iconImage.sprite = stack.itemData.icon;
                    iconImage.color = usableColor;
                    iconImage.enabled = true;
                    if (_iconEffect != null && stack.itemData.icon != null)
                        _iconEffect.SetTexture(stack.itemData.icon.texture);
                }
                if (quantityText != null)
                {
                    quantityText.text = stack.quantity > 1 ? stack.quantity.ToString() : "";
                    quantityText.enabled = stack.quantity > 1;
                }
            }
            else
            {
                if (iconImage != null)
                {
                    iconImage.sprite = null;
                    iconImage.color = Color.clear;
                    iconImage.enabled = true;
                    if (_iconEffect != null) _iconEffect.SetTexture(null);
                }
                if (quantityText != null) { quantityText.text = ""; quantityText.enabled = false; }
            }
        }
        else if (assignment.type == HotbarAssignment.AssignmentType.Ability)
        {
            if (assignment.ability != null)
            {
                if (iconImage != null)
                {
                    iconImage.sprite = assignment.ability.icon;
                    iconImage.color = usableColor;
                    iconImage.enabled = true;
                    if (_iconEffect != null && assignment.ability.icon != null)
                        _iconEffect.SetTexture(assignment.ability.icon.texture);
                }
                if (quantityText != null) { quantityText.text = ""; quantityText.enabled = false; }
            }
            else
            {
                if (iconImage != null)
                {
                    iconImage.sprite = null;
                    iconImage.color = Color.clear;
                    iconImage.enabled = true;
                    if (_iconEffect != null) _iconEffect.SetTexture(null);
                }
            }
        }
    }

    public void UpdateSlot(ItemStack itemStack)
    {
        if (_iconEffect != null) _iconEffect.SetDesaturation(0f);
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0;

        if (itemStack != null && itemStack.itemData != null && itemStack.quantity > 0)
        {
            iconImage.sprite = itemStack.itemData.icon;
            iconImage.color = usableColor;
            iconImage.enabled = true;

            if (_iconEffect != null && itemStack.itemData.icon != null)
                _iconEffect.SetTexture(itemStack.itemData.icon.texture);

            quantityText.text = itemStack.quantity > 1 ? itemStack.quantity.ToString() : "";
            quantityText.enabled = true;
        }
        else
        {
            iconImage.sprite = null; iconImage.color = Color.clear; iconImage.enabled = true;
            quantityText.text = ""; quantityText.enabled = false;
            if (_iconEffect != null) _iconEffect.SetTexture(null);
        }
    }

    public void UpdateSlot(Ability ability)
    {
        if (_iconEffect != null) _iconEffect.SetDesaturation(0f);
        quantityText.text = ""; quantityText.enabled = false;
        if (ability != null)
        {
            iconImage.sprite = ability.icon;
            iconImage.color = usableColor;
            iconImage.enabled = true;

            if (_iconEffect != null && ability.icon != null)
                _iconEffect.SetTexture(ability.icon.texture);
        }
        else
        {
            iconImage.sprite = null; iconImage.color = Color.clear; iconImage.enabled = true;
            if (_iconEffect != null) _iconEffect.SetTexture(null);
        }
    }

    public bool CanReceiveDrop(object item)
    {
        if (SlotIndex == 0) return false;
        if (item is ItemStack itemStack) return itemStack.itemData.itemType == ItemType.Consumable;
        return item is Ability;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (dragDropController != null && dragDropController.currentSource != null)
        {
            object draggedItem = dragDropController.currentSource.GetItem();
            if (CanReceiveDrop(draggedItem))
            {
                OnDrop(draggedItem);
                dragDropController.NotifyDropSuccessful(this);
            }
        }
    }

    public void OnDrop(object item)
    {
        int hotbarIndex = transform.GetSiblingIndex();
        if (item is ItemStack)
        {
            if (dragDropController.currentSource is InventorySlot sourceSlot)
            {
                hotbarManager.SetHotbarSlotWithItem(hotbarIndex, sourceSlot.slotIndex);
            }
        }
        else if (item is Ability ability)
        {
            hotbarManager.SetHotbarSlotWithAbility(hotbarIndex, ability);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (SlotIndex != 0 && currentAssignment != null && currentAssignment.type != HotbarAssignment.AssignmentType.Unassigned)
            {
                hotbarManager.ClearHotbarSlot(transform.GetSiblingIndex());
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventoryManager == null || currentAssignment == null) return;

        bool isDragging = dragDropController != null && dragDropController.currentSource != null;
        if (isDragging) return;

        if (currentAssignment.type == HotbarAssignment.AssignmentType.Item)
        {
            if (hotbarManager != null && hotbarManager.playerInventory != null && currentAssignment.inventorySlotIndex >= 0)
            {
                if (currentAssignment.inventorySlotIndex < hotbarManager.playerInventory.items.Count)
                {
                    ItemStack item = hotbarManager.playerInventory.items[currentAssignment.inventorySlotIndex];
                    inventoryManager.ShowTooltipForExternalItem(item, playerStats);
                }
            }
        }
        else if (currentAssignment.type == HotbarAssignment.AssignmentType.Ability)
        {
            inventoryManager.ShowTooltipForAbility(currentAssignment.ability);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryManager != null) inventoryManager.HideTooltip();
    }
}