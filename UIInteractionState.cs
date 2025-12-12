// UIInteractionState.cs

/// <summary>
/// A simple static class to hold the global state of UI interactions.
/// This provides a foolproof way for systems like player movement to know
/// if a UI element should be blocking input, avoiding race conditions
/// with the EventSystem.
/// </summary>
public static class UIInteractionState
{
    /// <summary>
    /// Set to true by any UI panel/menu that should block player movement clicks.
    /// </summary>
    public static bool IsUIBlockingInput { get; set; } = false;
}