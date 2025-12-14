using UnityEngine;

public class DualModeDebugger : MonoBehaviour
{
    // Drag your DualModeSetupPanel here in the Inspector
    public DualModeSetupUI setupUI;

    void Update()
    {
        // Press 'T' on your keyboard to toggle the menu
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (setupUI != null && setupUI.panelRoot != null)
            {
                if (setupUI.panelRoot.activeSelf)
                {
                    Debug.Log("Debugging: Closing Dual Mode Setup.");
                    setupUI.CloseSetup();
                }
                else
                {
                    Debug.Log("Debugging: Opening Dual Mode Setup.");
                    setupUI.OpenSetup();
                }
            }
            else
            {
                Debug.LogError("Debugger: DualModeSetupUI reference or PanelRoot is missing!");
            }
        }
    }
}