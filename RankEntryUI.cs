using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class RankEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDragSource
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI rankNameText;

    private Ability representedAbility;
    private RankSelectionPanel manager;

    public void Initialize(Ability ability, RankSelectionPanel panelManager)
    {
        representedAbility = ability;
        manager = panelManager;

        iconImage.sprite = ability.icon;
        // Use the new displayName field
        rankNameText.text = ability.displayName;
    }

    public void OnPointerEnter(PointerEventData eventData) => manager.ShowAbilityTooltip(representedAbility);
    public void OnPointerExit(PointerEventData eventData) => manager.HideAbilityTooltip();
    public object GetItem() => representedAbility;
    public void OnDropSuccess(IDropTarget target) { }
    public void OnBeginDrag(PointerEventData eventData) => manager.OnBeginDrag(this, representedAbility.icon);
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) => manager.OnEndDrag();
}