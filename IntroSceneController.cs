using UnityEngine;

public class IntroSceneController : MonoBehaviour
{
    [Header("References")]
    public SequentialTextFader textFader;
    public string nextSceneName = "MainMenu";

    void Start()
    {
        if (textFader != null)
        {
            // Listen for the sequence to finish via code (backup)
            textFader.onSequenceComplete.AddListener(OnIntroFinished);
        }
        else
        {
            Debug.LogWarning("No TextFader assigned to IntroController. Skipping immediately.");
            OnIntroFinished();
        }
    }

    // --- FIX: Added 'public' so the Inspector can see it ---
    public void OnIntroFinished()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadLevel(nextSceneName);
        }
        else
        {
            // Fallback for testing without Bootstrapper
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        }
    }
}