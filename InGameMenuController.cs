using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class InGameMenuController : MonoBehaviour
{
    public static InGameMenuController instance;

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
            ToggleMenu();
            GameManager.instance.ReturnToMainMenu();
        }
    }

    private void OnResumeClicked()
    {
        ToggleMenu();
    }
}