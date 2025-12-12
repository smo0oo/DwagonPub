using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AbilityBookManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform abilitySlotsParent;
    public GameObject abilityBookNodePrefab;
    public RankSelectionPanel rankSelectionPanel;

    [Header("Settings")]
    public Vector2 panelOffset = new Vector2(5, 0);

    private List<AbilityBookNodeUI> spawnedNodes = new List<AbilityBookNodeUI>();
    private PlayerStats currentPlayerStats;
    private InventoryManager inventoryManager;
    private UIDragDropController dragDropController;

    void Start()
    {
        inventoryManager = InventoryManager.instance;
        dragDropController = FindAnyObjectByType<UIDragDropController>();
        if (rankSelectionPanel != null)
        {
            rankSelectionPanel.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (currentPlayerStats != null)
            currentPlayerStats.OnAbilitiesChanged += RefreshDisplayedAbilities;
    }

    private void OnDisable()
    {
        if (currentPlayerStats != null)
            currentPlayerStats.OnAbilitiesChanged -= RefreshDisplayedAbilities;
    }

    public void DisplayAbilities(PlayerStats playerStats)
    {
        if (currentPlayerStats != null)
            currentPlayerStats.OnAbilitiesChanged -= RefreshDisplayedAbilities;

        currentPlayerStats = playerStats;

        if (currentPlayerStats != null)
            currentPlayerStats.OnAbilitiesChanged += RefreshDisplayedAbilities;

        RefreshDisplayedAbilities();
    }

    private void RefreshDisplayedAbilities()
    {
        foreach (var nodeUI in spawnedNodes)
        {
            Destroy(nodeUI.gameObject);
        }
        spawnedNodes.Clear();
        CloseRankSelection();

        if (currentPlayerStats == null || currentPlayerStats.characterClass.classSkillTree == null) return;

        foreach (SkillNode node in currentPlayerStats.characterClass.classSkillTree.skillNodes)
        {
            if (node.skillRanks.Count > 0 && currentPlayerStats.unlockedAbilityRanks.ContainsKey(node.skillRanks.First()))
            {
                GameObject nodeGO = Instantiate(abilityBookNodePrefab, abilitySlotsParent);
                AbilityBookNodeUI nodeUI = nodeGO.GetComponent<AbilityBookNodeUI>();
                nodeUI.Initialize(node, currentPlayerStats, this);
                spawnedNodes.Add(nodeUI);
            }
        }
    }

    public void ShowRankSelectionFor(AbilityBookNodeUI selectedNodeUI, SkillNode skillNode)
    {
        foreach (var node in spawnedNodes)
        {
            if (node != selectedNodeUI)
            {
                node.SetDimmed(true);
            }
        }

        RectTransform selectedNodeRect = selectedNodeUI.GetComponent<RectTransform>();

        Vector3[] nodeCorners = new Vector3[4];
        selectedNodeRect.GetWorldCorners(nodeCorners);
        Vector3 spawnPosition = nodeCorners[2];

        spawnPosition.x += panelOffset.x;
        spawnPosition.y += panelOffset.y;

        rankSelectionPanel.transform.position = spawnPosition;

        rankSelectionPanel.gameObject.SetActive(true);
        rankSelectionPanel.Populate(skillNode, currentPlayerStats, this);
    }

    public void CloseRankSelection()
    {
        if (rankSelectionPanel != null)
        {
            rankSelectionPanel.gameObject.SetActive(false);
        }

        foreach (var node in spawnedNodes)
        {
            node.SetDimmed(false);
        }
    }

    public void ShowAbilityTooltip(Ability ability)
    {
        inventoryManager.ShowTooltipForAbility(ability);
    }

    public void HideAbilityTooltip()
    {
        inventoryManager.HideTooltip();
    }

    public void OnBeginDrag(IDragSource source, Sprite icon)
    {
        // We no longer close the panel here to prevent the drag bug
        dragDropController.OnBeginDrag(source, icon);
    }

    public void OnEndDrag()
    {
        dragDropController.OnEndDrag();
        // The panel is now closed AFTER the drag is complete
        CloseRankSelection();
    }
}