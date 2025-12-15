using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using System;

public class DualModeManager : MonoBehaviour
{
    public static DualModeManager instance;

    public event Action OnLootBagChanged;

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

        OnLootBagChanged?.Invoke();
    }

    public void AddItemToLootBag(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0) return;

        if (item.isStackable)
        {
            foreach (var stack in dungeonLootBag)
            {
                if (stack.itemData == item && stack.quantity < item.maxStackSize)
                {
                    int space = item.maxStackSize - stack.quantity;
                    int add = Mathf.Min(space, quantity);
                    stack.quantity += add;
                    quantity -= add;
                    if (quantity <= 0) break;
                }
            }
        }

        if (quantity > 0)
        {
            dungeonLootBag.Add(new ItemStack(item, quantity));
        }

        Debug.Log($"Added {item.itemName} to Dungeon Loot Bag.");
        OnLootBagChanged?.Invoke();
    }

    // --- UPDATED: Part 1 - Transition only ---
    public void CompleteDungeonRun()
    {
        if (!isDualModeActive) return;

        Debug.Log("Dungeon Run Complete! Loading return scene (Loot Pending)...");

        // We DO NOT merge loot or end dual mode here anymore.
        // We just verify the destination and go there.

        if (GameManager.instance != null)
        {
            // Set the flag so the UI knows to open
            GameManager.instance.SetJustExitedDungeon(true);
            GameManager.instance.LoadLevel(defenseSceneName, "WagonCenter");
        }
    }

    // --- NEW: Part 2 - Called by LootBagUI ---
    public void FinalizeDungeonRun()
    {
        Debug.Log("Finalizing Dungeon Run: Merging loot and ending Dual Mode.");

        // 1. Merge Loot
        MergeLootToMainInventory();

        // 2. Apply Boss Buff
        if (pendingBossBuff != null)
        {
            ApplyBuffToTeam(wagonTeamIndices, pendingBossBuff);
            pendingBossBuff = null;
        }

        // 3. Clear the Exit Flag (UI is done)
        if (GameManager.instance != null)
        {
            GameManager.instance.SetJustExitedDungeon(false);
        }

        // 4. End Dual Mode
        EndDualMode();
    }
    // -----------------------------------------

    private void ReuniteTeams()
    {
        foreach (int index in dungeonTeamIndices)
        {
            if (!wagonTeamIndices.Contains(index))
            {
                wagonTeamIndices.Add(index);
            }
        }
        dungeonTeamIndices.Clear();
        fallenHeroes.Clear();
    }

    private void MergeLootToMainInventory()
    {
        if (InventoryManager.instance != null && dungeonLootBag.Count > 0)
        {
            Inventory targetInventory = null;

            // Try to find the Active Player's inventory
            if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
                targetInventory = PartyManager.instance.ActivePlayer.GetComponentInChildren<Inventory>();

            // Fallback to first available inventory
            if (targetInventory == null)
            {
                var allInventories = InventoryManager.instance.GetAllPlayerInventories();
                if (allInventories != null && allInventories.Count > 0) targetInventory = allInventories[0];
            }

            if (targetInventory != null)
            {
                foreach (var item in dungeonLootBag)
                {
                    if (item.itemData != null)
                    {
                        targetInventory.AddItem(item.itemData, item.quantity);
                        Debug.Log($"Secured Loot: {item.quantity}x {item.itemData.itemName}");
                    }
                }
            }
            dungeonLootBag.Clear();
            OnLootBagChanged?.Invoke();
        }
    }

    private void ApplyBuffToTeam(List<int> teamIndices, Ability buff)
    {
        if (PartyManager.instance == null) return;

        foreach (int index in teamIndices)
        {
            if (ValidateIndex(index))
            {
                GameObject member = PartyManager.instance.partyMembers[index];

                if (buff.friendlyEffects != null && buff.friendlyEffects.Count > 0)
                {
                    Debug.Log($"Applying Boss Reward '{buff.displayName}' to {member.name}");
                    foreach (var effect in buff.friendlyEffects)
                    {
                        effect.Apply(member, member);
                    }
                }
            }
        }
    }

    public void EndDualMode()
    {
        isDualModeActive = false;
        isRescueMissionActive = false;
        pendingBossBuff = null;
        dungeonLootBag.Clear();
        dungeonTeamIndices.Clear();
        wagonTeamIndices.Clear();
        fallenHeroes.Clear();
        OnLootBagChanged?.Invoke();

        Debug.Log("Dual Mode has ended. Returning to standard gameplay.");
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
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(revivePosition);
            }
            else
            {
                member.transform.position = revivePosition;
            }

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
            if (!allMembers[index].activeInHierarchy)
            {
                allRescued = false;
                break;
            }

            Health h = allMembers[index].GetComponentInChildren<Health>();
            if (h != null && h.isDowned)
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
                        var fallenData = fallenHeroes.FirstOrDefault(f => f.memberIndex == index);
                        bool wasKilled = (fallenData != null);

                        if (wasKilled)
                        {
                            if (h != null) h.ForceDownedState();
                            victim.SetActive(false);

                            if (soulOrbPrefab != null)
                            {
                                GameObject orb = Instantiate(soulOrbPrefab, fallenData.position, Quaternion.identity);
                                SoulOrb orbScript = orb.GetComponent<SoulOrb>();
                                if (orbScript == null) orbScript = orb.GetComponentInChildren<SoulOrb>();
                                if (orbScript != null) orbScript.memberIndexToRevive = index;
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

            foreach (int index in dungeonTeamIndices)
                if (ValidateIndex(index)) allMembers[index].SetActive(false);

            foreach (int index in wagonTeamIndices)
                if (ValidateIndex(index)) allMembers[index].SetActive(true);

            if (wagonTeamIndices.Count > 0)
                PartyManager.instance.SetActivePlayer(wagonTeamIndices[0]);
            else
                PartyManager.instance.SetActivePlayer(0);
        }
    }

    private bool ValidateIndex(int index) { return index >= 0 && index < PartyManager.instance.partyMembers.Count && PartyManager.instance.partyMembers[index] != null; }
    public void SetNextDungeonStep(string sceneName, string spawnPointID) { pendingDungeonScene = sceneName; pendingDungeonSpawn = spawnPointID; SwitchToDefense(); }
    private void SwitchToDefense() { if (GameManager.instance != null) { GameManager.instance.LoadLevel(defenseSceneName, "WagonCenter"); } }
    public void SwitchToDungeon() { if (string.IsNullOrEmpty(pendingDungeonScene)) { Debug.LogError("DualModeManager: Attempted to return to dungeon, but no scene was stored!"); return; } if (GameManager.instance != null) { GameManager.instance.LoadLevel(pendingDungeonScene, pendingDungeonSpawn); } }
}