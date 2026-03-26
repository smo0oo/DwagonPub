using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

// Registers the overlay to appear inside the Scene View window
[Overlay(typeof(SceneView), "Dwagon Command Bar", true)]
public class DwagonSceneOverlay : IMGUIOverlay
{
    public override void OnGUI()
    {
        GUILayout.BeginVertical();

        // --- ROW 1: World & Progression ---
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("🌍 Map", "Open World Map Architect"), EditorStyles.miniButtonLeft, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/World Map Forge");

        if (GUILayout.Button(new GUIContent("🌿 Forage", "Open Forage Event Forge"), EditorStyles.miniButtonMid, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/Forage Event Forge");

        if (GUILayout.Button(new GUIContent("🌳 Skills", "Open Progression Forge"), EditorStyles.miniButtonMid, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/Progression Forge");

        if (GUILayout.Button(new GUIContent("🛒 Wagon", "Open Wagon Workshop"), EditorStyles.miniButtonRight, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/Wagon Workshop Forge");

        GUILayout.EndHorizontal();

        // --- ROW 2: Entities, AI & Economy ---
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("⚔️ Abilities", "Open Ability Forge"), EditorStyles.miniButtonLeft, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/Ability Forge");

        if (GUILayout.Button(new GUIContent("🛡️ Items", "Open Item Forge"), EditorStyles.miniButtonMid, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/Item Forge");

        if (GUILayout.Button(new GUIContent("🐉 Bestiary", "Open Bestiary Forge"), EditorStyles.miniButtonMid, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/Bestiary Forge");

        // --- NEW: COMBAT AI ---
        if (GUILayout.Button(new GUIContent("🧠 AI", "Open Combat AI Architect"), EditorStyles.miniButtonMid, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/Combat AI Architect");

        if (GUILayout.Button(new GUIContent("💰 Loot", "Open Loot Balancer"), EditorStyles.miniButtonRight, GUILayout.Height(24)))
            EditorApplication.ExecuteMenuItem("Tools/DwagonPub/Loot Balancer Forge");

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }
}