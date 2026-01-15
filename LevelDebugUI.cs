using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using Unity.VisualScripting;

public class LevelDebugUI : MonoBehaviour
{
    [Header("UI Panel")]
    public GameObject debugPanel;

    [Header("Level/XP References")]
    public TMP_InputField xpToAddInput;
    public Button addXPButton;
    public TMP_InputField setLevelInput;
    public Button setLevelButton;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;

    [Header("Enemy Spawning")]
    public List<GameObject> enemyPrefabs;
    public TMP_Dropdown enemyDropdown;
    public Button spawnEnemyButton;
    public LayerMask groundLayer;

    [Header("Party Damage/Heal")]
    public TMP_InputField healAmountInput;
    public Button healAllButton;
    public TMP_InputField damageAmountInput;
    public Button damageAllButton;

    [Header("Party God Mode")]
    public Toggle godModeToggle;

    private PartyManager Party => PartyManager.instance;

    private Camera mainCamera;
    private bool isPlacingEnemy = false;

    void Start()
    {
        mainCamera = Camera.main;

        addXPButton?.onClick.AddListener(OnAddXPClicked);
        setLevelButton?.onClick.AddListener(OnSetLevelClicked);
        spawnEnemyButton?.onClick.AddListener(OnSpawnEnemyClicked);
        healAllButton?.onClick.AddListener(OnHealAllClicked);
        damageAllButton?.onClick.AddListener(OnDamageAllClicked);
        godModeToggle?.onValueChanged.AddListener(OnGodModeToggled);

        PopulateEnemyDropdown();

        if (godModeToggle != null) godModeToggle.isOn = false;
        if (debugPanel != null) debugPanel.SetActive(false);

        if (Party != null)
        {
            Party.OnLevelUp += UpdateUI;
            Party.OnXPChanged += UpdateUI;
            UpdateUI();
        }
    }

    void OnDestroy()
    {
        if (PartyManager.instance != null)
        {
            PartyManager.instance.OnLevelUp -= UpdateUI;
            PartyManager.instance.OnXPChanged -= UpdateUI;
        }
    }

    void Update()
    {
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu) return;

        if (Input.GetKeyDown(KeyCode.G))
        {
            if (debugPanel != null) debugPanel.SetActive(!debugPanel.activeSelf);
        }

        if (isPlacingEnemy)
        {
            if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                PlaceEnemyAtMousePosition();
                isPlacingEnemy = false;
            }
            if (Input.GetMouseButtonDown(1))
            {
                isPlacingEnemy = false;
                Debug.Log("Enemy placement cancelled.");
            }
        }
    }

    private void OnGodModeToggled(bool isEnabled)
    {
        if (Party == null)
        {
            Debug.LogWarning("LevelDebugUI: PartyManager not found!");
            return;
        }

        List<GameObject> allPlayers = Party.partyMembers;
        if (allPlayers == null) return;

        Debug.Log($"<color=orange>DEBUG: God Mode {(isEnabled ? "ENABLED" : "DISABLED")} for all players.</color>");

        foreach (var player in allPlayers)
        {
            if (player == null) continue;

            Health health = player.GetComponentInChildren<Health>();

            if (health == null)
            {
                var root = player.GetComponentInParent<CharacterRoot>();
                if (root != null) health = root.Health;
            }

            if (health != null)
            {
                // --- FIX: Use the specific debug flag, so status effects don't overwrite it ---
                health.debugGodMode = isEnabled;
                // -----------------------------------------------------------------------------
            }
            else
            {
                Debug.LogWarning($"LevelDebugUI: Could not find Health component on party member '{player.name}'");
            }
        }
    }

    private void OnHealAllClicked()
    {
        if (Party == null || healAmountInput == null) return;
        if (int.TryParse(healAmountInput.text, out int amount))
        {
            foreach (var player in Party.partyMembers)
            {
                if (player == null) continue;
                Health health = player.GetComponentInChildren<Health>() ?? player.GetComponentInParent<CharacterRoot>()?.Health;
                if (health != null) health.Heal(amount, this.gameObject);
            }
            Debug.Log($"<color=green>DEBUG: Healed Party for {amount}.</color>");
        }
    }

    private void OnDamageAllClicked()
    {
        if (Party == null || damageAmountInput == null) return;
        if (int.TryParse(damageAmountInput.text, out int amount))
        {
            foreach (var player in Party.partyMembers)
            {
                if (player == null) continue;
                Health health = player.GetComponentInChildren<Health>() ?? player.GetComponentInParent<CharacterRoot>()?.Health;
                if (health != null) health.TakeDamage(amount, DamageEffect.DamageType.Physical, false, this.gameObject);
            }
            Debug.Log($"<color=red>DEBUG: Damaged Party for {amount}.</color>");
        }
    }

    private void UpdateUI()
    {
        if (Party == null) { ClearUI(); return; }
        if (levelText != null) levelText.text = $"Level: {Party.partyLevel}";
        if (xpText != null) xpText.text = $"XP: {Party.currentXP} / {Party.xpToNextLevel}";
    }

    private void ClearUI()
    {
        if (levelText != null) levelText.text = "Level: --";
        if (xpText != null) xpText.text = "XP: -- / --";
    }

    private void OnAddXPClicked()
    {
        if (Party != null && xpToAddInput != null && int.TryParse(xpToAddInput.text, out int amount))
            Party.AddExperience(amount);
    }

    private void OnSetLevelClicked()
    {
        if (Party != null && setLevelInput != null && int.TryParse(setLevelInput.text, out int level))
            Party.SetLevel(level);
    }

    private void PopulateEnemyDropdown()
    {
        if (enemyDropdown == null || enemyPrefabs == null) return;
        enemyDropdown.ClearOptions();
        if (enemyPrefabs.Count > 0)
        {
            List<string> enemyNames = enemyPrefabs.Select(prefab => prefab.name).ToList();
            enemyDropdown.AddOptions(enemyNames);
            enemyDropdown.interactable = true;
            spawnEnemyButton.interactable = true;
        }
        else
        {
            enemyDropdown.AddOptions(new List<string> { "No Prefabs Assigned" });
            enemyDropdown.interactable = false;
            spawnEnemyButton.interactable = false;
        }
    }

    private void OnSpawnEnemyClicked()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0) return;
        isPlacingEnemy = true;
        Debug.Log("Now in placement mode. Left-click on the ground to spawn. Right-click to cancel.");
    }

    private void PlaceEnemyAtMousePosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayer))
        {
            int selectedIndex = enemyDropdown.value;
            GameObject prefabToSpawn = enemyPrefabs[selectedIndex];
            Instantiate(prefabToSpawn, hit.point, Quaternion.identity);
            Debug.Log($"Spawned '{prefabToSpawn.name}' at {hit.point}");
        }
        else
        {
            Debug.LogWarning("Spawn Enemy cancelled: Click was not on valid ground.");
        }
    }
}