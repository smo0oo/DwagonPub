using UnityEngine;
using System.Linq;

public class RankSelectionPanel : MonoBehaviour
{
    [Header("UI References")]
    public Transform contentParent;
    public GameObject rankEntryPrefab;

    private AbilityBookManager bookManager;
    private PlayerStats currentPlayerStats;

    public void Populate(SkillNode node, PlayerStats stats, AbilityBookManager manager)
    {
        bookManager = manager;
        currentPlayerStats = stats;

        // Clear any previous entries
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        stats.unlockedAbilityRanks.TryGetValue(node.skillRanks.FirstOrDefault(), out int currentRank);

        // Create an entry for each unlocked rank
        for (int i = 0; i < currentRank; i++)
        {
            Ability rankAbility = node.skillRanks[i];
            GameObject entryGO = Instantiate(rankEntryPrefab, contentParent);
            entryGO.GetComponent<RankEntryUI>().Initialize(rankAbility, this);
        }
    }

    // This method is a pass-through to the main managers
    public void ShowAbilityTooltip(Ability ability) => bookManager.ShowAbilityTooltip(ability);
    public void HideAbilityTooltip() => bookManager.HideAbilityTooltip();
    public void OnBeginDrag(IDragSource source, Sprite icon) => bookManager.OnBeginDrag(source, icon);
    public void OnEndDrag() => bookManager.OnEndDrag();

    // Close this panel if the player clicks away from it
    void Update()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(transform as RectTransform, Input.mousePosition))
            {
                bookManager.CloseRankSelection();
            }
        }
    }
}