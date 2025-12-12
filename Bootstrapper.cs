using UnityEngine;
using System.Collections;

public class Bootstrapper : MonoBehaviour
{
    IEnumerator Start()
    {
        // Load the persistent systems scene.
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("CoreScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);

        // Wait a frame to ensure all Awake() methods in CoreScene have run.
        yield return null;

        // Hand off control to the GameManager to load the MainMenu.
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadLevel("MainMenu");
        }
        else
        {
            Debug.LogError("FATAL: GameManager not found. Cannot load MainMenu.");
        }
    }
}