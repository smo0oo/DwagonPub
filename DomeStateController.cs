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
        if (GameManager.instance != null)
        {
            UpdateDomeState(GameManager.instance.currentSceneType);
        }
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

    private void UpdateDomeState(SceneType newSceneType)
    {
        lastKnownSceneType = newSceneType;

        bool shouldBeActive = (newSceneType == SceneType.DomeBattle);

        // 1. Toggle Local Components
        if (domeVisuals != null) domeVisuals.SetActive(shouldBeActive);
        if (domeCollider != null) domeCollider.enabled = shouldBeActive;
        if (domeAI != null) domeAI.enabled = shouldBeActive;
        if (domeAbilityHolder != null) domeAbilityHolder.enabled = shouldBeActive;

        // 2. --- FIX: Authoritatively Toggle the Main Controller ---
        // This ensures the Layer, Script State, and UI Logic follow the GameManager's decision
        // regardless of race conditions during scene load.
        if (DomeController.instance != null)
        {
            DomeController.instance.SetDomeActive(shouldBeActive);
        }
        // -----------------------------------------------------------

        if (shouldBeActive)
        {
            DomeUIManager uiManager = FindAnyObjectByType<DomeUIManager>();

            if (uiManager != null && DomeController.instance != null)
            {
                DomeController.instance.LinkUIManager(uiManager);
                uiManager.InitializeAndShow(DomeController.instance);
            }
        }

        Debug.Log($"Dome state updated for scene type '{newSceneType}'. Active: {shouldBeActive}");
    }
}