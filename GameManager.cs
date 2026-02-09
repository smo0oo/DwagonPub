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
    public string lastLongTermDestinationID;

    [Header("Tithe Settings (Credits)")]
    public int titheFuelCredit = 50;
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

    [Header("Sequence Visibility")]
    [Tooltip("CanvasGroups in this list will have their Alpha set to 0 (and input disabled) when a Sequence starts.")]
    public List<CanvasGroup> canvasGroupsHiddenDuringSequence;

    [Header("Core Scene")]
    public string coreSceneName = "CoreScene";

    // --- CACHED SCENE STATE ---
    public SceneType currentSceneType { get; private set; }
    private SceneType cachedSceneType = SceneType.Dungeon;
    private SceneInfo cachedSceneInfo;

    private string currentLevelScene;
    private bool isTransitioning;
    public bool IsTransitioning => isTransitioning;

    public bool IsSequenceModeActive { get; private set; }

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
            GUILayout.Label($"Sequence Mode: {IsSequenceModeActive}");
            GUILayout.Label($"Just Exited Dungeon: {justExitedDungeon}");

            if (DualModeManager.instance != null)
            {
                var dmm = DualModeManager.instance;
                string activeColor = dmm.isDualModeActive ? "green" : "grey";
                GUILayout.Label($"Dual Mode: <color={activeColor}>{dmm.isDualModeActive}</color>");
                if (dmm.isDualModeActive)
                {
                    GUILayout.Label($"Dungeon Team: {dmm.dungeonTeamIndices.Count}");
                    GUILayout.Label($"Wagon Team: {dmm.wagonTeamIndices.Count}");
                }
            }
        }
        GUILayout.EndArea();
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        startingSceneName = NormalizeSceneName(startingSceneName);
        coreSceneName = NormalizeSceneName(coreSceneName);
        currentLevelScene = NormalizeSceneName(SceneManager.GetActiveScene().name);
    }

    void Start()
    {
        UpdateCachedSceneInfo();
    }

    public void SetLocationType(NodeType type)
    {
        lastLocationType = type;
        justExitedDungeon = false;
    }

    public void SetJustExitedDungeon(bool state)
    {
        justExitedDungeon = state;
    }

    private void UpdateCachedSceneInfo()
    {
        cachedSceneInfo = FindAnyObjectByType<SceneInfo>();
        if (cachedSceneInfo != null)
        {
            cachedSceneType = cachedSceneInfo.type;
        }
        else
        {
            if (currentLevelScene.Contains("WorldMap")) cachedSceneType = SceneType.WorldMap;
            else if (currentLevelScene.Contains("MainMenu")) cachedSceneType = SceneType.MainMenu;
            else cachedSceneType = SceneType.Dungeon;
        }
        currentSceneType = cachedSceneType;
    }

    public void ApplySceneRules()
    {
        UpdateCachedSceneInfo();

        if (currentSceneType == SceneType.WorldMap)
        {
            foreach (var member in PartyManager.instance.partyMembers)
            {
                if (member != null) member.SetActive(false);
            }
            PartyManager.instance.SetPlayerSwitching(false);
            return;
        }

        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            DualModeManager.instance.ApplyTeamState(currentSceneType);

            if (currentSceneType == SceneType.Town)
            {
                PartyManager.instance.SetPlayerSwitching(false);
                PartyManager.instance.SetActivePlayer(0);
                PartyAIManager.instance.EnterTownMode();
            }
            else
            {
                PartyManager.instance.SetPlayerSwitching(true);
                PartyAIManager.instance.EnterCombatMode();
            }
            return;
        }

        bool anyControllable = false;

        for (int i = 0; i < PartyManager.instance.partyMembers.Count; i++)
        {
            GameObject member = PartyManager.instance.partyMembers[i];
            if (member == null) continue;

            PlayerSceneState state = PlayerSceneState.Active;
            if (cachedSceneInfo != null && i < cachedSceneInfo.playerConfigs.Count)
                state = cachedSceneInfo.playerConfigs[i].state;

            switch (state)
            {
                case PlayerSceneState.Active:
                    member.SetActive(true);
                    SetMemberControl(member, true);
                    anyControllable = true;
                    break;

                case PlayerSceneState.Inactive:
                case PlayerSceneState.SpawnAtMarker:
                    member.SetActive(true);
                    SetMemberControl(member, false);
                    break;

                case PlayerSceneState.Hidden:
                    member.SetActive(false);
                    break;
            }
        }

        if (currentSceneType == SceneType.Town)
        {
            PartyManager.instance.SetPlayerSwitching(false);
            PartyAIManager.instance.EnterTownMode();
            PartyManager.instance.SetActivePlayer(0);
        }
        else
        {
            PartyManager.instance.SetPlayerSwitching(anyControllable);
            if (anyControllable) PartyAIManager.instance.EnterCombatMode();
            else PartyAIManager.instance.EnterTownMode();
        }
    }

    private void SetMemberControl(GameObject member, bool enabled)
    {
        var agent = member.GetComponent<NavMeshAgent>();
        var movement = member.GetComponent<PlayerMovement>();
        var ai = member.GetComponent<PartyMemberAI>();

        if (agent != null) agent.enabled = enabled;
        if (movement != null) movement.enabled = enabled;

        bool shouldAIBeActive = enabled;
        if (cachedSceneType == SceneType.Town) shouldAIBeActive = false;

        if (ai != null) ai.enabled = shouldAIBeActive;
    }

    public void SetSequenceMode(bool isSequenceActive)
    {
        IsSequenceModeActive = isSequenceActive;

        if (isSequenceActive)
        {
            if (canvasGroupsHiddenDuringSequence != null)
                foreach (var group in canvasGroupsHiddenDuringSequence) ConfigureGroup(group, false);

            if (sharedCanvasGroup != null) ConfigureGroup(sharedCanvasGroup, false);
            if (battleCanvasGroup != null) ConfigureGroup(battleCanvasGroup, false);

            SetPlayerMovementComponentsActive(false);
        }
        else
        {
            RestoreControlsSafely();

            if (currentSceneType == SceneType.WorldMap) SetUIVisibility("WorldMap");
            else if (currentSceneType == SceneType.Cinematic) SetUIVisibility("Cinematic");
            else SetUIVisibility("InGame");
        }
    }

    public void SetDialogueState(bool isInDialogue)
    {
        if (isInDialogue)
        {
            SetPlayerMovementComponentsActive(false);
            if (sharedCanvasGroup != null) ConfigureGroup(sharedCanvasGroup, false);
            if (battleCanvasGroup != null) ConfigureGroup(battleCanvasGroup, false);
        }
        else
        {
            RestoreControlsSafely();

            if (currentSceneType == SceneType.WorldMap) SetUIVisibility("WorldMap");
            else if (currentSceneType == SceneType.Cinematic) SetUIVisibility("Cinematic");
            else SetUIVisibility("InGame");
        }
    }

    private void RestoreControlsSafely()
    {
        UpdateCachedSceneInfo();

        if (playerPartyObject != null)
        {
            PartyAIManager partyAI = playerPartyObject.GetComponent<PartyAIManager>();
            if (partyAI != null) partyAI.enabled = true;

            foreach (var agent in playerPartyObject.GetComponentsInChildren<NavMeshAgent>(true))
            {
                if (agent.gameObject.activeInHierarchy)
                {
                    agent.enabled = true;
                    agent.isStopped = false;
                }
            }

            if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
            {
                GameObject activePlayer = PartyManager.instance.ActivePlayer;

                var pm = activePlayer.GetComponent<PlayerMovement>();
                if (pm != null) pm.enabled = true;

                bool isTown = (cachedSceneType == SceneType.Town);

                foreach (var member in PartyManager.instance.partyMembers)
                {
                    if (member == null) continue;
                    var ai = member.GetComponent<PartyMemberAI>();
                    if (ai != null)
                    {
                        bool isLeader = (member == activePlayer);
                        bool aiShouldBeActive = !isLeader && !isTown;
                        ai.enabled = aiShouldBeActive;
                    }
                }

                if (partyAI != null) partyAI.ForceUpdateState(isTown);
            }
        }
    }

    public void SetPlayerMovementComponentsActive(bool isActive)
    {
        if (playerPartyObject == null || PartyManager.instance == null) return;

        if (!isActive)
        {
            foreach (var animator in playerPartyObject.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController != null)
                {
                    animator.SetFloat("VelocityX", 0f);
                    animator.SetFloat("VelocityZ", 0f);
                }
            }

            foreach (var agent in playerPartyObject.GetComponentsInChildren<NavMeshAgent>(true))
            {
                if (agent.isActiveAndEnabled)
                {
                    agent.ResetPath();
                    agent.velocity = Vector3.zero;
                    agent.isStopped = true;
                }
                agent.enabled = false;
            }

            foreach (var movement in playerPartyObject.GetComponentsInChildren<PlayerMovement>(true))
                movement.enabled = false;

            foreach (var ai in playerPartyObject.GetComponentsInChildren<PartyMemberAI>(true))
                ai.enabled = false;

            PartyAIManager partyAI = playerPartyObject.GetComponent<PartyAIManager>();
            if (partyAI != null) partyAI.enabled = false;

            return;
        }

        RestoreControlsSafely();
    }

    // --- LOADING & TRANSITIONS ---
    public void LoadLevel(string sceneName, string spawnPointID = null, string fromNodeID = null)
    {
        if (isTransitioning) return;
        lastSpawnPointID = spawnPointID;
        sceneName = NormalizeSceneName(sceneName);
        var alreadyLoaded = SceneManager.GetSceneByName(sceneName).isLoaded;
        if (alreadyLoaded && currentLevelScene == sceneName && SceneManager.GetActiveScene().name == sceneName) return;
        StartCoroutine(TransitionLevel(sceneName, spawnPointID, fromNodeID));
    }

    private IEnumerator TransitionLevel(string sceneName, string spawnPointID, string fromNodeID = null)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;
        IsSequenceModeActive = false;

        // [FIX] Disable controls IMMEDIATELY to prevent input bleed while fading out
        SetPlayerMovementComponentsActive(false);

        if (currentSceneType == SceneType.WorldMap) CaptureWagonState();
        if (currentLevelScene != sceneName)
        {
            if (currentSceneType == SceneType.Town || currentSceneType == SceneType.DomeBattle || currentSceneType == SceneType.WorldMap)
                previousSceneName = currentLevelScene;
        }

        float startTime = Time.realtimeSinceStartup;
        yield return LoadingScreenManager.instance.ShowLoadingScreen(fadeDuration);

        if (SceneStateManager.instance != null && FindAnyObjectByType<InventoryManager>() != null)
            SceneStateManager.instance.CaptureSceneState(currentLevelScene, FindAnyObjectByType<InventoryManager>().worldItemPrefab);

        yield return SwitchToSceneExact(sceneName);

        UpdateCachedSceneInfo();

        if (SceneStateManager.instance != null && FindAnyObjectByType<InventoryManager>() != null)
            SceneStateManager.instance.RestoreSceneState(sceneName, FindAnyObjectByType<InventoryManager>().worldItemPrefab);

        ApplySceneRules();

        if (DualModeManager.instance != null) DualModeManager.instance.ApplyTeamState(currentSceneType);

        if (currentSceneType != SceneType.MainMenu && currentSceneType != SceneType.WorldMap)
        {
            if (InventoryUIController.instance != null && PartyManager.instance != null)
                InventoryUIController.instance.RefreshAllPlayerDisplays(PartyManager.instance.ActivePlayer);
        }
        FindAnyObjectByType<UIPartyPortraitsManager>()?.RefreshAllPortraits();

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
            SetUIVisibility((currentSceneType == SceneType.Cinematic) ? "Cinematic" : "InGame");
            if (!string.IsNullOrEmpty(fromNodeID)) lastKnownLocationNodeID = fromNodeID;
            SetPlayerModelsActive(true);

            MovePartyToSpawnPoint(spawnPointID);
        }

        float elapsedTime = Time.realtimeSinceStartup - startTime;
        if (elapsedTime < minimumLoadingScreenTime) yield return new WaitForSeconds(minimumLoadingScreenTime - elapsedTime);

        LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
        if (currentSceneType == SceneType.Town) ApplyTithePayment(cachedSceneInfo);
        isTransitioning = false;
    }

    private void MovePartyToSpawnPoint(string spawnPointID)
    {
        PlayerSpawnPoint spawnPoint = null;
        if (!string.IsNullOrEmpty(spawnPointID))
        {
            spawnPoint = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None)
                .FirstOrDefault(sp => sp.spawnPointID == spawnPointID);
        }

        if (spawnPoint == null)
        {
            spawnPoint = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None).FirstOrDefault();
        }

        if (spawnPoint == null) return;

        PartyManager partyManager = PartyManager.instance;
        if (playerPartyObject == null || partyManager == null) return;

        playerPartyObject.transform.position = spawnPoint.transform.position;
        playerPartyObject.transform.rotation = spawnPoint.transform.rotation;

        TownCharacterSpawnPoint[] townSpawns = FindObjectsByType<TownCharacterSpawnPoint>(FindObjectsSortMode.None);

        for (int i = 0; i < partyManager.partyMembers.Count; i++)
        {
            GameObject member = partyManager.partyMembers[i];
            if (member == null) continue;

            if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
            {
                if (i == 0) { member.SetActive(false); continue; }
                if (currentSceneType == SceneType.Dungeon) { if (!DualModeManager.instance.dungeonTeamIndices.Contains(i)) { member.SetActive(false); continue; } }
                else if (currentSceneType == SceneType.DomeBattle) { if (!justExitedDungeon) { if (DualModeManager.instance.dungeonTeamIndices.Contains(i)) { member.SetActive(false); continue; } } }
            }

            PlayerSceneState state = PlayerSceneState.Active;
            if (cachedSceneInfo != null && i < cachedSceneInfo.playerConfigs.Count) state = cachedSceneInfo.playerConfigs[i].state;

            if (state == PlayerSceneState.Hidden && !(DualModeManager.instance != null && DualModeManager.instance.isDualModeActive))
            {
                member.SetActive(false);
                continue;
            }

            NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            bool positioned = false;

            if (i != 0 && (state == PlayerSceneState.SpawnAtMarker || (townSpawns != null && townSpawns.Length > 0)))
            {
                TownCharacterSpawnPoint mySpawn = townSpawns.FirstOrDefault(t => t.partyMemberIndex == i);
                if (mySpawn != null)
                {
                    member.transform.position = mySpawn.transform.position;
                    member.transform.rotation = mySpawn.transform.rotation;
                    positioned = true;
                }
            }

            if (!positioned)
            {
                member.transform.localPosition = Vector3.zero;
                member.transform.localRotation = Quaternion.identity;
            }

            bool shouldBeActive = true;
            bool shouldHaveControl = (state == PlayerSceneState.Active);
            member.SetActive(shouldBeActive);

            if (agent != null)
            {
                if (shouldHaveControl) { agent.enabled = true; agent.Warp(member.transform.position); }
                else { agent.enabled = false; }
            }
        }
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
                lastLongTermDestinationID = (dest != null) ? dest.locationName : null;
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
        IsSequenceModeActive = false;

        // [FIX] Disable controls IMMEDIATELY
        SetPlayerMovementComponentsActive(false);

        NodeType typeBeforeExit = lastLocationType;
        string sceneBeforeExit = currentLevelScene;
        lastLocationType = NodeType.Scene;
        float startTime = Time.realtimeSinceStartup;
        yield return LoadingScreenManager.instance.ShowLoadingScreen(fadeDuration);

        if (SceneStateManager.instance != null && FindAnyObjectByType<InventoryManager>() != null)
            SceneStateManager.instance.CaptureSceneState(currentLevelScene, FindAnyObjectByType<InventoryManager>().worldItemPrefab);

        previousSceneName = currentLevelScene;
        yield return SwitchToSceneExact(worldMapSceneName);
        yield return null;

        UpdateCachedSceneInfo();

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
            LocationNode targetNode = FindObjectsByType<LocationNode>(FindObjectsSortMode.None).FirstOrDefault(n => n.locationName == lastKnownLocationNodeID);
            if (targetNode != null) wmm.SetCurrentLocation(targetNode, true);
        }

        if (wmm != null)
        {
            bool isAmbushOrCamp = sceneBeforeExit.Contains("DomeBattle");
            bool isDungeonRun = typeBeforeExit == NodeType.Scene || typeBeforeExit == NodeType.DualModeLocation;
            if (isAmbushOrCamp || isDungeonRun) wmm.timeOfDay = 6f;
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
        if (isTransitioning) return;
        StartCoroutine(LoadGameSequence());
    }

    private IEnumerator LoadGameSequence()
    {
        if (isTransitioning) yield break;
        isTransitioning = true;
        IsSequenceModeActive = false;

        // Disable controls immediately for load too
        SetPlayerMovementComponentsActive(false);

        yield return LoadingScreenManager.instance.ShowLoadingScreen(fadeDuration);
        string path = Path.Combine(Application.persistentDataPath, "savegame.json");
        if (!File.Exists(path))
        {
            isTransitioning = false;
            LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
            yield break;
        }
        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        lastLocationType = (NodeType)data.lastLocationType;
        justExitedDungeon = false;
        lastLongTermDestinationID = null;
        yield return SwitchToSceneExact(startingSceneName);
        var targetNode = FindObjectsByType<LocationNode>(FindObjectsSortMode.None).FirstOrDefault(n => n.locationName == data.currentLocationNodeID);
        if (targetNode == null)
        {
            isTransitioning = false;
            LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
            yield break;
        }
        string finalSceneToLoad = NormalizeSceneName(targetNode.sceneToLoad);
        if (finalSceneToLoad != startingSceneName) yield return SwitchToSceneExact(finalSceneToLoad);

        UpdateCachedSceneInfo();

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
                Ability baseAbility = stats.characterClass.classSkillTree.skillNodes.Where(sn => sn.skillRanks.Count > 0 && sn.skillRanks[0].abilityName == abilityName).Select(sn => sn.skillRanks[0]).FirstOrDefault();
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
        if (SaveManager.instance != null) SaveManager.instance.RestoreDualModeState(data);
        if (InventoryUIController.instance != null) InventoryUIController.instance.RefreshAllPlayerDisplays(PartyManager.instance.ActivePlayer);
        if (isWorldMapScene) SetUIVisibility("WorldMap");
        else if (currentSceneType == SceneType.Cinematic) SetUIVisibility("Cinematic");
        else SetUIVisibility("InGame");
        SetPlayerModelsActive(!isWorldMapScene);
        SetPlayerMovementComponentsActive(!isWorldMapScene);
        LoadingScreenManager.instance.HideLoadingScreen(fadeDuration);
        isTransitioning = false;
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
        UpdateCachedSceneInfo();
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

    private void ApplyTithePayment(SceneInfo info = null)
    {
        if (WagonResourceManager.instance == null) return;
        int finalFuel = titheFuelCredit;
        int finalRations = titheRationsCredit;
        bool shouldPay = true;
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
            if (FloatingTextManager.instance != null && playerPartyObject != null)
            {
                FloatingTextManager.instance.ShowEvent($"Tithe Received: +{finalFuel} Fuel, +{finalRations} Rations", playerPartyObject.transform.position);
            }
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
        bool isHUDVisible = !IsSequenceModeActive;
        switch (sceneType)
        {
            case "WorldMap":
                ConfigureGroup(sharedCanvasGroup, isHUDVisible);
                ConfigureGroup(battleCanvasGroup, false);
                ConfigureGroup(worldMapCanvasGroup, true);
                ConfigureGroup(inGameMenuCanvasGroup, true, false);
                ConfigureGroup(wagonHotbarCanvasGroup, false);
                ConfigureGroup(domeUICanvasGroup, false);
                SetElementsVisibility(worldMapHiddenElements, false);
                if (worldMapCamera != null) worldMapCamera.gameObject.SetActive(true);
                break;
            case "Cinematic":
                ConfigureGroup(sharedCanvasGroup, false);
                ConfigureGroup(battleCanvasGroup, false);
                ConfigureGroup(worldMapCanvasGroup, false);
                ConfigureGroup(inGameMenuCanvasGroup, false);
                ConfigureGroup(wagonHotbarCanvasGroup, false);
                ConfigureGroup(domeUICanvasGroup, false);
                SetElementsVisibility(worldMapHiddenElements, true);
                if (worldMapCamera != null) worldMapCamera.gameObject.SetActive(false);
                break;
            case "InGame":
                ConfigureGroup(sharedCanvasGroup, isHUDVisible);
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
            if (element != null)
            {
                element.SetActive(isVisible);
            }
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