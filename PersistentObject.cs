using UnityEngine;

/// <summary>
/// A simple script that prevents this GameObject from being destroyed during scene loads.
/// </summary>
public class PersistentObject : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }
}