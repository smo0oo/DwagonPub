using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.Splines;
using UnityEngine.UI;
using Cinemachine;
using PixelCrushers.DialogueSystem;

public class WorldMapManager : MonoBehaviour
{
    public static WorldMapManager instance;

    [Header("Game State")]
    public LocationNode currentLocation;
    public LocationNode longTermDestination;
    public float timeOfDay = 8f;

    [Header("Travel Simulation")]
    public float nightfallHour = 18f;
    public float wagonTravelSpeed = 20f;
    public string defaultDomeBattleScene = "DomeBattle";

    [Header("World Dimensions")]
    public Vector2 worldSize = new Vector2(2000, 2000);
    public Vector2 worldCenter = Vector2.zero;

    [Header("Raycasting")]
    public LayerMask locationNodeLayer;

    [Header("UI - Travel Control")]
    public GameObject travelConfirmationPanel;
    public TextMeshProUGUI confirmationText;
    public Button confirmTravelButton;
    public Button cancelTravelButton;
    public Button haltButton;

    [Header("UI - Arrival")]
    public GameObject arrivalPanel;
    public TextMeshProUGUI arrivalText;
    public Button enterButton;
    public Button continueJourneyButton;
    public Button waitButton;

    [Header("UI - Roadside Interaction")]
    public GameObject roadsidePanel;
    public TextMeshProUGUI roadsideInfoText;
    public Button resumeButton;
    public Button campButton;
    public Button forageButton;

    [Header("UI - Narrative Foraging")]
    public GameObject forageEventPanel;
    public Image forageEventImage;
    public TextMeshProUGUI forageEventTitleText;
    public TextMeshProUGUI forageEventStoryText;
    public Button forageAcceptButton;
    public TextMeshProUGUI forageAcceptButtonText;
    public Button forageEnterDungeonButton;

    private ForageEventData currentActiveForageEvent;

    [Header("Scene References")]
    public WagonController wagonController;
    public WagonResourceManager resourceManager;

    private List<LocationNode> currentPlannedPath;
    private RoadConnection currentActiveConnection;

