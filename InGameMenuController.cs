using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class InGameMenuController : MonoBehaviour
{
    public static InGameMenuController instance;

    // --- ADDED: Public property for other scripts to check pause state easily ---
    public static bool IsGamePaused => instance != null && instance.isMenuOpen;

    [Header("UI References")]
    [Tooltip("The parent GameObject of the entire in-game menu panel.")]
    public GameObject menuPanel;
    [Tooltip("The button that saves the game.")]
    public Button saveButton;
    [Tooltip("The button that loads the game.")]
    public Button loadButton;
    [Tooltip("The button that returns to the main menu.")]
    public Button quitButton;
    [Tooltip("The button that resumes the game.")]
    public Button resumeButton;

    // --- ADDED: Reference for Exit to Desktop button ---
    [Tooltip("The button that exits the application entirely (Desktop).")]
    public Button exitButton;

    private bool isMenuOpen = false;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }
    }

    void Start()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        saveButton?.onClick.AddListener(OnSaveClicked);
        loadButton?.onClick.AddListener(OnLoadClicked);
        quitButton?.onClick.AddListener(OnQuitClicked);
        resumeButton?.onClick.AddListener(OnResumeClicked);

        // --- ADDED: Listener for exit button ---
        exitButton?.onClick.AddListener(OnExitClicked);
    }

    void Update()
    {
        // ADDED GUARD CLAUSE FOR MAIN MENU
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        menuPanel.SetActive(isMenuOpen);

        if (isMenuOpen)
        {
            Time.timeScale = 0f;
            UIInteractionState.IsUIBlockingInput = true;

            // --- ADDED: Unlock Cursor so player can click buttons ---
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (loadButton != null)
            {
                string path = Path.Combine(Application.persistentDataPath, "savegame.json");
                loadButton.interactable = File.Exists(path);
            }
        }
        else
        {
            Time.timeScale = 1f;
            UIInteractionState.IsUIBlockingInput = false;

            // --- ADDED: Lock Cursor again (Optional: remove if your game is point-and-click) ---
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnSaveClicked()
    {
        if (SaveManager.instance != null)
        {
            SaveManager.instance.SaveGame();
            if (loadButton != null)
            {
                loadButton.interactable = true;
            }
        }
    }

    private void OnLoadClicked()
    {
        if (SaveManager.instance != null)
        {
            ToggleMenu();
            SaveManager.instance.LoadGame();
        }
    }

    private void OnQuitClicked()
    {
        if (GameManager.instance != null)
        {
            // Reset TimeScale before leaving scene to ensure next scene runs
            if (isMenuOpen) ToggleMenu();

            GameManager.instance.ReturnToMainMenu();
        }
    }

    private void OnResumeClicked()
    {
        ToggleMenu();
    }

    // --- ADDED: Handler for Desktop Quit ---
    private void OnExitClicked()
    {
        Debug.Log("Exiting Application...");
        Application.Quit();
    }
}