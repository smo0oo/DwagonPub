using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Linq;

public class AbilityBookNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDragSource
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI rankText;
    public CanvasGroup canvasGroup;

    private SkillNode representedNode;
    private PlayerStats currentPlayerStats;
    private AbilityBookManager manager;
    private Ability highestRankAbility;

    public void Initialize(SkillNode node, PlayerStats stats, AbilityBookManager bookManager)
    {
        representedNode = node;
        currentPlayerStats = stats;
        manager = bookManager;

        // --- THIS IS THE FIX for the name text ---
        // It now uses the base skill name from the node data.
        nameText.text = node.skillName;

        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        if (currentPlayerStats == null || representedNode == null) return;

        currentPlayerStats.unlockedAbilityRanks.TryGetValue(representedNode.skillRanks.FirstOrDefault(), out int currentRank);
        int maxRank = representedNode.skillRanks.Count;

        if (currentRank > 0)
        {
            highestRankAbility = representedNode.skillRanks[currentRank - 1];
            iconImage.sprite = highestRankAbility.icon;
            rankText.text = $"{currentRank}/{maxRank}"; // Rank text is still correct
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void SetDimmed(bool isDimmed)
    {
        canvasGroup.alpha = isDimmed ? 0.25f : 1.0f;
        canvasGroup.interactable = !isDimmed;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Only open the panel if there's more than one rank to choose from.
        if (eventData.button == PointerEventData.InputButton.Right && representedNode.skillRanks.Count > 1)
        {
            manager.ShowRankSelectionFor(this, representedNode);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => manager.ShowAbilityTooltip(highestRankAbility);
    public void OnPointerExit(PointerEventData eventData) => manager.HideAbilityTooltip();
    public object GetItem() => highestRankAbility;
    public void OnDropSuccess(IDropTarget target) { }
    public void OnBeginDrag(PointerEventData eventData) => manager.OnBeginDrag(this, highestRankAbility.icon);
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) => manager.OnEndDrag();
}