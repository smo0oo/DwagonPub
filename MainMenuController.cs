using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Buttons")]
    public Button newGameButton;
    public Button loadGameButton;
    public Button optionsButton;
    public Button quitButton;

    [Header("Debug / Dev Buttons")]
    [Tooltip("Button to instantly load the town scene for testing.")]
    public Button debugTownButton;

    [Header("Scene Config")]
    [Tooltip("The name of the scene to load when the 'Town' button is clicked.")]
    public string debugTownSceneName = "Town_Large_A";

    void Start()
    {
        // Standard Button Listeners
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
        if (loadGameButton != null) loadGameButton.onClick.AddListener(OnLoadGame);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitGame);

        // Options Listener
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsClicked);

        // Town Loader Listener
        if (debugTownButton != null) debugTownButton.onClick.AddListener(LoadTownScene);

        CheckSaveFile();
    }

    public void OnOptionsClicked()
    {
        // 1. Try Singleton first (Fastest)
        if (OptionsMenuUI.instance != null)
        {
            OptionsMenuUI.instance.OpenMenu();
            return;
        }

        // 2. Fallback search (Safety)
        // --- FIX: Updated for Unity 2023+ to resolve CS0618 warning ---
        // We use FindFirstObjectByType with FindObjectsInactive.Include 
        // to find the menu even if the GameObject is currently disabled.
        OptionsMenuUI foundOptions = FindFirstObjectByType<OptionsMenuUI>(FindObjectsInactive.Include);

        if (foundOptions != null)
        {
            foundOptions.OpenMenu();
        }
        else
        {
            Debug.LogError("MainMenuController: Could not find 'OptionsMenuUI'. Ensure your Core/UI scene is loaded.");
        }
    }

    public void LoadTownScene()
    {
        if (GameManager.instance != null)
        {
            // Reset dungeon flags to prevent UI issues in town
            GameManager.instance.SetJustExitedDungeon(false);

            // Load the town directly
            GameManager.instance.LoadLevel(debugTownSceneName);
        }
        else
        {
            Debug.LogWarning("GameManager instance not found! Cannot load town.");
        }
    }

    private void CheckSaveFile()
    {
        if (loadGameButton != null)
        {
            string path = Path.Combine(Application.persistentDataPath, "savegame.json");
            loadGameButton.interactable = File.Exists(path);
        }
    }

    public void OnNewGame()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.StartNewGame();
        }
    }

    public void OnLoadGame()
    {
        if (SaveManager.instance != null)
        {
            SaveManager.instance.LoadGame();
        }
    }

    public void OnQuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}