using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;
using Cinemachine;
using UnityEngine.Splines;

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
    [Tooltip("The ID of the spawn point used to enter the current scene.")]
    public string lastSpawnPointID;

    [Tooltip("The scene we came from. Used by Dungeon Exits to return to the correct Hub (Town/Dome).")]
    public string previousSceneName;

    [Tooltip("The type of the Location Node we most recently entered.")]
    public NodeType lastLocationType;

    [Tooltip("Flag to suppress Dual Mode UI when returning from a dungeon run.")]
    public bool justExitedDungeon = false;

    [Header("Travel State")]
    public string lastKnownLocationNodeID;
    public string lastSplineContainerName;
    public float lastSplineProgress;
    public bool lastSplineReverse;
    public string lastLongTermDestinationID; // Tracks final target of multi-leg trip

    [Header("Tithe Settings (Credits)")]
    [Tooltip("Default Fuel ADDED to the wagon upon entering a town if SceneInfo is missing.")]
    public int titheFuelCredit = 50;
    [Tooltip("Default Rations ADDED to the wagon upon entering a town if SceneInfo is missing.")]
    public int titheRationsCredit = 50;

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

    [Header("Core Scene")]
    public string coreSceneName = "CoreScene";
    public SceneType currentSceneType { get; private set; }
    private string currentLevelScene;

    private bool isTransitioning;
    public bool IsTransitioning => isTransitioning;

    private bool isDebugExpanded = true;

    void OnGUI()
    {
        if (!Debug.isDebugBuild) return;

        float height = isDebugExpanded ? 250 : 30;
        GUILayout.BeginArea(new Rect(10, 10, 350, height));
        if (GUILayout.Button(isDebugExpanded ? "Game State Debugger (▼)" : "Game State Debugger (▶)"))
        {
            isDebugExpanded = !isDebugExpanded;
        }

        if (isDebugExpanded)
        {
            GUI.Box(new Rect(0, 25, 350, 225), "");
            GUILayout.Label($"Scene: {currentLevelScene} ({currentSceneType})");
            GUILayout.Label($"Return Point: {previousSceneName}");
            GUILayout.Label($"Last Loc Type: {lastLocationType}");
            GUILayout.Label($"Just Exited Dungeon: {justExitedDungeon}");
            GUILayout.Label($"Long Term Dest: {lastLongTermDestinationID}");

            if (DualModeManager.instance != null)
            {
                var dmm = DualModeManager.instance;
                string activeColor = dmm.isDualModeActive ? "green" : "grey";
                GUILayout.Label($"Dual Mode: <color={activeColor}>{dmm.isDualModeActive}</color>");

                if (dmm.isDualModeActive)
                {
                    GUILayout.Label($"Rescue: {dmm.isRescueMissionActive}");
                    GUILayout.Label($"Bag: {dmm.dungeonLootBag.Count} items");
                    GUILayout.Label($"Dungeon Team: {dmm.dungeonTeamIndices.Count}");
                    GUILayout.Label($"Wagon Team: {dmm.wagonTeamIndices.Count}");
                }
            }
            else
            {
                GUILayout.Label("DualModeManager: <color=red>MISSING</color>");
            }
        }
        GUILayout.EndArea();
    }

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

    public void SetLocationType(NodeType type)
    {
        lastLocationType = type;
        justExitedDungeon = false;
        Debug.Log($"GameManager: Location Type updated to {lastLocationType}");
    }

    public void SetJustExitedDungeon(bool state)
    {
        justExitedDungeon = state;
    }

    public void CaptureWagonState()
    {
        WagonController wagon = FindAnyObjectByType<WagonController>();
        WorldMapManager wmm = FindAnyObjectByType<WorldMapManager>();

        if (wagon != null && wagon.CurrentSpline != null)
        {
            lastSplineContainerName = wagon.CurrentSpline.gameObject.name;
            lastSplineProgress = wagon.TravelProgress;
            lastSplineReverse = wagon.IsReversing;
            lastKnownLocationNodeID = null;

            if (wmm != null)
            {
                var dest = wmm.GetFinalDestination();
                if (dest != null)
                {
                    lastLongTermDestinationID = dest.locationName;
                    Debug.Log($"[GameManager] Captured Long Term Destination: {lastLongTermDestinationID}");
                }
                else
                {
                    lastLongTermDestinationID = null;
                }
            }
        }
        else
        {
            lastSplineContainerName = null;
            lastLongTermDestinationID = null;
        }
    }

    public void ReturnToWorldMap()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToWorldMap());
    }

    private IEnumerator TransitionToWorldMap()
    {
        isTransitioning = true;

        NodeType typeBeforeExit = lastLocationType;
        string sceneBeforeExit = currentLevelScene;

        lastLocationType = NodeType.Scene;
        float startTime = Time.realtimeSinceStartup;
        yield return LoadingScreenManager.instance.ShowLoadingScreen(fadeDuration);

        if (SceneStateManager.instance != null && FindAnyObjectByType<InventoryManager>() != null)
        {
            SceneStateManager.instance.CaptureSceneState(currentLevelScene, FindAnyObjectByType<InventoryManager>().worldItemPrefab);
        }

        previousSceneName = currentLevelScene;

        yield return SwitchToSceneExact(worldMapSceneName);
        yield return null;

        WorldMapManager wmm = FindAnyObjectByType<WorldMapManager>();
        WagonController wagon = FindAnyObjectByType<WagonController>();
        bool positionRestored = false;

        if (wagon != null && !string.IsNullOrEmpty(lastSplineContainerName))
        {
            GameObject splineObj = GameObject.Find(lastSplineContainerName);
            if (splineObj != null && splineObj.TryGetComponent<SplineContainer>(out var spline))
            {
                if (wmm != null) wmm.RestoreJourneyState(spline, lastSplineProgress, lastSplineReverse);
                else wagon.RestorePosition(spline, lastSplineProgress, lastSplineReverse);
                positionRestored = true;
            }
        }

        if (!positionRestored && wmm != null && !string.IsNullOrEmpty(lastKnownLocationNodeID))
        {
            LocationNode targetNode = FindObjectsByType<LocationNode>(FindObjectsSortMode.None)
                .FirstOrDefault(n => n.locationName == lastKnownLocationNodeID);
            if (targetNode != null) wmm.SetCurrentLocation(targetNode, true);
        }

        if (wmm != null)
        {
            bool isAmbushOrCamp = sceneBeforeExit.Contains("DomeBattle");
            bool isDungeonRun = typeBeforeExit == NodeType.Scene || typeBeforeExit == NodeType.DualModeLocation;

            if (isAmbushOrCamp || isDungeonRun)
            {
                Debug.Log($"TransitionToWorldMap: Returning from {sceneBeforeExit} ({typeBeforeExit}). Advancing time to Dawn (6:00).");
                wmm.timeOfDay = 6f;
            }
        }

        SetUIVisibility("WorldMap");
        SetPlayerModelsActive(false);
        SetPlayerMovementComponentsActive(false);
        ApplySceneRules();
        FindAnyObjectByType<UIPartyPortraitsManager>()?.RefreshAllPortraits();

        float elapsedTime = Time.realtimeSinceStartup - startTime;
        if (elapsedTime < minimumLoadingScreenTime) yield return new WaitForSeconds(minimumLoadingScreenTime - elapsedTime);

        LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
        isTransitioning = false;
    }

    public void ApplySceneRules()
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
                if (wagonHotbar != null) wagonHotbar.InitializeAndShow();
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

        if (currentSceneType == SceneType.WorldMap)
        {
            CaptureWagonState();
        }

        if (currentLevelScene != sceneName)
        {
            if (currentSceneType == SceneType.Town ||
                currentSceneType == SceneType.DomeBattle ||
                currentSceneType == SceneType.WorldMap)
            {
                previousSceneName = currentLevelScene;
            }
        }

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

            // 1. Position the party FIRST
            MovePartyToSpawnPoint(spawnPointID);

            if (InventoryUIController.instance != null) InventoryUIController.instance.RefreshAllPlayerDisplays(PartyManager.instance.ActivePlayer);
        }

        ApplySceneRules();

        if (DualModeManager.instance != null)
        {
            DualModeManager.instance.ApplyTeamState(currentSceneType);
        }

        FindAnyObjectByType<UIPartyPortraitsManager>()?.RefreshAllPortraits();

        // 2. Start fading out UI/Loading Screen before firing Tithe
        LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);

        // 3. Brief delay to let the screen clear so the text is visible
        yield return new WaitForSeconds(0.2f);

        // 4. Fire Tithe logic now that the party is moved and screen is clearing
        if (currentSceneType == SceneType.Town)
        {
            // MODIFIED: Pass the SceneInfo we found earlier
            ApplyTithePayment(info);
        }

        float elapsedTime = Time.realtimeSinceStartup - startTime;
        if (elapsedTime < minimumLoadingScreenTime) yield return new WaitForSeconds(minimumLoadingScreenTime - elapsedTime);

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

        lastLocationType = (NodeType)data.lastLocationType;
        justExitedDungeon = false;
        lastLongTermDestinationID = null;

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

        if (SaveManager.instance != null)
        {
            SaveManager.instance.RestoreDualModeState(data);
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

    public void ReloadCurrentLevel(string spawnPointID = null)
    {
        if (isTransitioning) return;
        lastSpawnPointID = spawnPointID;
        StartCoroutine(TransitionLevel(currentLevelScene, spawnPointID));
    }

    public void StartNewGame()
    {
        lastLocationType = NodeType.Scene;
        justExitedDungeon = false;
        LoadLevel(startingSceneName);
    }

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
        lastLocationType = NodeType.Scene;
        justExitedDungeon = false;
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

    /// <summary>
    /// Credits resources to the Wagon upon reaching civilization.
    /// Uses SceneInfo values if available, otherwise defaults to GameManager values.
    /// </summary>
    private void ApplyTithePayment(SceneInfo info = null)
    {
        if (WagonResourceManager.instance == null) return;

        // Default to the settings in GameManager
        int finalFuel = titheFuelCredit;
        int finalRations = titheRationsCredit;
        bool shouldPay = true;

        // If SceneInfo is available, use its settings
        if (info != null)
        {
            shouldPay = info.givesTithe;
            if (shouldPay)
            {
                finalFuel = info.titheFuelAmount;
                finalRations = info.titheRationsAmount;
            }
        }

        if (shouldPay)
        {
            WagonResourceManager.instance.AddResource(ResourceType.Fuel, finalFuel);
            WagonResourceManager.instance.AddResource(ResourceType.Rations, finalRations);

            Debug.Log($"[Tithe] Credited wagon with {finalFuel} Fuel and {finalRations} Rations.");

            // Feedback is visible now that party is moved and screen has cleared
            if (FloatingTextManager.instance != null && playerPartyObject != null)
            {
                FloatingTextManager.instance.ShowEvent($"Tithe Received: +{finalFuel} Fuel, +{finalRations} Rations", playerPartyObject.transform.position);
            }
        }
        else
        {
            Debug.Log($"[Tithe] Scene {currentLevelScene} does not provide a tithe.");
        }
    }

    public void SetPlayerMovementComponentsActive(bool isActive)
    {
        if (playerPartyObject == null || PartyManager.instance == null) return;

        if (!isActive)
        {
            foreach (var animator in playerPartyObject.GetComponentsInChildren<Animator>(true))
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsMoving", false);
            }

            foreach (var agent in playerPartyObject.GetComponentsInChildren<NavMeshAgent>(true))
            {
                if (agent.isActiveAndEnabled) agent.ResetPath();
                agent.enabled = false;
            }
            foreach (var movement in playerPartyObject.GetComponentsInChildren<PlayerMovement>(true)) { movement.enabled = false; }
            foreach (var ai in playerPartyObject.GetComponentsInChildren<PartyMemberAI>(true)) { ai.enabled = false; }

            PartyAIManager partyAI = playerPartyObject.GetComponent<PartyAIManager>();
            if (partyAI != null) { partyAI.enabled = false; }
            return;
        }

        if (currentSceneType == SceneType.Town)
        {
            for (int i = 0; i < PartyManager.instance.partyMembers.Count; i++)
            {
                GameObject member = PartyManager.instance.partyMembers[i];
                if (member == null) continue;

                NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
                PlayerMovement movement = member.GetComponent<PlayerMovement>();
                PartyMemberAI ai = member.GetComponent<PartyMemberAI>();

                if (i == 0) // Leader
                {
                    if (agent != null) agent.enabled = true;
                    if (movement != null) movement.enabled = true;
                    if (ai != null) ai.enabled = false;
                }
                else // Others stay still
                {
                    if (agent != null) agent.enabled = true;
                    if (movement != null) movement.enabled = false;
                    if (ai != null) ai.enabled = false;
                }
            }
            PartyManager.instance.SetPlayerSwitching(false);
        }
        else
        {
            foreach (var agent in playerPartyObject.GetComponentsInChildren<NavMeshAgent>(true)) { agent.enabled = true; }
            foreach (var movement in playerPartyObject.GetComponentsInChildren<PlayerMovement>(true)) { movement.enabled = true; }
            foreach (var ai in playerPartyObject.GetComponentsInChildren<PartyMemberAI>(true)) { ai.enabled = true; }
            PartyAIManager partyAI = playerPartyObject.GetComponent<PartyAIManager>();
            if (partyAI != null) { partyAI.enabled = true; }

            PartyManager.instance.SetPlayerSwitching(true);
            PartyAIManager.instance?.EnterCombatMode();
        }
    }

    private static string NormalizeSceneName(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.EndsWith(".unity")) s = Path.GetFileNameWithoutExtension(s);
        int slash = s.LastIndexOf('/');
        if (slash >= 0 && slash < s.Length - 1) s = s[(slash + 1)..];
        return s;
    }

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
                // [FIX APPLIED] Force the agent to acknowledge the transform change immediately
                // This prevents the agent's 'nextPosition' (stuck at 0,0,0) from overriding 
                // the transform when PlayerMovement updates.
                agent.Warp(member.transform.position);
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