using UnityEngine;
using UnityEngine.EventSystems;

public class WagonShopRotator : MonoBehaviour
{
    [Header("Settings")]
    public float rotationSpeed = 15f;
    public bool invertX = true;

    void Update()
    {
        // Only rotate if the Workshop UI is actually open
        if (WagonWorkshopUI.instance == null || !WagonWorkshopUI.instance.IsShopOpen())
            return;

        // Check for Left Mouse Button Drag
        if (Input.GetMouseButton(0))
        {
            // Optional: Check if mouse is NOT over a UI element (so buttons don't trigger rotation)
            // if (EventSystem.current.IsPointerOverGameObject()) return; 

            float rotX = Input.GetAxis("Mouse X") * rotationSpeed * Time.unscaledDeltaTime; // Use unscaled if game is paused

            if (invertX) rotX = -rotX;

            transform.Rotate(Vector3.up, rotX, Space.World);
        }
    }
}