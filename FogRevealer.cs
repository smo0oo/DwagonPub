using UnityEngine;

public class FogRevealer : MonoBehaviour
{
    void Update()
    {
        if (FogOfWarManager.instance != null)
        {
            // Simply pass the wagon's current 3D position to the manager
            FogOfWarManager.instance.PaintFog(transform.position);
        }
    }
}