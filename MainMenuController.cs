using UnityEngine;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Manages the buttons on the Main Menu scene.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("UI Buttons")]
    public Button newGameButton;
    public Button loadGameButton;
    public Button quitButton;

    [Tooltip("The name of the scene to load when the 'Town' button is clicked.")]
    public string debugTownSceneName = "Town_Large_A";

    void Start()
    {
        // Add listeners to the buttons to call the correct methods
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
        if (loadGameButton != null) loadGameButton.onClick.AddListener(OnLoadGame);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitGame);

        // Disable the "Load Game" button if no save file exists
        if (loadGameButton != null)
        {
            string path = Path.Combine(Application.persistentDataPath, "savegame.json");
            loadGameButton.interactable = File.Exists(path);
        }
    }

    public void OnNewGame()
    {
        // Tell the GameManager to start a new game
        if (GameManager.instance != null)
        {
            GameManager.instance.StartNewGame();
        }
    }

    public void OnLoadGame()
    {
        // Tell the SaveManager to load the game data
        if (SaveManager.instance != null)
        {
            SaveManager.instance.LoadGame();
        }
    }

    public void LoadTownScene()
    {
        if (GameManager.instance != null)
        {
            // Optional: Set specific flags if needed before loading
            // e.g. Ensure we aren't flagged as 'just exited dungeon' to prevent weird UI popups
            GameManager.instance.SetJustExitedDungeon(false);

            // Load the town directly
            GameManager.instance.LoadLevel(debugTownSceneName);
        }
    }

    public void OnQuitGame()
    {
        // Quit the application
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
