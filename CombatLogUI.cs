using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;

public class CombatLogUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The TextMeshPro text element where the log will be displayed.")]
    public TextMeshProUGUI logText;
    [Tooltip("The ScrollRect component of this UI panel.")]
    public ScrollRect scrollRect;

    [Header("Log Settings")]
    [Tooltip("The maximum number of lines to keep in the log history.")]
    public int maxLines = 100;

    private List<string> logEntries = new List<string>();

    void Start()
    {
        // Start() is a good place for one-time setup, like clearing initial text.
        if (logText != null)
        {
            logText.text = "";
        }
    }

    // --- MODIFIED ---
    // OnEnable is called every time the object is activated.
    // This is the correct place to subscribe to events.
    void OnEnable()
    {
        if (CombatLogManager.instance != null)
        {
            CombatLogManager.instance.OnLogEntryAdded += AddEntryToUI;
        }
        else
        {
            Debug.LogError("[COMBAT LOG UI] Could not find CombatLogManager instance to subscribe to!", this);
        }
    }

    // OnDisable is correct as is. It's called every time the object is deactivated.
    void OnDisable()
    {
        if (CombatLogManager.instance != null)
        {
            CombatLogManager.instance.OnLogEntryAdded -= AddEntryToUI;
        }
    }

    private void AddEntryToUI(string entry)
    {
        if (logEntries.Count >= maxLines)
        {
            logEntries.RemoveAt(0);
        }
        logEntries.Add(entry);

        logText.text = string.Join("\n", logEntries);

        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}