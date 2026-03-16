using UnityEngine;

public class TimeScaleDebugger : MonoBehaviour
{
    [Header("Configuration")]
    public bool showUI = true;

    [Tooltip("You can manually drag this in the Inspector, or use the on-screen slider.")]
    [Range(0f, 1f)]
    public float currentTimeScale = 1f;

    // We store the original fixed delta time (usually 0.02) so we can scale physics accurately
    private float defaultFixedDeltaTime;

    void Start()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    void Update()
    {
        // Apply the time scale
        Time.timeScale = currentTimeScale;

        // AAA FIX: Scale the physics step so cloth and rigidbodies don't stutter in slow-motion
        // Avoid setting it to literal 0 to prevent divide-by-zero errors in some physics engines
        if (currentTimeScale > 0.01f)
        {
            Time.fixedDeltaTime = defaultFixedDeltaTime * currentTimeScale;
        }
        else
        {
            Time.fixedDeltaTime = defaultFixedDeltaTime; // Fallback if perfectly paused
        }
    }

    private void OnGUI()
    {
        if (!showUI) return;

        // Define window dimensions and padding
        float windowWidth = 250f;
        float windowHeight = 90f;
        float padding = 10f;

        // Dynamically calculate the X position for the top right corner based on screen width
        float xPos = Screen.width - windowWidth - padding;
        float yPos = padding;

        // Draw a dark semi-transparent box in the top right corner
        GUILayout.BeginArea(new Rect(xPos, yPos, windowWidth, windowHeight), "Time Scale Debugger", GUI.skin.window);

        GUILayout.Space(10);

        // Display current speed as a percentage
        GUILayout.Label($"Game Speed: {Mathf.RoundToInt(currentTimeScale * 100)}%");

        // The functional slider (0.0 to 1.0)
        currentTimeScale = GUILayout.HorizontalSlider(currentTimeScale, 0f, 1f);

        GUILayout.Space(5);

        // Quick reset button
        if (GUILayout.Button("Reset to 100%"))
        {
            currentTimeScale = 1f;
        }

        GUILayout.EndArea();
    }

    void OnDisable()
    {
        // Safety net: ensure time goes back to normal if this object is deleted or disabled
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }
}