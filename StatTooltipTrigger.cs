using UnityEngine;
using UnityEngine.EventSystems;

public class StatTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip Content")]
    public string title; // --- NEW: Title for the AAA Header ---

    [TextArea(5, 15)]
    public string tooltipText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.instance != null && !string.IsNullOrEmpty(tooltipText))
        {
            // Pass both Title and Body to the manager
            TooltipManager.instance.ShowSimpleTooltip(title, tooltipText);
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