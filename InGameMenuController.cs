using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Required for EventSystem
using System.Collections;
using System.IO;

public class InGameMenuController : MonoBehaviour
{
    public static InGameMenuController instance;

    // Public property for other scripts to check pause state easily
    public static bool IsGamePaused => instance != null && instance.isMenuOpen;

    [Header("UI References")]
    [Tooltip("The parent GameObject of the entire in-game menu panel.")]
    public GameObject menuPanel;

    [Tooltip("The container holding buttons (drag your MainButtonsContainer here if you have one).")]
    public GameObject mainButtonsContainer; // Optional: If you use the container hiding logic

    [Tooltip("The button that saves the game.")]
    public Button saveButton;
    [Tooltip("The button that loads the game.")]
    public Button loadButton;
    [Tooltip("The button that returns to the main menu.")]
    public Button quitButton;
    [Tooltip("The button that resumes the game.")]
    public Button resumeButton;
    [Tooltip("The button that exits the application entirely (Desktop).")]
    public Button exitButton;

    // Optional: Options button if you implemented it
    public Button optionsButton;

    private bool isMenuOpen = false;

    void Awake()
    {
        if (instance != null && instance != this) Destroy(gameObject);
        else instance = this;
    }

    void Start()
    {
        if (menuPanel != null) menuPanel.SetActive(false);

        saveButton?.onClick.AddListener(OnSaveClicked);
        loadButton?.onClick.AddListener(OnLoadClicked);
        quitButton?.onClick.AddListener(OnQuitClicked);
        resumeButton?.onClick.AddListener(OnResumeClicked);
        exitButton?.onClick.AddListener(OnExitClicked);

        // Link Options if it exists in your prefab
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsClicked);
    }

    void Update()
    {
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Close Options if open
            if (OptionsMenuUI.instance != null && OptionsMenuUI.instance.contentPanel.activeSelf)
            {
                OptionsMenuUI.instance.CloseMenu();
            }
            else
            {
                ToggleMenu();
            }
        }
    }

    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (menuPanel != null) menuPanel.SetActive(isMenuOpen);

        if (isMenuOpen)
        {
            // --- PAUSE ---
            Time.timeScale = 0f;
            UIInteractionState.IsUIBlockingInput = true;

            // Ensure Cursor is visible for menu navigation
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Reset View
            if (mainButtonsContainer != null) mainButtonsContainer.SetActive(true);
            if (OptionsMenuUI.instance != null) OptionsMenuUI.instance.contentPanel.SetActive(false);

            if (loadButton != null)
            {
                string path = Path.Combine(Application.persistentDataPath, "savegame.json");
                loadButton.interactable = File.Exists(path);
            }
        }
        else
        {
            // --- RESUME ---
            // Use Coroutine to ensure the frame finishes before unlocking gameplay
            StartCoroutine(RestoreGameplayState());
        }
    }

    private IEnumerator RestoreGameplayState()
    {
        // 1. Wait for end of frame to prevent ESC key from triggering game logic immediately
        yield return new WaitForEndOfFrame();

        Time.timeScale = 1f;
        UIInteractionState.IsUIBlockingInput = false;

        // --- FIX: ARPG CURSOR SETTINGS ---
        // For a Top-Down/ARPG, we generally want the cursor FREE and VISIBLE.
        // Do NOT use CursorLockMode.Locked (that is for FPS).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // ---------------------------------

        // 2. Deselect UI buttons so pressing 'Space' doesn't trigger them again
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void OnOptionsClicked()
    {
        if (OptionsMenuUI.instance != null)
        {
            if (mainButtonsContainer != null) mainButtonsContainer.SetActive(false);
            OptionsMenuUI.instance.OpenMenu();
        }
    }

    public void CloseOptions()
    {
        if (mainButtonsContainer != null) mainButtonsContainer.SetActive(true);
    }

    private void OnSaveClicked()
    {
        if (SaveManager.instance != null)
        {
            SaveManager.instance.SaveGame();
            if (loadButton != null) loadButton.interactable = true;
        }
    }

    private void OnLoadClicked()
    {
        if (SaveManager.instance != null)
        {
            ToggleMenu(); // Unpause before loading
            SaveManager.instance.LoadGame();
        }
    }

    private void OnQuitClicked()
    {
        if (GameManager.instance != null)
        {
            if (isMenuOpen) ToggleMenu(); // Reset TimeScale
            GameManager.instance.ReturnToMainMenu();
        }
    }

    private void OnResumeClicked() => ToggleMenu();

    private void OnExitClicked()
    {
        Debug.Log("Exiting Application...");
        Application.Quit();
    }
}