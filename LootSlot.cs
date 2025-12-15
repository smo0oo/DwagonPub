using UnityEngine;
using UnityEngine.EventSystems;

public class LootSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ItemStack _stack;

    public void Initialize(ItemStack stack)
    {
        _stack = stack;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_stack == null || TooltipManager.instance == null) return;

        // 1. Find the viewer's stats (for DPS calculations in the tooltip)
        PlayerStats viewerStats = null;
        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
        {
            viewerStats = PartyManager.instance.ActivePlayer.GetComponent<PlayerStats>();
        }

        // 2. Check Requirements (Optional: Basic Level check)
        bool requirementsMet = true;
        if (viewerStats != null && _stack.itemData != null)
        {
            if (_stack.itemData.levelRequirement > viewerStats.currentLevel)
            {
                requirementsMet = false;
            }
            // Add class checks here if you want strict red text in the loot bag
        }

        // 3. Show the Tooltip
        TooltipManager.instance.ShowItemTooltip(_stack, requirementsMet, viewerStats);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.instance != null)
        {
            TooltipManager.instance.HideTooltip();
        }
    }

    private void OnDisable()
    {
        // Safety: Ensure tooltip hides if the bag is closed while hovering
        if (TooltipManager.instance != null)
        {
            TooltipManager.instance.HideTooltip();
        }
    }
}