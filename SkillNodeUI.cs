using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.EventSystems;

public class SkillNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI rankText;
    public Button learnButton;
    public Image borderImage;

    [Header("State Colors")]
    public Color lockedColor = Color.grey;
    public Color availableColor = Color.yellow;
    public Color maxedColor = new Color(0.5f, 1f, 0.5f);

    private SkillNode representedNode;
    private SkillTreeUIManager manager;
    private PlayerStats lastKnownPlayerStats;

    public void Initialize(SkillNode node, SkillTreeUIManager uiManager)
    {
        representedNode = node;
        manager = uiManager;
        nameText.text = node.skillName;
        iconImage.sprite = node.skillIcon;
        learnButton.onClick.AddListener(OnLearnButtonClicked);
    }

    public void UpdateDisplay(PlayerStats playerStats)
    {
        lastKnownPlayerStats = playerStats;

        playerStats.unlockedAbilityRanks.TryGetValue(representedNode.skillRanks.FirstOrDefault(), out int currentRank);
        int maxRank = representedNode.skillRanks.Count;
        rankText.text = $"{currentRank} / {maxRank}";

        bool isMaxed = currentRank >= maxRank;
        bool canAfford = playerStats.unspentSkillPoints > 0;
        bool prereqMet = CheckPrerequisites(playerStats);

        if (isMaxed)
        {
            borderImage.color = maxedColor;
            learnButton.interactable = false;
        }
        else if (prereqMet)
        {
            borderImage.color = availableColor;
            learnButton.interactable = canAfford;
        }
        else
        {
            borderImage.color = lockedColor;
            learnButton.interactable = false;
        }
    }

    private bool CheckPrerequisites(PlayerStats playerStats)
    {
        if (representedNode.prerequisiteIndex < 0) return true;
        SkillTree tree = playerStats.characterClass.classSkillTree;
        if (tree == null || representedNode.prerequisiteIndex >= tree.skillNodes.Count) return false;
        SkillNode prerequisiteNode = tree.skillNodes[representedNode.prerequisiteIndex];
        Ability basePrereqAbility = prerequisiteNode.skillRanks.FirstOrDefault();
        if (basePrereqAbility == null) return true;
        playerStats.unlockedAbilityRanks.TryGetValue(basePrereqAbility, out int prereqCurrentRank);
        return prereqCurrentRank >= representedNode.prerequisiteRank;
    }

    private void OnLearnButtonClicked()
    {
        manager.AttemptToLearnSkill(representedNode);
    }

    public void ForceTooltipRefresh()
    {
        OnPointerEnter(null);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (manager == null || representedNode == null || representedNode.skillRanks.Count == 0 || lastKnownPlayerStats == null) return;

        manager.SetHoveredNode(this);

        lastKnownPlayerStats.unlockedAbilityRanks.TryGetValue(representedNode.skillRanks[0], out int currentRank);

        Ability currentAbility = null;
        if (currentRank > 0)
        {
            currentAbility = representedNode.skillRanks[currentRank - 1];
        }

        Ability nextAbility = null;
        if (currentRank < representedNode.skillRanks.Count)
        {
            nextAbility = representedNode.skillRanks[currentRank];
        }

        manager.ShowAbilityTooltip(currentAbility, nextAbility);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager != null)
        {
            manager.ClearHoveredNode();
        }
    }
}