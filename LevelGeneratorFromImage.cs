using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// The ColorToPrefabMapping class is NO LONGER defined inside this script.
// It now exists in its own file, solving the duplicate definition error.

public class LevelGeneratorFromImage : EditorWindow
{
    private Texture2D blueprintTexture;
    private float gridSize = 5f;

    public List<ColorToPrefabMapping> mappings = new List<ColorToPrefabMapping>();

    private SerializedObject serializedObject;
    private SerializedProperty mappingsProperty;

    private string newConfigName = "NewDungeonConfig";
    private int selectedConfigIndex = 0;
    private GeneratorConfig[] allConfigs;
    private string[] configNames;

    [MenuItem("Tools/Level Generator From Image")]
    public static void ShowWindow()
    {
        GetWindow<LevelGeneratorFromImage>("Level Generator");
    }

    private void OnEnable()
    {
        serializedObject = new SerializedObject(this);
        mappingsProperty = serializedObject.FindProperty("mappings");
        RefreshConfigList();
    }

    private void OnGUI()
    {
        GUILayout.Label("Level Generation Settings", EditorStyles.boldLabel);

        blueprintTexture = (Texture2D)EditorGUILayout.ObjectField("Blueprint Texture", blueprintTexture, typeof(Texture2D), false);
        gridSize = EditorGUILayout.FloatField("Grid Size (Meters)", gridSize);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Configuration Management", EditorStyles.boldLabel);

        if (configNames != null && configNames.Length > 0)
        {
            int newIndex = EditorGUILayout.Popup("Load Config", selectedConfigIndex, configNames);
            if (newIndex != selectedConfigIndex)
            {
                selectedConfigIndex = newIndex;
                LoadConfiguration();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No Generator Configs found in the project.", MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();
        newConfigName = EditorGUILayout.TextField("New Config Name", newConfigName);
        if (GUILayout.Button("Save Current"))
        {
            SaveConfiguration();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUILayout.Label("Color Mappings", EditorStyles.boldLabel);

        serializedObject.Update();
        EditorGUILayout.PropertyField(mappingsProperty, true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Level"))
        {
            if (blueprintTexture != null && mappings.Count > 0)
            {
                GenerateLevel();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Blueprint Texture and define at least one Mapping.", "OK");
            }
        }
    }

    private void RefreshConfigList()
    {
        string[] guids = AssetDatabase.FindAssets("t:GeneratorConfig");
        allConfigs = new GeneratorConfig[guids.Length];
        configNames = new string[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            allConfigs[i] = AssetDatabase.LoadAssetAtPath<GeneratorConfig>(path);
            configNames[i] = allConfigs[i].name;
        }
    }

    private void SaveConfiguration()
    {
        if (string.IsNullOrWhiteSpace(newConfigName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a valid name for the new configuration.", "OK");
            return;
        }

        GeneratorConfig newConfig = CreateInstance<GeneratorConfig>();
        newConfig.mappings = new List<ColorToPrefabMapping>(mappings);

        string path = $"Assets/GeneratorConfigs/{newConfigName}.asset";
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        AssetDatabase.CreateAsset(newConfig, path);
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newConfig;

        RefreshConfigList();
        Debug.Log($"Configuration saved to: {path}");
    }

    private void LoadConfiguration()
    {
        if (allConfigs != null && selectedConfigIndex < allConfigs.Length)
        {
            mappings = new List<ColorToPrefabMapping>(allConfigs[selectedConfigIndex].mappings);
            Debug.Log($"Loaded configuration: {allConfigs[selectedConfigIndex].name}");
        }
    }

    private void GenerateLevel()
    {
        GameObject levelParentGO = GameObject.Find(blueprintTexture.name + " Level");
        if (levelParentGO != null) DestroyImmediate(levelParentGO);
        Transform levelParent = new GameObject(blueprintTexture.name + " Level").transform;

        int width = blueprintTexture.width;
        int height = blueprintTexture.height;
        Color[] allPixels = blueprintTexture.GetPixels();

        Dictionary<Color, ColorToPrefabMapping> colorToMappingMap = mappings.ToDictionary(x => x.colorKey, x => x);
        var mappedColors = colorToMappingMap.Keys.ToList();

        float pivotOffset = gridSize / 2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixelColor = allPixels[y * width + x];
                if (pixelColor.a == 0) continue;

                if (colorToMappingMap.ContainsKey(pixelColor))
                {
                    ColorToPrefabMapping mapping = colorToMappingMap[pixelColor];

                    Vector3 position = new Vector3(x * gridSize + pivotOffset, 0, y * gridSize + pivotOffset);

                    int mask = CalculateBitmask(x, y, width, height, allPixels, mappedColors);
                    float autoYRotation = GetRotationForMask(mask);

                    float finalYRotation = autoYRotation + mapping.manualYRotation;

                    InstantiatePrefab(mapping.prefab, position, Quaternion.Euler(0, finalYRotation, 0), levelParent);
                }
            }
        }
        Debug.Log("Level generation complete!");
    }

    private int CalculateBitmask(int x, int y, int width, int height, Color[] pixels, List<Color> mappedColors)
    {
        int mask = 0;
        if (IsMappedColor(x, y + 1, width, height, pixels, mappedColors)) mask += 1; // North
        if (IsMappedColor(x + 1, y, width, height, pixels, mappedColors)) mask += 2; // East
        if (IsMappedColor(x, y - 1, width, height, pixels, mappedColors)) mask += 4; // South
        if (IsMappedColor(x - 1, y, width, height, pixels, mappedColors)) mask += 8; // West
        return mask;
    }

    private bool IsMappedColor(int x, int y, int width, int height, Color[] pixels, List<Color> mappedColors)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return mappedColors.Contains(pixels[y * width + x]);
    }

    private float GetRotationForMask(int mask)
    {
        switch (mask)
        {
            case 1: return 0;
            case 2: return 90;
            case 4: return 180;
            case 8: return 270;

            case 3: return 90;
            case 6: return 180;
            case 9: return 0;
            case 12: return 270;

            default: return 0;
        }
    }

    private void InstantiatePrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null) return;
        GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        newObj.transform.position = position;
        newObj.transform.rotation = rotation;
    }
}