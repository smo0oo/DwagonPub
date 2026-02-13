using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SceneQuickButtons : EditorWindow
{
    [MenuItem("Window/Scene Quick Buttons")]
    public static void ShowWindow()
    {
        GetWindow<SceneQuickButtons>("Scenes");
    }

    void OnGUI()
    {
        if (GUILayout.Button("Load BootStrapper"))
        {
            LoadScene("Assets/Scenes/Bootstrapper.unity");
        }
        if (GUILayout.Button("Load Main Menu"))
        {
            LoadScene("Assets/Scenes/Menu/MainMenu.unity");
        }
        if (GUILayout.Button("Load Core"))
        {
            LoadScene("Assets/Scenes/Core/CoreScene.unity");
        }
        if (GUILayout.Button("Load World Map"))
        {
            LoadScene("Assets/Scenes/WorldMap/WorldMap_A.unity");
        }
        if (GUILayout.Button("Load Ambush A"))
        {
            LoadScene("Assets/Scenes/Battle/Ambush/Ambush_Basic_01.unity");
        }
        if (GUILayout.Button("Load Town A"))
        {
            LoadScene("Assets/Scenes/Battle/Towns/Town_Large_A.unity");
        }
        if (GUILayout.Button("Load Dungeon A"))
        {
            LoadScene("Assets/Scenes/Battle/Dungeons/Dungeon_Basic_A.unity");
        }
        if (GUILayout.Button("Load Intro Cin"))
        {
            LoadScene("Assets/Scenes/Battle/Cinematic/DW_Cin_Intro_01.unity");
        }
    }

    void LoadScene(string path)
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(path);
    }
}
