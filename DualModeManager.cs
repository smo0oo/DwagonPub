using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

public class DualModeManager : MonoBehaviour
{
    public static DualModeManager instance;

    [Header("State")]
    public bool isDualModeActive = false;
    public bool isRescueMissionActive = false;

    [Header("Teams")]
    public List<int> dungeonTeamIndices = new List<int>();
    public List<int> wagonTeamIndices = new List<int>();

    [System.Serializable]
    public class FallenHeroData
    {
        public int memberIndex;
        public Vector3 position;
        public Quaternion rotation;
    }
    public List<FallenHeroData> fallenHeroes = new List<FallenHeroData>();

    [Header("Rescue Settings")]
    public GameObject soulOrbPrefab;

    [Header("Buffs & Loot")]
    public Ability pendingBossBuff;
    public List<ItemStack> dungeonLootBag = new List<ItemStack>();

    [Header("Scene Config")]
    public string defenseSceneName = "DomeBattle";
    private string pendingDungeonScene;
    private string pendingDungeonSpawn;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; DontDestroyOnLoad(gameObject); }
    }

    public void InitializeSplit(List<int> groupA, List<int> groupB, string domeScene)
    {
        isDualModeActive = true;
        isRescueMissionActive = false;
        dungeonTeamIndices = new List<int>(groupA);
        wagonTeamIndices = new List<int>(groupB);
        defenseSceneName = domeScene;
        pendingBossBuff = null;
        dungeonLootBag.Clear();
        fallenHeroes.Clear();
    }

    public void EndDualMode()
    {
        isDualModeActive = false;
        isRescueMissionActive = false;
        pendingBossBuff = null;

        if (InventoryManager.instance != null && dungeonLootBag.Count > 0)
        {
            Inventory targetInventory = null;
            if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
                targetInventory = PartyManager.instance.ActivePlayer.GetComponentInChildren<Inventory>();

            if (targetInventory == null)
            {
                var allInventories = InventoryManager.instance.GetAllPlayerInventories();
                if (allInventories != null && allInventories.Count > 0) targetInventory = allInventories[0];
            }

            if (targetInventory != null)
            {
                foreach (var item in dungeonLootBag)
                    if (item.itemData != null) targetInventory.AddItem(item.itemData, item.quantity);
            }
            dungeonLootBag.Clear();
        }

        dungeonTeamIndices.Clear();
        wagonTeamIndices.Clear();
        fallenHeroes.Clear();
    }

    public void StartRescueMission()
    {
        if (!isDualModeActive || PartyManager.instance == null) return;

        isRescueMissionActive = true;
        fallenHeroes.Clear();

        List<GameObject> allMembers = PartyManager.instance.partyMembers;
        foreach (int index in dungeonTeamIndices)
        {
            if (ValidateIndex(index))
            {
                fallenHeroes.Add(new FallenHeroData
                {
                    memberIndex = index,
                    position = allMembers[index].transform.position,
                    rotation = allMembers[index].transform.rotation
                });
            }
        }

        if (SceneStateManager.instance != null && InventoryManager.instance != null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            SceneStateManager.instance.CaptureSceneState(currentScene, InventoryManager.instance.worldItemPrefab);
        }

        if (GameManager.instance != null) GameManager.instance.ReloadCurrentLevel("Entrance");
    }

    public void ReviveMember(int index, Vector3 revivePosition)
    {
        if (!ValidateIndex(index)) return;

        GameObject member = PartyManager.instance.partyMembers[index];
        Health h = member.GetComponentInChildren<Health>();

        if (h != null)
        {
            member.SetActive(true);
            NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
            if (agent != null) agent.Warp(revivePosition);
            else member.transform.position = revivePosition;
            h.Revive(0.25f);
        }
        CheckRescueProgress();
    }

    public void CheckRescueProgress()
    {
        bool allRescued = true;
        List<GameObject> allMembers = PartyManager.instance.partyMembers;

        foreach (int index in dungeonTeamIndices)
        {
            Health h = allMembers[index].GetComponentInChildren<Health>();
            if (!allMembers[index].activeInHierarchy || (h != null && h.isDowned))
            {
                allRescued = false;
                break;
            }
        }

        if (allRescued)
        {
            Debug.Log("Rescue Successful! All members revived.");
            if (FloatingTextManager.instance != null && PartyManager.instance.ActivePlayer != null)
            {
                FloatingTextManager.instance.ShowEvent("Rescue Complete!", PartyManager.instance.ActivePlayer.transform.position + Vector3.up * 3);
            }
        }
    }

    public void ApplyTeamState(SceneType currentScene)
    {
        if (!isDualModeActive || PartyManager.instance == null) return;

        List<GameObject> allMembers = PartyManager.instance.partyMembers;

        if (currentScene == SceneType.Dungeon)
        {
            if (allMembers.Count > 0 && allMembers[0] != null) allMembers[0].SetActive(false);

            if (!isRescueMissionActive)
            {
                foreach (int index in dungeonTeamIndices) if (ValidateIndex(index)) allMembers[index].SetActive(true);
                foreach (int index in wagonTeamIndices) if (ValidateIndex(index)) allMembers[index].SetActive(false);
                if (dungeonTeamIndices.Count > 0) PartyManager.instance.SetActivePlayer(dungeonTeamIndices[0]);
            }
            else
            {
                foreach (int index in dungeonTeamIndices)
                {
                    if (ValidateIndex(index))
                    {
                        GameObject victim = allMembers[index];
                        Health h = victim.GetComponentInChildren<Health>();

                        if (h != null && h.isDowned)
                        {
                            victim.SetActive(false);
                            if (soulOrbPrefab != null)
                            {
                                var data = fallenHeroes.FirstOrDefault(f => f.memberIndex == index);
                                if (data != null)
                                {
                                    GameObject orb = Instantiate(soulOrbPrefab, data.position, Quaternion.identity);

                                    // --- SAFE ASSIGNMENT ---
                                    SoulOrb orbScript = orb.GetComponent<SoulOrb>();
                                    if (orbScript == null) orbScript = orb.GetComponentInChildren<SoulOrb>();

                                    if (orbScript != null) orbScript.memberIndexToRevive = index;
                                    // -----------------------
                                }
                            }
                        }
                        else
                        {
                            victim.SetActive(true);
                        }
                    }
                }

                foreach (int index in wagonTeamIndices) if (ValidateIndex(index)) allMembers[index].SetActive(true);
                if (wagonTeamIndices.Count > 0) PartyManager.instance.SetActivePlayer(wagonTeamIndices[0]);
            }
        }
        else if (currentScene == SceneType.DomeBattle)
        {
            if (allMembers.Count > 0 && allMembers[0] != null) allMembers[0].SetActive(false);
            foreach (int index in dungeonTeamIndices) if (ValidateIndex(index)) allMembers[index].SetActive(false);
            foreach (int index in wagonTeamIndices) if (ValidateIndex(index)) allMembers[index].SetActive(true);
            if (wagonTeamIndices.Count > 0) PartyManager.instance.SetActivePlayer(wagonTeamIndices[0]);
            else PartyManager.instance.SetActivePlayer(0);
        }
    }

    private bool ValidateIndex(int index) { return index >= 0 && index < PartyManager.instance.partyMembers.Count && PartyManager.instance.partyMembers[index] != null; }
    public void SetNextDungeonStep(string sceneName, string spawnPointID) { pendingDungeonScene = sceneName; pendingDungeonSpawn = spawnPointID; SwitchToDefense(); }
    private void SwitchToDefense() { if (GameManager.instance != null) { GameManager.instance.LoadLevel(defenseSceneName, "WagonCenter"); } }
    public void SwitchToDungeon() { if (string.IsNullOrEmpty(pendingDungeonScene)) { Debug.LogError("DualModeManager: Attempted to return to dungeon, but no scene was stored!"); return; } if (GameManager.instance != null) { GameManager.instance.LoadLevel(pendingDungeonScene, pendingDungeonSpawn); } }
}