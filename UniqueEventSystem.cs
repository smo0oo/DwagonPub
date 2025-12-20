using UnityEngine;
using System.Collections.Generic;

public class UniqueEventSystem : MonoBehaviour
{
    public static UniqueEventSystem instance;

    // A collection to store unique IDs of completed events/cinematics
    private HashSet<string> completedEventIDs = new HashSet<string>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    /// <summary>
    /// Records an event as finished so it doesn't trigger again.
    /// </summary>
    public void MarkEventAsCompleted(string eventID)
    {
        if (string.IsNullOrEmpty(eventID)) return;

        if (!completedEventIDs.Contains(eventID))
        {
            completedEventIDs.Add(eventID);
            Debug.Log($"[UniqueEventSystem] Event Registered: {eventID}");
        }
    }

    /// <summary>
    /// Checks if a specific event ID has already been completed.
    /// </summary>
    public bool IsEventCompleted(string eventID)
    {
        if (string.IsNullOrEmpty(eventID)) return false;
        return completedEventIDs.Contains(eventID);
    }
}