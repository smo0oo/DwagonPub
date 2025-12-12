using UnityEngine;
using UnityEngine.EventSystems;

public class UniqueEventSystem : MonoBehaviour
{
    // The static instance that will hold our single EventSystem.
    public static UniqueEventSystem instance;

    void Awake()
    {
        // Check if an instance of this script already exists.
        if (instance == null)
        {
            // If not, this becomes the instance.
            instance = this;
            // And we make sure it persists across all scenes.
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            // If an instance already exists (our persistent one),
            // then this new one is a duplicate and must be destroyed.
            Destroy(this.gameObject);
        }
    }
}