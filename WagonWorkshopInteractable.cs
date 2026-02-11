using UnityEngine;
using Cinemachine;
using PixelCrushers.DialogueSystem; // Required

public class WagonWorkshopInteractable : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("A Virtual Camera positioned to look nicely at the Wagon.")]
    public CinemachineVirtualCamera workshopCamera;

    // This method will be called by the Dialogue System
    public void OpenShop()
    {
        // 1. Activate Camera
        if (workshopCamera != null)
        {
            workshopCamera.Priority = 100;
            workshopCamera.gameObject.SetActive(true);
        }

        // 2. Open UI
        if (WagonWorkshopUI.instance != null)
        {
            // Pass 'CloseShop' as the callback so when UI closes, camera resets
            WagonWorkshopUI.instance.OpenWorkshop(CloseShop);
        }
        else
        {
            Debug.LogError("WagonWorkshopUI not found in scene!");
        }
    }

    private void CloseShop()
    {
        // Reset Camera
        if (workshopCamera != null)
        {
            workshopCamera.Priority = 0;
            workshopCamera.gameObject.SetActive(false);
        }
    }
}