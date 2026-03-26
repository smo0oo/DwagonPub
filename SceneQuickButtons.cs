using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Overlays;

[Overlay(typeof(SceneView), "Scene Quick Loaders", true)]
public class SceneQuickButtonsOverlay : IMGUIOverlay
{
    public override void OnGUI()
    {
        GUILayout.BeginVertical();

        // --- ROW 1: System & Core ---
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("▶ Bootstrapper", "Load Bootstrapper Scene"), EditorStyles.miniButtonLeft, GUILayout.Height(24)))
            LoadScene("Assets/Scenes/Bootstrapper.unity");

        if (GUILayout.Button(new GUIContent("🏠 Main Menu", "Load Main Menu Scene"), EditorStyles.miniButtonMid, GUILayout.Height(24)))
            LoadScene("Assets/Scenes/Menu/MainMenu.unity");

        if (GUILayout.Button(new GUIContent("⚙️ Core", "Load Core Scene"), EditorStyles.miniButtonRight, GUILayout.Height(24)))
            LoadScene("Assets/Scenes/Core/CoreScene.unity");
        GUILayout.EndHorizontal();

        // --- ROW 2: World & Narrative ---
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("🗺️ World Map", "Load World Map"), EditorStyles.miniButtonLeft, GUILayout.Height(24)))
            LoadScene("Assets/Scenes/WorldMap/WorldMap_A.unity");

        if (GUILayout.Button(new GUIContent("🏡 Town A", "Load Town A"), EditorStyles.miniButtonMid, GUILayout.Height(24)))
            LoadScene("Assets/Scenes/Battle/Towns/Town_Large_A.unity");

        if (GUILayout.Button(new GUIContent("🎬 Intro Cin", "Load Intro Cinematic"), EditorStyles.miniButtonRight, GUILayout.Height(24)))
            LoadScene("Assets/Scenes/Battle/Cinematic/DW_Cin_Intro_01.unity");
        GUILayout.EndHorizontal();

        // --- ROW 3: Combat Encounters ---
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("⚔️ Ambush A", "Load Ambush A"), EditorStyles.miniButtonLeft, GUILayout.Height(24)))
            LoadScene("Assets/Scenes/Battle/Ambush/Ambush_Basic_01.unity");

        if (GUILayout.Button(new GUIContent("💀 Dungeon A", "Load Dungeon A"), EditorStyles.miniButtonRight, GUILayout.Height(24)))
            LoadScene("Assets/Scenes/Battle/Dungeons/Dungeon_Basic_A.unity");
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void LoadScene(string path)
    {
        // Prompts the user to save any unsaved changes before switching scenes
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(path);
        }
    }
}