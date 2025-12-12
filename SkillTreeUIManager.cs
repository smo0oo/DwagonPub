using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class SkillTreeUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject skillTreePanel;
    public TextMeshProUGUI skillPointsText;
    public Transform nodesContainer;
    public Transform linesContainer;
    public GameObject skillNodePrefab;
    public GameObject linePrefab;

    [Header("Layout Settings")]
    public float cellWidth = 150f;
    public float cellHeight = 120f;

    private PlayerStats currentPlayerStats;
    private Dictionary<SkillNode, SkillNodeUI> nodeUIMap = new Dictionary<SkillNode, SkillNodeUI>();
    private InventoryManager inventoryManager;
    private SkillNodeUI currentlyHoveredNode = null;

    void Awake()
    {
        inventoryManager = InventoryManager.instance;
    }

    public void DisplaySkillTree(PlayerStats stats)
    {
        if (currentPlayerStats != null)
        {
            currentPlayerStats.OnSkillPointsChanged -= RefreshAllNodeDisplays;
            currentPlayerStats.OnAbilitiesChanged -= RefreshAllNodeDisplays;
        }

        currentPlayerStats = stats;

        currentPlayerStats.OnSkillPointsChanged += RefreshAllNodeDisplays;
        currentPlayerStats.OnAbilitiesChanged += RefreshAllNodeDisplays;

        BuildTree();
    }

    // --- FIX: This method no longer controls the panel's active state. ---
    public void HideSkillTree()
    {
        if (currentPlayerStats != null)
        {
            currentPlayerStats.OnSkillPointsChanged -= RefreshAllNodeDisplays;
            currentPlayerStats.OnAbilitiesChanged -= RefreshAllNodeDisplays;
        }
        // The line "skillTreePanel.SetActive(false);" has been removed from this method.
    }

    private void BuildTree()
    {
        foreach (Transform child in nodesContainer) Destroy(child.gameObject);
        foreach (Transform child in linesContainer) Destroy(child.gameObject);
        nodeUIMap.Clear();

        if (currentPlayerStats == null || currentPlayerStats.characterClass.classSkillTree == null) return;

        foreach (SkillNode node in currentPlayerStats.characterClass.classSkillTree.skillNodes)
        {
            GameObject nodeGO = Instantiate(skillNodePrefab, nodesContainer);
            SkillNodeUI nodeUI = nodeGO.GetComponent<SkillNodeUI>();
            nodeUI.Initialize(node, this);

            RectTransform rect = nodeGO.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(node.gridPosition.x * cellWidth, -node.gridPosition.y * cellHeight);

            nodeUIMap[node] = nodeUI;
        }

        DrawConnectorLines();
        RefreshAllNodeDisplays();
    }

    private void DrawConnectorLines()
    {
        if (currentPlayerStats == null || currentPlayerStats.characterClass.classSkillTree == null) return;
        SkillTree tree = currentPlayerStats.characterClass.classSkillTree;

        foreach (SkillNode node in nodeUIMap.Keys)
        {
            if (node.prerequisiteIndex >= 0 && node.prerequisiteIndex < tree.skillNodes.Count)
            {
                SkillNode prerequisiteNode = tree.skillNodes[node.prerequisiteIndex];

                if (nodeUIMap.ContainsKey(prerequisiteNode))
                {
                    SkillNodeUI startNodeUI = nodeUIMap[prerequisiteNode];
                    SkillNodeUI endNodeUI = nodeUIMap[node];

                    GameObject lineGO = Instantiate(linePrefab, linesContainer);
                    RectTransform lineRect = lineGO.GetComponent<RectTransform>();

                    Vector2 startPos = startNodeUI.GetComponent<RectTransform>().anchoredPosition;
                    Vector2 endPos = endNodeUI.GetComponent<RectTransform>().anchoredPosition;

                    Vector2 direction = (endPos - startPos).normalized;
                    float distance = Vector2.Distance(startPos, endPos);

                    lineRect.anchoredPosition = (startPos + endPos) / 2f;
                    lineRect.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    lineRect.sizeDelta = new Vector2(distance, lineRect.sizeDelta.y);
                }
            }
        }
    }

    private void RefreshAllNodeDisplays()
    {
        if (currentPlayerStats == null) return;
        skillPointsText.text = $"Skill Points: {currentPlayerStats.unspentSkillPoints}";
        foreach (var uiNode in nodeUIMap.Values)
        {
            uiNode.UpdateDisplay(currentPlayerStats);
        }

        if (currentlyHoveredNode != null)
        {
            currentlyHoveredNode.ForceTooltipRefresh();
        }
    }

    public void AttemptToLearnSkill(SkillNode nodeToLearn)
    {
        if (currentPlayerStats == null) return;

        if (currentPlayerStats.unspentSkillPoints < 1) return;

        if (nodeToLearn.skillRanks.Count > 0)
        {
            currentPlayerStats.unlockedAbilityRanks.TryGetValue(nodeToLearn.skillRanks.FirstOrDefault(), out int currentRank);
            if (currentRank >= nodeToLearn.skillRanks.Count) return;
        }

        if (nodeToLearn.prerequisiteIndex >= 0)
        {
            if (currentPlayerStats.characterClass.classSkillTree == null) return;
            SkillTree tree = currentPlayerStats.characterClass.classSkillTree;
            if (nodeToLearn.prerequisiteIndex >= tree.skillNodes.Count) return;

            SkillNode prerequisiteNode = tree.skillNodes[nodeToLearn.prerequisiteIndex];

            if (prerequisiteNode.skillRanks.Count > 0)
            {
                currentPlayerStats.unlockedAbilityRanks.TryGetValue(prerequisiteNode.skillRanks.FirstOrDefault(), out int prereqRank);
                if (prereqRank < nodeToLearn.prerequisiteRank) return;
            }
        }

        currentPlayerStats.LearnSkill(nodeToLearn);
    }

    public void SetHoveredNode(SkillNodeUI node)
    {
        currentlyHoveredNode = node;
    }

    public void ClearHoveredNode()
    {
        currentlyHoveredNode = null;
        HideAbilityTooltip();
    }

    public void ShowAbilityTooltip(Ability currentRank, Ability nextRank)
    {
        if (TooltipManager.instance != null)
        {
            TooltipManager.instance.ShowAbilityTooltip(currentRank, nextRank);
        }
    }

    public void HideAbilityTooltip()
    {
        if (inventoryManager != null)
        {
            inventoryManager.HideTooltip();
        }
    }
}