    private Camera mainCamera;
    private bool isUiBusy = false;
    private LocationNode lastHoveredNode = null;
    private float baseWagonSpeed;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); }
        else { instance = this; }
    }

    void Start()
    {
        mainCamera = Camera.main;
        baseWagonSpeed = wagonTravelSpeed;

        if (travelConfirmationPanel != null) travelConfirmationPanel.SetActive(false);
        if (arrivalPanel != null) arrivalPanel.SetActive(false);
        if (roadsidePanel != null) roadsidePanel.SetActive(false);
        if (haltButton != null) haltButton.gameObject.SetActive(false);

        if (forageEventPanel != null) forageEventPanel.SetActive(false);

        if (confirmTravelButton != null) confirmTravelButton.onClick.AddListener(OnConfirmTravel);
        if (cancelTravelButton != null) cancelTravelButton.onClick.AddListener(OnCancelTravel);
        if (enterButton != null) enterButton.onClick.AddListener(OnEnterLocation);
        if (continueJourneyButton != null) continueJourneyButton.onClick.AddListener(OnContinueJourney);
        if (waitButton != null) waitButton.onClick.AddListener(OnWait);

        if (haltButton != null) haltButton.onClick.AddListener(OnHaltClicked);
        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
        if (campButton != null) campButton.onClick.AddListener(OnCampClicked);
        if (forageButton != null) forageButton.onClick.AddListener(OnForageClicked);

        if (forageAcceptButton != null) forageAcceptButton.onClick.AddListener(OnForageAcceptClicked);
        if (forageEnterDungeonButton != null) forageEnterDungeonButton.onClick.AddListener(OnForageEnterDungeonClicked);

        if (resourceManager == null) resourceManager = FindAnyObjectByType<WagonResourceManager>();
    }

    public LocationNode GetFinalDestination()
    {
        if (longTermDestination != null) return longTermDestination;
        if (currentPlannedPath != null && currentPlannedPath.Count > 0) return currentPlannedPath.Last();
        return null;
    }

    public void RestoreJourneyState(SplineContainer spline, float progress, bool reverse)
    {
        currentPlannedPath = null;

        if (wagonController != null)
        {
            wagonController.RestorePosition(spline, progress, reverse);
        }

        LocationNode originNode = null;
        RoadConnection connection = null;

        var allNodes = FindObjectsByType<LocationNode>(FindObjectsSortMode.None);
        foreach (var node in allNodes)
        {
            foreach (var conn in node.connections)
            {
                if (conn.roadSpline == spline && conn.reverseSpline == reverse)
                {
                    originNode = node;
                    connection = conn;
                    break;
                }
            }
            if (originNode != null) break;
        }

        if (originNode != null && connection != null)
        {
            currentLocation = originNode;
            currentActiveConnection = connection;

            if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.lastLongTermDestinationID))
            {
                var target = allNodes.FirstOrDefault(n => n.locationName == GameManager.instance.lastLongTermDestinationID);
                if (target != null && target != originNode)
                {
                    SetLongTermDestination(target);
                    var trip = NavigationComputer.CalculateTrip(originNode, target);
                    if (trip.isValid && trip.path.Count > 1)
                    {
                        currentPlannedPath = trip.path;
                    }
                }
            }

            ShowRoadsidePanel();

            if (arrivalPanel != null) arrivalPanel.SetActive(false);
            if (haltButton != null) haltButton.gameObject.SetActive(false);

            originNode.SetRoadsVisibilityExcept(spline, false);
        }
    }

    public Vector2 GetNormalizedPosition(Vector3 worldPos)
    {
        float offsetX = worldPos.x - (worldCenter.x - worldSize.x / 2f);
        float offsetY = worldPos.z - (worldCenter.y - worldSize.y / 2f);
        float x = offsetX / worldSize.x;
        float y = offsetY / worldSize.y;
        return new Vector2(x, y);
    }

    public void SetLongTermDestination(LocationNode target)
    {
        longTermDestination = target;
        UpdateGPSHighlight();
    }

    public void UpdateGPSHighlight()
    {
        var allNodes = FindObjectsByType<LocationNode>(FindObjectsSortMode.None);
        foreach (var node in allNodes)
        {
            if (node.gpsHighlight != null) node.gpsHighlight.SetActive(false);
        }

        if (longTermDestination == null || currentLocation == longTermDestination) return;

        LocationNode nextStep = NavigationComputer.GetNextStep(currentLocation, longTermDestination);

        if (nextStep != null && nextStep.gpsHighlight != null)
        {
            nextStep.gpsHighlight.SetActive(true);
        }
    }

    private void OnHaltClicked()
    {
        if (wagonController != null && wagonController.IsTraveling)
        {
            wagonController.PauseJourney();
            ShowRoadsidePanel();
            if (haltButton != null) haltButton.gameObject.SetActive(false);
        }
    }

    private void OnResumeClicked()
    {
        if (roadsidePanel != null) roadsidePanel.SetActive(false);

        if (wagonController != null)
        {
            if (wagonController.IsTraveling)
            {
                wagonController.ResumeJourney();
            }
            else
            {
                float progress = Mathf.Clamp(wagonController.TravelProgress, 0f, 1f);

                if (currentPlannedPath != null && currentPlannedPath.Count > 1)
                {
                    StartCoroutine(ExecuteResumedJourney(progress));
                }
                else if (currentActiveConnection != null && currentActiveConnection.destinationNode != null)
                {
                    if (progress > 0.99f)
                    {
                        SetCurrentLocation(currentActiveConnection.destinationNode, true);
                        ShowArrivalPanel();
                    }
                    else
                    {
                        StartCoroutine(TravelToNode(currentActiveConnection.destinationNode, progress));
                    }
                }
            }
        }

        if (haltButton != null) haltButton.gameObject.SetActive(true);
    }

    private IEnumerator ExecuteResumedJourney(float firstLegStartProgress)
    {
        int startIndex = currentPlannedPath.IndexOf(currentLocation);
        if (startIndex == -1) startIndex = 0;

        for (int i = startIndex; i < currentPlannedPath.Count - 1; i++)
        {
            LocationNode legEnd = currentPlannedPath[i + 1];
            float progress = (i == startIndex) ? firstLegStartProgress : 0f;

            if (i == startIndex && progress > 0.99f)
            {
                SetCurrentLocation(legEnd, true);

                if (legEnd == currentPlannedPath.Last())
                {
                    ShowArrivalPanel();
                    yield break;
                }
                continue;
            }

            yield return StartCoroutine(TravelToNode(legEnd, progress));

            if (currentLocation != legEnd) yield break;
        }
    }

    private void OnCampClicked()
    {
        string sceneToLoad = defaultDomeBattleScene;
        if (currentActiveConnection != null && !string.IsNullOrEmpty(currentActiveConnection.combatSceneName))
        {
            sceneToLoad = currentActiveConnection.combatSceneName;
        }
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadLevel(sceneToLoad, "WagonCenter");
        }
    }

    private void ShowRoadsidePanel()
    {
        if (roadsidePanel != null) roadsidePanel.SetActive(true);
        if (roadsideInfoText != null) roadsideInfoText.text = "The wagon is stopped. What is your command?";
    }

    private void OnForageClicked()
    {
        timeOfDay += 1f;
        if (timeOfDay >= 24f) timeOfDay -= 24f;

        if (resourceManager != null) resourceManager.ConsumeForTime(1f);

        if (currentActiveConnection == null || currentActiveConnection.possibleForageEvents.Count == 0)
        {
            if (roadsideInfoText != null) roadsideInfoText.text = "You search the area, but find nothing of interest.";
            return;
        }

        int totalWeight = currentActiveConnection.possibleForageEvents.Sum(e => e.weight);
        int randomRoll = Random.Range(0, totalWeight);

        ForageEventData rolledEvent = null;
        int currentWeightSum = 0;

        foreach (var eventWeight in currentActiveConnection.possibleForageEvents)
        {
            currentWeightSum += eventWeight.weight;
            if (randomRoll < currentWeightSum)
            {
                rolledEvent = eventWeight.eventData;
                break;
            }
        }

        if (rolledEvent == null) return;

        currentActiveForageEvent = rolledEvent;
        isUiBusy = true;

        if (roadsidePanel != null) roadsidePanel.SetActive(false);

        if (forageEventTitleText != null) forageEventTitleText.text = rolledEvent.eventName;
        if (forageEventStoryText != null) forageEventStoryText.text = rolledEvent.storySnippet;
        if (forageAcceptButtonText != null) forageAcceptButtonText.text = rolledEvent.acceptButtonText;

        if (forageEventImage != null)
        {
            if (rolledEvent.contextualArt != null)
            {
                forageEventImage.sprite = rolledEvent.contextualArt;
                forageEventImage.gameObject.SetActive(true);
            }
            else
            {
                forageEventImage.gameObject.SetActive(false);
            }
        }

        if (forageAcceptButton != null) forageAcceptButton.gameObject.SetActive(true);

        if (forageEnterDungeonButton != null)
        {
            if (rolledEvent.eventType == ForageEventType.HiddenDungeon || rolledEvent.eventType == ForageEventType.Ambush)
            {
                forageEnterDungeonButton.gameObject.SetActive(true);
            }
            else
            {
                forageEnterDungeonButton.gameObject.SetActive(false);
            }
        }

        if (forageEventPanel != null) forageEventPanel.SetActive(true);
    }

    private void OnForageAcceptClicked()
    {
        if (currentActiveForageEvent != null && currentActiveForageEvent.rewardTable != null)
        {
            string lootSummary = "Obtained:\n";
            bool foundSomething = false;

            foreach (var drop in currentActiveForageEvent.rewardTable.potentialDrops)
            {
                if (Random.value <= drop.dropChance)
                {
                    int qty = Random.Range(drop.minQuantity, drop.maxQuantity + 1);
                    if (InventoryManager.instance != null)
                    {
                        InventoryManager.instance.HandleLoot(drop.itemData, qty);
                        lootSummary += $"{qty}x {drop.itemData.displayName}\n";
                        foundSomething = true;
                    }
                }
            }

            if (roadsideInfoText != null)
            {
                roadsideInfoText.text = foundSomething ? lootSummary : "The cache was empty...";
            }
        }

        if (currentActiveForageEvent != null && !string.IsNullOrEmpty(currentActiveForageEvent.conversationTitle))
        {
            if (forageEventPanel != null) forageEventPanel.SetActive(false);
            DialogueManager.StartConversation(currentActiveForageEvent.conversationTitle);
        }
        else
        {
            if (forageEventPanel != null) forageEventPanel.SetActive(false);
            ShowRoadsidePanel();
        }

        currentActiveForageEvent = null;
        isUiBusy = false;
    }

    private void OnForageEnterDungeonClicked()
    {
        if (currentActiveForageEvent == null || string.IsNullOrEmpty(currentActiveForageEvent.linkedSceneName)) return;

        if (forageEventPanel != null) forageEventPanel.SetActive(false);
        isUiBusy = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.SetLocationType(NodeType.Event);
            GameManager.instance.LoadLevel(currentActiveForageEvent.linkedSceneName, "DungeonEntrance", "WorldMap");
        }
    }

    public void OnLocationClicked(LocationNode destinationNode)
    {
        if (currentLocation == null) return;
        if (destinationNode == currentLocation) return;

        var tripData = NavigationComputer.CalculateTrip(currentLocation, destinationNode);

        if (tripData.isValid && tripData.path.Count > 1)
        {
            currentPlannedPath = tripData.path;
            SetLongTermDestination(destinationNode);

            bool pathBlocked = false;
            string blockingReason = "";

            for (int i = 0; i < currentPlannedPath.Count - 1; i++)
            {
                LocationNode from = currentPlannedPath[i];
                LocationNode to = currentPlannedPath[i + 1];

                string missingTags;
                if (!from.CanTravelTo(to, out missingTags))
                {
                    pathBlocked = true;
                    blockingReason = $"Requires: {missingTags}\n(Near {to.locationName})";
                    break;
                }
            }

            float arrivalTime = (timeOfDay + tripData.totalHours) % 24f;
            string warning = "";

            if (tripData.totalHours > 0)
            {
                float endTime = timeOfDay + tripData.totalHours;
                if (endTime >= nightfallHour || (timeOfDay < nightfallHour && endTime % 24f >= nightfallHour))
                {
                    warning = "\n<color=red>[WARNING] Arriving at Night!</color>";
                }
            }

            if (pathBlocked)
            {
                confirmationText.text = $"<color=red>CANNOT TRAVEL</color>\n" +
                                        $"<color=red>{blockingReason}</color>\n\n" +
                                        $"Visit the Workshop to upgrade your wagon.";

                confirmTravelButton.interactable = false;
            }
            else
            {
                float currentFuel = (resourceManager != null) ? resourceManager.currentFuel : 0;
                float currentRations = (resourceManager != null) ? resourceManager.currentRations : 0;
                string fuelColor = (currentFuel >= tripData.estimatedFuelCost) ? "black" : "red";
                string foodColor = (currentRations >= tripData.estimatedRationsCost) ? "black" : "red";

                confirmationText.text = $"Travel to {destinationNode.locationName}?\n" +
                                        $"Total Time: {tripData.totalHours} hrs\n" +
                                        $"Fuel: <color={fuelColor}>{tripData.estimatedFuelCost:F0}</color> | " +
                                        $"Food: <color={foodColor}>{tripData.estimatedRationsCost:F0}</color>" +
                                        $"{warning}";

                confirmTravelButton.interactable = true;
            }

            travelConfirmationPanel.SetActive(true);
            isUiBusy = true;
        }
    }

    public void OnConfirmTravel()
    {
        travelConfirmationPanel.SetActive(false);
        if (currentPlannedPath != null && currentPlannedPath.Count > 1)
        {
            StartCoroutine(ExecuteMultiLegJourney());
        }
        else
        {
            isUiBusy = false;
        }
    }

    public void OnCancelTravel()
    {
        travelConfirmationPanel.SetActive(false);
        currentPlannedPath = null;
        isUiBusy = false;
    }

    private IEnumerator ExecuteMultiLegJourney()
    {
        for (int i = 0; i < currentPlannedPath.Count - 1; i++)
        {
            LocationNode legEnd = currentPlannedPath[i + 1];
            yield return StartCoroutine(TravelToNode(legEnd));
            if (currentLocation != legEnd) yield break;
        }
    }

    private IEnumerator TravelToNode(LocationNode destination, float startProgress = 0f)
    {
        isUiBusy = true;

        if (currentActiveConnection == null)
        {
            currentActiveConnection = currentLocation.connections.FirstOrDefault(c => c.destinationNode != null && c.destinationNode.locationName == destination.locationName);
        }

        if (currentActiveConnection == null) { isUiBusy = false; yield break; }

        currentLocation.SetRoadsVisibilityExcept(currentActiveConnection.roadSpline, false);

        if (haltButton != null) haltButton.gameObject.SetActive(true);

        if (wagonController != null && currentActiveConnection.roadSpline != null)
        {
            float splineLength = currentActiveConnection.roadSpline.CalculateLength();
            float fullDuration = (wagonTravelSpeed > 0) ? splineLength / wagonTravelSpeed : 5f;

            wagonController.StartJourney(
                currentActiveConnection.roadSpline,
                fullDuration,
                currentActiveConnection.reverseSpline,
                currentActiveConnection.manualYRotation,
                startProgress
            );
        }

        float safeStart = Mathf.Clamp(startProgress, 0f, 0.999f);
        float remainingPercent = 1f - safeStart;
        float remainingHours = currentActiveConnection.travelTimeHours * remainingPercent;

        float previousRelativeProgress = 0f;
        int dayOfLastAmbush = -1;
        int currentDay = 0;

        while (wagonController != null && wagonController.IsTraveling)
        {
            if (wagonController.IsPaused)
            {
                yield return null;
                continue;
            }

            float currentProg = wagonController.TravelProgress;
            float relativeProgress = 0f;

            if (remainingPercent > 0.001f)
            {
                relativeProgress = (currentProg - safeStart) / remainingPercent;
            }
            else
            {
                relativeProgress = 1f;
            }

            // AAA DELTA TIME FIX: Add progress natively so we never overwrite manual clock changes
            float deltaProgress = relativeProgress - previousRelativeProgress;
            float deltaTimeHours = remainingHours * deltaProgress;

            timeOfDay += deltaTimeHours;

            // Handle day transitions securely
            while (timeOfDay >= 24f)
            {
                timeOfDay -= 24f;
                currentDay++;
            }

            if (resourceManager != null && deltaTimeHours > 0)
            {
                resourceManager.ConsumeForTravel(deltaTimeHours);
            }

            previousRelativeProgress = relativeProgress;

            bool isNightTime = timeOfDay >= nightfallHour;
            bool canBeAmbushedToday = currentDay > dayOfLastAmbush;

            if (isNightTime && canBeAmbushedToday)
            {
                dayOfLastAmbush = currentDay; // Prevent multiple ambushes per night phase

                int diceRoll = Random.Range(0, 11);
                if (diceRoll <= currentActiveConnection.ambushChance)
                {
                    string ambushScene = currentActiveConnection.combatSceneName;
                    if (string.IsNullOrEmpty(ambushScene)) ambushScene = defaultDomeBattleScene;
                    GameManager.instance.LoadLevel(ambushScene, "AmbushSpawn");
                    yield break;
                }
            }
            yield return null;
        }

        if (haltButton != null) haltButton.gameObject.SetActive(false);
        if (currentActiveConnection != null && currentActiveConnection.roadSpline != null)
        {
            var renderer = currentActiveConnection.roadSpline.GetComponentInChildren<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        currentActiveConnection = null;

        if (destination != null)
        {
            SetCurrentLocation(destination, true);

            bool isFinalDestination = (currentPlannedPath == null || currentPlannedPath.Count == 0 || destination == currentPlannedPath.Last());

            if (isFinalDestination)
            {
                ShowArrivalPanel();
            }
        }
    }

    public void SetCurrentLocation(LocationNode node, bool showRoads)
    {
        if (currentLocation != null) currentLocation.SetRoadsVisibility(false);
        currentLocation = node;
        if (currentLocation != null)
        {
            currentLocation.SetRoadsVisibility(showRoads);
            if (wagonController != null)
            {
                wagonController.transform.position = currentLocation.transform.position;
                wagonController.transform.rotation = currentLocation.transform.rotation;
                wagonController.SetCurrentNode(node);
            }
            UpdateGPSHighlight();
        }
    }

    void Update()
    {
        if (isUiBusy) return;

        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (lastHoveredNode != null && lastHoveredNode != currentLocation)
            {
                lastHoveredNode.SetRoadsVisibility(false);
                lastHoveredNode = null;
            }
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        LocationNode currentHoveredNode = null;

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, locationNodeLayer))
        {
            currentHoveredNode = hit.collider.GetComponent<LocationNode>();
        }

        if (currentHoveredNode != lastHoveredNode)
        {
            if (lastHoveredNode != null && lastHoveredNode != currentLocation)
            {
                lastHoveredNode.SetRoadsVisibility(false);
            }

            if (currentHoveredNode != null && currentHoveredNode != currentLocation)
            {
                currentHoveredNode.SetRoadsVisibility(true);
            }

            lastHoveredNode = currentHoveredNode;
        }

        if (Input.GetMouseButtonDown(0) && currentHoveredNode != null)
        {
            OnLocationClicked(currentHoveredNode);
        }
    }

    public void LinkWagonToCamera() { if (GameManager.instance != null && GameManager.instance.worldMapCamera != null && wagonController != null) { CinemachineVirtualCamera vcam = GameManager.instance.worldMapCamera; if (vcam.Follow == null) { vcam.Follow = wagonController.transform; vcam.LookAt = wagonController.transform; } } }
    public void ApplyNoFuelPenalty() { wagonTravelSpeed = baseWagonSpeed * 0.5f; }
    public void RemoveNoFuelPenalty() { wagonTravelSpeed = baseWagonSpeed; }
    public void GuardedLoadLevel(string sceneToLoad, string spawnPointID, string fromNodeID) { if (GameManager.instance == null) return; if (GameManager.instance.IsTransitioning) return; GameManager.instance.LoadLevel(sceneToLoad, spawnPointID, fromNodeID); }

    private void ShowArrivalPanel()
    {
        isUiBusy = true;
        arrivalText.text = $"You have arrived at {currentLocation.locationName}.\nTime is {Mathf.Floor(timeOfDay)}:00.";
        arrivalPanel.SetActive(true);

        if (currentLocation.nodeType == NodeType.Scene ||
            currentLocation.nodeType == NodeType.DualModeLocation)
        {
            if (enterButton != null) enterButton.gameObject.SetActive(true);
        }
        else
        {
            if (enterButton != null) enterButton.gameObject.SetActive(false);
        }
    }

    public void OnEnterLocation()
    {
        arrivalPanel.SetActive(false);
        isUiBusy = false;

        if (GameManager.instance != null && currentLocation != null)
        {
            GameManager.instance.SetLocationType(currentLocation.nodeType);

            if (DualModeManager.instance != null)
            {
                DualModeManager.instance.queuedDungeonScene = currentLocation.dualModeDungeonScene;
            }

            string spawnPointID = "WorldMapArrival";
            var originNode = FindObjectsByType<LocationNode>(FindObjectsSortMode.None).FirstOrDefault(n => n.connections.Any(c => c.destinationNode.locationName == currentLocation.locationName));
            if (originNode != null)
            {
                var connectionData = originNode.connections.FirstOrDefault(c => c.destinationNode.locationName == currentLocation.locationName);
                if (connectionData != null) spawnPointID = connectionData.destinationSpawnPointID;
            }
            GameManager.instance.LoadLevel(currentLocation.sceneToLoad, spawnPointID, currentLocation.locationName);
        }
    }

    public void OnContinueJourney() { arrivalPanel.SetActive(false); isUiBusy = false; }

    public void OnWait()
    {
        timeOfDay += 1f;
        if (timeOfDay >= 24f) timeOfDay -= 24f;

        if (resourceManager != null) resourceManager.ConsumeForTime(1f);
        ShowArrivalPanel();
    }
}