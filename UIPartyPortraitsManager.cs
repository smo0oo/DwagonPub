using UnityEngine;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using DG.Tweening;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(CanvasGroup))]
public class UIPartyPortraitsManager : MonoBehaviour
{
    [Header("Core References")]
    public PartyManager partyManager;
    public List<PlayerPortraitUI> playerPortraits;
    public GameObject statusEffectIconPrefab;
    [Header("Animation Settings")]
    public float activeScale = 1.1f;
    public float inactiveScale = 1.0f;
    public float scaleDuration = 0.25f;
    [Header("Movement Settings")]
    public RectTransform statsOpenPositionAnchor;
    public float moveDuration = 0.5f;
    [Header("AI Stance Icons")]
    public Sprite aggressiveIcon;
    public Sprite defensiveIcon;
    public Sprite passiveIcon;

    private RectTransform rectTransform;
    private Vector3 originalPosition;
    private CanvasGroup canvasGroup;
    private Dictionary<GameObject, PlayerPortraitUI> playerToUIMap = new Dictionary<GameObject, PlayerPortraitUI>();
    private Dictionary<StatusEffectHolder, PlayerPortraitUI> holderToUIMap = new Dictionary<StatusEffectHolder, PlayerPortraitUI>();
    private List<GameObject> allPlayers;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged;
        StatsUIManager.OnStatsPanelToggled += HandleStatsPanelToggle;
        InventoryUIController.OnPartyInventoryToggled += HandlePartyInventoryToggle;
        PartyAIManager.OnStanceChanged += HandleStanceChanged;
        StartCoroutine(DelayedInitialRefresh());
    }

    void OnDisable()
    {
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
        StatsUIManager.OnStatsPanelToggled -= HandleStatsPanelToggle;
        InventoryUIController.OnPartyInventoryToggled -= HandlePartyInventoryToggle;
        PartyAIManager.OnStanceChanged -= HandleStanceChanged;
        if (allPlayers != null) { foreach (var player in allPlayers) { if (player != null && player.TryGetComponent<PartyMemberAI>(out var ai)) { ai.OnStatusChanged -= HandleAIStatusChanged; } } }
    }

    public void RefreshAllPortraits()
    {
        if (partyManager == null) return;

        if (allPlayers != null) { foreach (var player in allPlayers) { if (player != null && player.TryGetComponent<PartyMemberAI>(out var ai)) { ai.OnStatusChanged -= HandleAIStatusChanged; } } }

        allPlayers = partyManager.partyMembers;
        playerToUIMap.Clear();
        holderToUIMap.Clear();

        SceneInfo currentSceneInfo = FindAnyObjectByType<SceneInfo>();

        for (int i = 0; i < playerPortraits.Count; i++)
        {
            PlayerPortraitUI ui = playerPortraits[i];

            if (i < allPlayers.Count)
            {
                GameObject player = allPlayers[i];
                playerToUIMap[player] = ui;

                // --- SCENE INFO LOGIC ---
                PlayerSceneState sceneState = PlayerSceneState.Active;
                if (currentSceneInfo != null && i < currentSceneInfo.playerConfigs.Count)
                {
                    sceneState = currentSceneInfo.playerConfigs[i].state;
                }

                // Ensure the frame is universally active in the layout group
                ui.portraitFrame.SetActive(true);

                if (ui.canvasGroup != null)
                {
                    if (sceneState == PlayerSceneState.Hidden)
                    {
                        // Completely invisible, but preserves layout space
                        ui.canvasGroup.alpha = 0f;
                        ui.canvasGroup.interactable = false;
                        ui.canvasGroup.blocksRaycasts = false;
                    }
                    else
                    {
                        // Visible and functional
                        ui.canvasGroup.alpha = 1f;
                        ui.canvasGroup.interactable = true;
                        ui.canvasGroup.blocksRaycasts = true;
                    }
                }

                // If inactive, darken the portrait to visually indicate they cannot be controlled
                if (sceneState == PlayerSceneState.Inactive || sceneState == PlayerSceneState.SpawnAtMarker)
                {
                    ui.portraitImage.color = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark Gray
                }
                else
                {
                    ui.portraitImage.color = Color.white; // Full brightness
                }
                // -------------------------

                CharacterRoot root = player.GetComponent<CharacterRoot>();
                if (root == null) continue;

                PlayerStats stats = root.PlayerStats;
                Health health = root.Health;
                StatusEffectHolder statusHolder = root.GetComponentInChildren<StatusEffectHolder>();
                PartyMemberAI ai = root.GetComponent<PartyMemberAI>();

                if (stats != null)
                {
                    string characterDisplayName = player.name;
                    DialogueActor dialogueActor = player.GetComponentInChildren<DialogueActor>();
                    if (dialogueActor != null && !string.IsNullOrEmpty(dialogueActor.actor))
                    {
                        Actor actorRecord = DialogueManager.MasterDatabase.GetActor(dialogueActor.actor);
                        if (actorRecord != null)
                        {
                            characterDisplayName = actorRecord.LookupValue("Display Name") ?? actorRecord.Name;
                        }
                    }
                    ui.nameText.text = characterDisplayName;
                    ui.classText.text = stats.characterClass.displayName;

                    if (dialogueActor != null)
                    {
                        var actorRecord = DialogueManager.MasterDatabase.GetActor(dialogueActor.actor);
                        if (actorRecord != null && actorRecord.portrait != null)
                        {
                            var portraitTex = actorRecord.portrait;
                            ui.portraitImage.sprite = Sprite.Create(portraitTex, new Rect(0, 0, portraitTex.width, portraitTex.height), new Vector2(0.5f, 0.5f));
                        }
                    }
                }

                if (health != null) { health.OnHealthChanged -= () => UpdateHealthBar(ui, health); health.OnHealthChanged += () => UpdateHealthBar(ui, health); UpdateHealthBar(ui, health); }
                if (statusHolder != null) { holderToUIMap[statusHolder] = ui; statusHolder.OnEffectsChanged -= RefreshStatusEffects; statusHolder.OnEffectsChanged += RefreshStatusEffects; RefreshStatusEffects(statusHolder); }
                if (ai != null) { ai.OnStatusChanged += HandleAIStatusChanged; ui.stanceButton.onClick.RemoveAllListeners(); ui.stanceButton.onClick.AddListener(() => CycleStance(player)); UpdateStanceIcon(ui, PartyAIManager.instance.GetStanceForCharacter(player)); }
            }
            else
            {
                // Empty party slots (e.g., Slot 4 and 5 if you only have 3 players)
                if (ui.canvasGroup != null)
                {
                    ui.canvasGroup.alpha = 0f;
                    ui.canvasGroup.interactable = false;
                    ui.canvasGroup.blocksRaycasts = false;
                }
                else
                {
                    // Fallback just in case you forgot to assign a Canvas Group
                    ui.portraitFrame.SetActive(false);
                }
            }
        }
        HandleActivePlayerChanged(partyManager.ActivePlayer);
    }

    #region Unchanged Code + Stance Visibility Protection
    private void HandleAIStatusChanged(PartyMemberAI ai, string newStatus) { if (playerToUIMap.TryGetValue(ai.gameObject, out PlayerPortraitUI ui)) { if (ui.statusText != null) { ui.statusText.text = newStatus; } } }
    private void HandleStanceChanged(GameObject character, AIStance newStance) { if (playerToUIMap.TryGetValue(character, out PlayerPortraitUI ui)) { UpdateStanceIcon(ui, newStance); } }
    private void CycleStance(GameObject character) { if (PartyAIManager.instance == null) return; AIStance currentStance = PartyAIManager.instance.GetStanceForCharacter(character); AIStance nextStance = (AIStance)(((int)currentStance + 1) % 3); PartyAIManager.instance.SetStanceForCharacter(character, nextStance); }
    private void UpdateStanceIcon(PlayerPortraitUI ui, AIStance stance) { if (ui.stanceIconImage == null) return; switch (stance) { case AIStance.Aggressive: ui.stanceIconImage.sprite = aggressiveIcon; break; case AIStance.Defensive: ui.stanceIconImage.sprite = defensiveIcon; break; case AIStance.Passive: ui.stanceIconImage.sprite = passiveIcon; break; } }
    private IEnumerator DelayedInitialRefresh() { yield return new WaitForEndOfFrame(); RefreshAllPortraits(); }
    private void UpdateHealthBar(PlayerPortraitUI ui, Health health) { if (ui == null || health == null) return; if (health.maxHealth > 0) ui.hpSlider.value = (float)health.currentHealth / health.maxHealth; else ui.hpSlider.value = 0; }
    private void RefreshStatusEffects(StatusEffectHolder holder) { if (holder == null || !holderToUIMap.ContainsKey(holder)) return; PlayerPortraitUI portraitUI = holderToUIMap[holder]; foreach (Transform child in portraitUI.statusEffectContainer) { Destroy(child.gameObject); } foreach (ActiveStatusEffect effect in holder.GetActiveEffects()) { GameObject iconGO = Instantiate(statusEffectIconPrefab, portraitUI.statusEffectContainer); iconGO.GetComponent<StatusEffectIconUI>().Initialize(effect); } }

    private void HandleActivePlayerChanged(GameObject activePlayer)
    {
        if (allPlayers == null) return;

        SceneInfo currentSceneInfo = FindAnyObjectByType<SceneInfo>();

        for (int i = 0; i < allPlayers.Count; i++)
        {
            var player = allPlayers[i];
            if (playerToUIMap.TryGetValue(player, out var ui))
            {
                float targetScale = (player == activePlayer) ? activeScale : inactiveScale;
                ui.portraitFrame.transform.DOScale(targetScale, scaleDuration);

                if (ui.statusText != null)
                {
                    ui.statusText.text = (player == activePlayer) ? "Player Controlled" : player.GetComponent<PartyMemberAI>()?.CurrentStatus ?? "";
                }

                if (ui.stanceButton != null)
                {
                    bool isControllable = true;
                    if (currentSceneInfo != null && i < currentSceneInfo.playerConfigs.Count)
                    {
                        var state = currentSceneInfo.playerConfigs[i].state;
                        if (state == PlayerSceneState.Inactive || state == PlayerSceneState.SpawnAtMarker || state == PlayerSceneState.Hidden)
                        {
                            isControllable = false;
                        }
                    }
                    ui.stanceButton.gameObject.SetActive(player != activePlayer && isControllable);
                }
            }
        }
    }

    private void HandleStatsPanelToggle(bool isPanelOpen) { if (rectTransform == null || statsOpenPositionAnchor == null) return; Vector3 targetPosition = isPanelOpen ? statsOpenPositionAnchor.anchoredPosition : originalPosition; rectTransform.DOAnchorPos(targetPosition, moveDuration).SetEase(Ease.OutCubic); }
    private void HandlePartyInventoryToggle(bool isPanelOpen) { canvasGroup.alpha = isPanelOpen ? 0 : 1; canvasGroup.interactable = !isPanelOpen; canvasGroup.blocksRaycasts = !isPanelOpen; }
    public PlayerPortraitUI GetPortraitUIForPlayer(GameObject player) { playerToUIMap.TryGetValue(player, out var ui); return ui; }
    #endregion
}