using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;
using Cinemachine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Scene Transition")]
    public float fadeDuration = 0.5f;
    [Tooltip("The minimum time (in seconds) the loading screen will be visible, even if loading is instant.")]
    public float minimumLoadingScreenTime = 2.0f;

    [Header("Scene Management")]
    public string startingSceneName = "WorldMap_A";
    public string worldMapSceneName = "WorldMap_A";
    public List<ItemData> allItemsDatabase;

    [Header("State Tracking")]
    [Tooltip("The ID of the spawn point used to enter the current scene. Read by level managers to determine context (e.g. Ambush vs Camp).")]
    public string lastSpawnPointID;

    [Header("Persistent Objects")]
    public GameObject playerPartyObject;
    public CinemachineVirtualCamera worldMapCamera;

    [Header("UI Canvas Groups")]
    public CanvasGroup sharedCanvasGroup;
    public CanvasGroup battleCanvasGroup;
    public CanvasGroup worldMapCanvasGroup;
    public CanvasGroup inGameMenuCanvasGroup;
    public CanvasGroup wagonHotbarCanvasGroup;
    public CanvasGroup domeUICanvasGroup;

    [Header("UI Element Visibility")]
    public List<GameObject> worldMapHiddenElements;

    [Header("Core Scene (optional)")]
    public string coreSceneName = "CoreScene";
    public SceneType currentSceneType { get; private set; }
    private string currentLevelScene;
    public string lastKnownLocationNodeID { get; private set; }
    private bool isTransitioning;
    public bool IsTransitioning => isTransitioning;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        startingSceneName = NormalizeSceneName(startingSceneName);
        coreSceneName = NormalizeSceneName(coreSceneName);
        currentLevelScene = NormalizeSceneName(SceneManager.GetActiveScene().name);
    }

    public void ReturnToWorldMap()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToWorldMap());
    }

    private IEnumerator TransitionToWorldMap()
    {
        isTransitioning = true;
        float startTime = Time.realtimeSinceStartup;
        yield return LoadingScreenManager.instance.ShowLoadingScreen(fadeDuration);

        if (SceneStateManager.instance != null && FindAnyObjectByType<InventoryManager>() != null)
        {
            SceneStateManager.instance.CaptureSceneState(currentLevelScene, FindAnyObjectByType<InventoryManager>().worldItemPrefab);
        }

        yield return SwitchToSceneExact(worldMapSceneName);
        yield return null;

        WorldMapManager wmm = FindAnyObjectByType<WorldMapManager>();
        if (wmm != null && !string.IsNullOrEmpty(lastKnownLocationNodeID))
        {
            LocationNode targetNode = FindObjectsByType<LocationNode>(FindObjectsSortMode.None)
                .FirstOrDefault(n => n.locationName == lastKnownLocationNodeID);

            if (targetNode != null)
            {
                wmm.SetCurrentLocation(targetNode, true);
            }
            else
            {
                Debug.LogWarning($"Could not find location node with ID '{lastKnownLocationNodeID}' to return to.");
            }
        }

        // Restored Method Call
        SetUIVisibility("WorldMap");
        SetPlayerModelsActive(false);
        SetPlayerMovementComponentsActive(false);
        ApplySceneRules();
        FindAnyObjectByType<UIPartyPortraitsManager>()?.RefreshAllPortraits();

        float elapsedTime = Time.realtimeSinceStartup - startTime;
        if (elapsedTime < minimumLoadingScreenTime)
        {
            yield return new WaitForSeconds(minimumLoadingScreenTime - elapsedTime);
        }

        LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
        isTransitioning = false;
    }

    private void ApplySceneRules()
    {
        SceneInfo info = FindAnyObjectByType<SceneInfo>();
        currentSceneType = (info != null) ? info.type : SceneType.Dungeon;

        switch (currentSceneType)
        {
            case SceneType.Town:
                PartyManager.instance.SetPlayerSwitching(false);
                PartyAIManager.instance.EnterTownMode();
                break;
            case SceneType.DomeBattle:
                WagonHotbarManager wagonHotbar = FindAnyObjectByType<WagonHotbarManager>(FindObjectsInactive.Include);
                if (wagonHotbar != null)
                {
                    wagonHotbar.InitializeAndShow();
                }
                PartyManager.instance.SetPlayerSwitching(true);
                PartyAIManager.instance.EnterCombatMode();
                break;
            case SceneType.Dungeon:
                PartyManager.instance.SetPlayerSwitching(true);
                PartyAIManager.instance.EnterCombatMode();
                break;
            case SceneType.WorldMap:
                PartyManager.instance.SetPlayerSwitching(false);
                break;
        }
    }

    private IEnumerator TransitionLevel(string sceneName, string spawnPointID, string fromNodeID = null)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;

        float startTime = Time.realtimeSinceStartup;
        yield return LoadingScreenManager.instance.ShowLoadingScreen(fadeDuration);

        if (SceneStateManager.instance != null && FindAnyObjectByType<InventoryManager>() != null)
        {
            SceneStateManager.instance.CaptureSceneState(currentLevelScene, FindAnyObjectByType<InventoryManager>().worldItemPrefab);
        }

        yield return SwitchToSceneExact(sceneName);

        if (SceneStateManager.instance != null && FindAnyObjectByType<InventoryManager>() != null)
        {
            SceneStateManager.instance.RestoreSceneState(sceneName, FindAnyObjectByType<InventoryManager>().worldItemPrefab);
        }

        SceneInfo info = FindAnyObjectByType<SceneInfo>();
        if (info == null) { currentSceneType = SceneType.Dungeon; } else { currentSceneType = info.type; }

        if (currentSceneType == SceneType.WorldMap)
        {
            SetUIVisibility("WorldMap");
            SetPlayerModelsActive(false);
            SetPlayerMovementComponentsActive(false);
        }
        else if (sceneName == "MainMenu")
        {
            SetUIVisibility("MainMenu");
            SetPlayerModelsActive(false);
            SetPlayerMovementComponentsActive(false);
        }
        else
        {
            SetUIVisibility("InGame");
            if (!string.IsNullOrEmpty(fromNodeID)) { lastKnownLocationNodeID = fromNodeID; }
            SetPlayerModelsActive(true);
            SetPlayerMovementComponentsActive(true);

            // Restored Method Call
            MovePartyToSpawnPoint(spawnPointID);

            if (InventoryUIController.instance != null)
            {
                InventoryUIController.instance.RefreshAllPlayerDisplays(PartyManager.instance.ActivePlayer);
            }
        }

        ApplySceneRules();
        FindAnyObjectByType<UIPartyPortraitsManager>()?.RefreshAllPortraits();

        float elapsedTime = Time.realtimeSinceStartup - startTime;
        if (elapsedTime < minimumLoadingScreenTime)
        {
            yield return new WaitForSeconds(minimumLoadingScreenTime - elapsedTime);
        }

        LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
        isTransitioning = false;
    }

    private IEnumerator LoadGameSequence()
    {
        if (isTransitioning) yield break;
        isTransitioning = true;
        float startTime = Time.realtimeSinceStartup;
        yield return LoadingScreenManager.instance.ShowLoadingScreen(fadeDuration);

        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        if (!File.Exists(path))
        {
            isTransitioning = false;
            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < minimumLoadingScreenTime) yield return new WaitForSeconds(minimumLoadingScreenTime - elapsed);
            LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
            yield break;
        }

        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        yield return SwitchToSceneExact(startingSceneName);

        var allNodes = FindObjectsByType<LocationNode>(FindObjectsSortMode.None);
        var targetNode = allNodes.FirstOrDefault(n => n.locationName == data.currentLocationNodeID);

        if (targetNode == null)
        {
            isTransitioning = false;
            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < minimumLoadingScreenTime) yield return new WaitForSeconds(minimumLoadingScreenTime - elapsed);
            LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
            yield break;
        }

        string finalSceneToLoad = NormalizeSceneName(targetNode.sceneToLoad);
        if (finalSceneToLoad != startingSceneName) { yield return SwitchToSceneExact(finalSceneToLoad); }

        bool isWorldMapScene = finalSceneToLoad.StartsWith("WorldMap");

        PartyManager pm = PartyManager.instance;
        if (pm != null)
        {
            pm.partyLevel = data.partyLevel;
            pm.currentXP = data.currentXP;
            pm.xpToNextLevel = data.xpToNextLevel;
            pm.currencyGold = data.currencyGold;
        }

        if (isWorldMapScene)
        {
            var wmm = FindAnyObjectByType<WorldMapManager>();
            if (wmm != null)
            {
                wmm.timeOfDay = data.timeOfDay;
                wmm.SetCurrentLocation(targetNode, true);
            }
        }

        WagonResourceManager wagonMgr = FindAnyObjectByType<WagonResourceManager>();
        if (wagonMgr != null)
        {
            wagonMgr.currentFuel = data.wagonFuel;
            wagonMgr.currentRations = data.wagonRations;
            wagonMgr.currentIntegrity = data.wagonIntegrity;
            wagonMgr.OnResourcesChanged?.Invoke();
        }

        SetUIVisibility(isWorldMapScene ? "WorldMap" : "InGame");
        SetPlayerModelsActive(!isWorldMapScene);
        SetPlayerMovementComponentsActive(!isWorldMapScene);
        ApplySceneRules();

        List<GameObject> partyMembers = pm.partyMembers;
        string TrimClone(string s) => string.IsNullOrEmpty(s) ? s : s.Replace("(Clone)", "").Trim();

        foreach (CharacterSaveData charData in data.characterData)
        {
            GameObject playerGO = partyMembers.FirstOrDefault(p => p != null && (p.name == charData.characterPrefabID || TrimClone(p.name) == TrimClone(charData.characterPrefabID)));
            if (playerGO == null) continue;

            if (!isWorldMapScene)
            {
                var cc = playerGO.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                var agent = playerGO.GetComponent<NavMeshAgent>();
                if (agent != null) agent.Warp(charData.position);
                else playerGO.transform.position = charData.position;
                playerGO.transform.rotation = charData.rotation;
                if (cc) cc.enabled = true;
            }

            var root = playerGO.GetComponent<CharacterRoot>();
            var stats = root.PlayerStats;
            var inventory = root.Inventory;
            var equipment = root.PlayerEquipment;
            var health = root.Health;

            stats.unspentStatPoints = charData.unspentStatPoints;
            stats.bonusStrength = charData.bonusStrength;
            stats.bonusAgility = charData.bonusAgility;
            stats.bonusIntelligence = charData.bonusIntelligence;
            stats.bonusFaith = charData.bonusFaith;

            stats.unlockedAbilityRanks.Clear();
            for (int i = 0; i < charData.unlockedAbilityBaseIDs.Count; i++)
            {
                string abilityName = charData.unlockedAbilityBaseIDs[i];
                Ability baseAbility = stats.characterClass.classSkillTree.skillNodes
                    .Where(sn => sn.skillRanks.Count > 0 && sn.skillRanks[0].abilityName == abilityName)
                    .Select(sn => sn.skillRanks[0])
                    .FirstOrDefault();

                if (baseAbility != null)
                {
                    stats.unlockedAbilityRanks[baseAbility] = charData.unlockedAbilityRanks[i];
                }
            }

            inventory.items.Clear();
            for (int i = 0; i < inventory.inventorySize; i++)
            {
                if (charData.inventoryItems != null && i < charData.inventoryItems.Count)
                {
                    var itemSave = charData.inventoryItems[i];
                    var itemData = allItemsDatabase.FirstOrDefault(item => item.id == itemSave.itemID);
                    inventory.items.Add(new ItemStack(itemData, itemSave.quantity));
                }
                else
                {
                    inventory.items.Add(new ItemStack(null, 0));
                }
            }

            equipment.equippedItems.Clear();
            foreach (EquipmentType slot in System.Enum.GetValues(typeof(EquipmentType)))
            {
                equipment.equippedItems[slot] = null;
            }

            if (charData.equippedItems != null)
            {
                foreach (var kvp in charData.equippedItems)
                {
                    var itemData = allItemsDatabase.FirstOrDefault(item => item.id == kvp.Value.itemID);
                    if (itemData != null)
                    {
                        equipment.equippedItems[kvp.Key] = new ItemStack(itemData, kvp.Value.quantity);
                    }
                }
            }

            health.currentHealth = charData.currentHealth;
            stats.CalculateFinalStats();
        }

        if (InventoryUIController.instance != null)
        {
            InventoryUIController.instance.RefreshAllPlayerDisplays(PartyManager.instance.ActivePlayer);
        }

        float finalElapsed = Time.realtimeSinceStartup - startTime;
        if (finalElapsed < minimumLoadingScreenTime) yield return new WaitForSeconds(minimumLoadingScreenTime - finalElapsed);

        LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
        isTransitioning = false;
    }

    public void LoadLevel(string sceneName, string spawnPointID = null, string fromNodeID = null)
    {
        if (isTransitioning) return;
        lastSpawnPointID = spawnPointID;
        sceneName = NormalizeSceneName(sceneName);
        var alreadyLoaded = SceneManager.GetSceneByName(sceneName).isLoaded;
        if (alreadyLoaded && currentLevelScene == sceneName && SceneManager.GetActiveScene().name == sceneName) return;
        StartCoroutine(TransitionLevel(sceneName, spawnPointID, fromNodeID));
    }

    public void StartNewGame() { LoadLevel(startingSceneName); }

    public void LoadSavedGame()
    {
        if (isTransitioning)
        {
            Debug.Log("[GameManager] LoadSavedGame ignored: transition in progress.");
            return;
        }
        StartCoroutine(LoadGameSequence());
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        LoadLevel("MainMenu");
    }

    public void RegisterInitialScene(string sceneName)
    {
        currentLevelScene = NormalizeSceneName(sceneName);
    }

    private void SetPlayerModelsActive(bool isActive)
    {
        if (playerPartyObject == null) return;
        var allChildren = playerPartyObject.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child.CompareTag("PlayerModel"))
            {
                child.gameObject.SetActive(isActive);
            }
        }
    }

    private void SetPlayerMovementComponentsActive(bool isActive)
    {
        if (playerPartyObject == null) return;
        foreach (var agent in playerPartyObject.GetComponentsInChildren<NavMeshAgent>(true)) { agent.enabled = isActive; }
        foreach (var movement in playerPartyObject.GetComponentsInChildren<PlayerMovement>(true)) { movement.enabled = isActive; }
        foreach (var ai in playerPartyObject.GetComponentsInChildren<PartyMemberAI>(true)) { ai.enabled = isActive; }
        PartyAIManager partyAI = playerPartyObject.GetComponent<PartyAIManager>();
        if (partyAI != null) { partyAI.enabled = isActive; }
    }

    private static string NormalizeSceneName(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.EndsWith(".unity")) s = Path.GetFileNameWithoutExtension(s);
        int slash = s.LastIndexOf('/');
        if (slash >= 0 && slash < s.Length - 1) s = s[(slash + 1)..];
        return s;
    }

    // --- RESTORED HELPER METHOD: SetUIVisibility ---
    private void SetUIVisibility(string sceneType)
    {
        switch (sceneType)
        {
            case "WorldMap":
                ConfigureGroup(sharedCanvasGroup, true);
                ConfigureGroup(battleCanvasGroup, false);
                ConfigureGroup(worldMapCanvasGroup, true);
                ConfigureGroup(inGameMenuCanvasGroup, true, false);
                ConfigureGroup(wagonHotbarCanvasGroup, false);
                ConfigureGroup(domeUICanvasGroup, false);
                SetElementsVisibility(worldMapHiddenElements, false);
                if (worldMapCamera != null) worldMapCamera.gameObject.SetActive(true);
                break;
            case "InGame":
                ConfigureGroup(sharedCanvasGroup, true);
                ConfigureGroup(battleCanvasGroup, true);
                ConfigureGroup(worldMapCanvasGroup, false);
                ConfigureGroup(inGameMenuCanvasGroup, true, false);
                ConfigureGroup(wagonHotbarCanvasGroup, currentSceneType == SceneType.DomeBattle);
                ConfigureGroup(domeUICanvasGroup, currentSceneType == SceneType.DomeBattle);
                SetElementsVisibility(worldMapHiddenElements, true);
                if (worldMapCamera != null) worldMapCamera.gameObject.SetActive(false);
                break;
            case "MainMenu":
            default:
                ConfigureGroup(sharedCanvasGroup, false);
                ConfigureGroup(battleCanvasGroup, false);
                ConfigureGroup(worldMapCanvasGroup, false);
                ConfigureGroup(inGameMenuCanvasGroup, false);
                ConfigureGroup(wagonHotbarCanvasGroup, false);
                ConfigureGroup(domeUICanvasGroup, false);
                SetElementsVisibility(worldMapHiddenElements, false);
                if (worldMapCamera != null) worldMapCamera.gameObject.SetActive(false);
                break;
        }
    }

    // --- RESTORED HELPER METHOD: MovePartyToSpawnPoint ---
    private void MovePartyToSpawnPoint(string spawnPointID)
    {
        if (string.IsNullOrEmpty(spawnPointID)) return;
        PlayerSpawnPoint spawnPoint = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None)
            .FirstOrDefault(sp => sp.spawnPointID == spawnPointID);

        if (spawnPoint == null)
        {
            Debug.LogWarning($"Could not find spawn point with ID: {spawnPointID}");
            return;
        }

        PartyManager partyManager = PartyManager.instance;
        if (playerPartyObject == null || partyManager == null) return;

        playerPartyObject.transform.position = spawnPoint.transform.position;
        playerPartyObject.transform.rotation = spawnPoint.transform.rotation;

        foreach (GameObject member in partyManager.partyMembers)
        {
            if (member.TryGetComponent<NavMeshAgent>(out NavMeshAgent agent))
            {
                agent.enabled = false;
            }
            member.transform.localPosition = Vector3.zero;
            member.transform.localRotation = Quaternion.identity;
            if (agent != null)
            {
                agent.enabled = true;
            }
        }
    }

    private void ConfigureGroup(CanvasGroup group, bool visible, bool interactable = true)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible && interactable;
        group.blocksRaycasts = visible && interactable;
    }

    private void SetElementsVisibility(List<GameObject> elements, bool isVisible)
    {
        if (elements == null) return;
        foreach (var element in elements)
        {
            if (element != null) { element.SetActive(isVisible); }
        }
    }

    private IEnumerator SwitchToSceneExact(string sceneName)
    {
        sceneName = NormalizeSceneName(sceneName);
        var target = SceneManager.GetSceneByName(sceneName);
        bool targetAlreadyLoaded = target.isLoaded;
        var toUnload = new List<Scene>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            bool isCore = !string.IsNullOrEmpty(coreSceneName) && NormalizeSceneName(s.name) == coreSceneName;
            bool isTarget = NormalizeSceneName(s.name) == sceneName;
            if (!isCore && !isTarget) toUnload.Add(s);
        }

        foreach (var s in toUnload)
        {
            yield return SceneManager.UnloadSceneAsync(s);
        }

        if (!targetAlreadyLoaded)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            target = SceneManager.GetSceneByName(sceneName);
        }

        if (target.IsValid())
        {
            SceneManager.SetActiveScene(target);
        }
        currentLevelScene = sceneName;
        yield return null;
    }
}