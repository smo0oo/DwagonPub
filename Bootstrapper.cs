using UnityEngine;
using System.Collections;

public class Bootstrapper : MonoBehaviour
{
    // Define your scene names here for easy editing
    public string coreSceneName = "CoreScene";
    public string firstSceneName = "IntroScene";

    [Header("Build Configuration")]
    [Tooltip("Assign your BuildReferenceHolder asset here to force-include VFX/Shaders.")]
    public BuildReferenceHolder buildRefs; // <--- CRUCIAL STEP ENACTED

    IEnumerator Start()
    {
        // Load the persistent systems scene.
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(coreSceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);

        // Wait a frame to ensure all Awake() methods in CoreScene have run.
        yield return null;

        // Hand off control to the GameManager.
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadLevel(firstSceneName);
        }
        else
        {
            Debug.LogError("FATAL: GameManager not found. Cannot load game.");
        }
    }
}