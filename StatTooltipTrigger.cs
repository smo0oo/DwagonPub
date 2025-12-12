using UnityEngine;
using UnityEngine.EventSystems;

public class StatTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(5, 15)]
    public string tooltipText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        // --- THIS IS THE FIX ---
        // Changed the method call from the non-existent "ShowTooltip" to the correct "ShowSimpleTooltip".
        if (TooltipManager.instance != null && !string.IsNullOrEmpty(tooltipText))
        {
            TooltipManager.instance.ShowSimpleTooltip("", tooltipText);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.instance != null)
        {
            TooltipManager.instance.HideTooltip();
        }
    }
}
