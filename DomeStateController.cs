using UnityEngine;

public class DomeStateController : MonoBehaviour
{
    [Header("Components to Toggle")]
    [Tooltip("Assign the child object that contains the Dome's main visuals.")]
    public GameObject domeVisuals;
    [Tooltip("Assign the main SphereCollider component of the Dome.")]
    public Collider domeCollider;
    [Tooltip("Assign the DomeAI component.")]
    public DomeAI domeAI;
    [Tooltip("Assign the DomeAbilityHolder component.")]
    public DomeAbilityHolder domeAbilityHolder;

    private SceneType lastKnownSceneType = SceneType.MainMenu;

    void Start()
    {
        UpdateDomeState(GameManager.instance.currentSceneType);
    }

    void Update()
    {
        if (GameManager.instance == null) return;

        SceneType currentSceneType = GameManager.instance.currentSceneType;

        if (currentSceneType != lastKnownSceneType)
        {
            UpdateDomeState(currentSceneType);
        }
    }

    // --- THIS METHOD HAS BEEN MODIFIED ---
    private void UpdateDomeState(SceneType newSceneType)
    {
        lastKnownSceneType = newSceneType;

        bool shouldBeActive = (newSceneType == SceneType.DomeBattle);

        if (domeVisuals != null) domeVisuals.SetActive(shouldBeActive);
        if (domeCollider != null) domeCollider.enabled = shouldBeActive;
        if (domeAI != null) domeAI.enabled = shouldBeActive;
        if (domeAbilityHolder != null) domeAbilityHolder.enabled = shouldBeActive;

        // --- NEW LOGIC TO LINK THE DOME AND ITS UI ---
        if (shouldBeActive)
        {
            // When the Dome activates, find the UI Manager and link them.
            // --- FIX: Replaced FindObjectOfType with FindAnyObjectByType ---
            DomeUIManager uiManager = FindAnyObjectByType<DomeUIManager>();

            // We use the singleton instance to get the controller
            if (uiManager != null && DomeController.instance != null)
            {
                // Call the new public method on DomeController to establish the link
                DomeController.instance.LinkUIManager(uiManager);
                uiManager.InitializeAndShow(DomeController.instance);
            }
        }
        // --- END OF NEW LOGIC ---

        Debug.Log($"Dome state updated for scene type '{newSceneType}'. Active: {shouldBeActive}");
    }
}