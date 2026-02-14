using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class LevelGeneratorFromImage : EditorWindow
{
    private Texture2D blueprintTexture;
    private float gridSize = 5f;

    // Global tweak to fix "Backwards" models instantly
    private float globalRotationOffset = 180f;

    public List<ColorToPrefabMapping> mappings = new List<ColorToPrefabMapping>();

    private SerializedObject serializedObject;
    private SerializedProperty mappingsProperty;

    private string newConfigName = "NewDungeonConfig";
    private int selectedConfigIndex = 0;
    private GeneratorConfig[] allConfigs;
    private string[] configNames;

    // --- Internal State for Generation ---
    private TileType[,] typeGrid;
    private int width;
    private int height;

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

        globalRotationOffset = EditorGUILayout.FloatField("Global Rotation Fix", globalRotationOffset);

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

    // --- GENERATION LOGIC ---

    private void GenerateLevel()
    {
        GameObject levelParentGO = GameObject.Find(blueprintTexture.name + " Level");
        if (levelParentGO != null) DestroyImmediate(levelParentGO);
        Transform levelParent = new GameObject(blueprintTexture.name + " Level").transform;

        width = blueprintTexture.width;
        height = blueprintTexture.height;
        Color[] allPixels = blueprintTexture.GetPixels();

        // Pass 1: Build the Type Grid
        typeGrid = new TileType[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixelColor = allPixels[y * width + x];
                if (pixelColor.a == 0)
                {
                    typeGrid[x, y] = TileType.None;
                    continue;
                }

                ColorToPrefabMapping match = FindMapping(pixelColor);
                if (match != null) typeGrid[x, y] = match.type;
                else typeGrid[x, y] = TileType.None;
            }
        }

        // Pass 2: Instantiate with Context Logic
        float pivotOffset = gridSize / 2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixelColor = allPixels[y * width + x];
                if (pixelColor.a == 0) continue;

                ColorToPrefabMapping mapping = FindMapping(pixelColor);

                if (mapping != null)
                {
                    Vector3 position = new Vector3(x * gridSize + pivotOffset, 0, y * gridSize + pivotOffset);
                    GameObject prefabToSpawn = mapping.prefab;

                    float rotationY = mapping.manualYRotation;

                    // --- DOOR LOGIC ---
                    if (mapping.type == TileType.Door)
                    {
                        // Check specifically for Walls
                        bool w_isWall = IsWall(x - 1, y);
                        bool e_isWall = IsWall(x + 1, y);
                        bool n_isWall = IsWall(x, y + 1);
                        bool s_isWall = IsWall(x, y - 1);

                        // Horizontal Alignment (Walls East & West)
                        // If door pivot is at one end (e.g. Left), Rot 0 places hinge on West.
                        if (w_isWall && e_isWall)
                        {
                            rotationY = 0f;
                        }
                        // Vertical Alignment (Walls North & South)
                        // If door pivot is at one end, Rot 90 places hinge on South.
                        else if (n_isWall && s_isWall)
                        {
                            rotationY = 90f;
                        }
                        // Fallback / Single Wall Connection
                        // If we are a door at the end of a corridor (one side floor, one side wall)
                        // we attach the hinge to the Wall.
                        else if (w_isWall) rotationY = 0f;
                        else if (e_isWall) rotationY = 180f;
                        else if (s_isWall) rotationY = 90f;
                        else if (n_isWall) rotationY = 270f;
                    }

                    // --- WALL LOGIC (Unchanged) ---
                    else if (mapping.type == TileType.Wall)
                    {
                        bool n = IsTiledFloor(x, y + 1);
                        bool s = IsTiledFloor(x, y - 1);
                        bool e = IsTiledFloor(x + 1, y);
                        bool w = IsTiledFloor(x - 1, y);

                        bool ne = IsTiledFloor(x + 1, y + 1);
                        bool nw = IsTiledFloor(x - 1, y + 1);
                        bool se = IsTiledFloor(x + 1, y - 1);
                        bool sw = IsTiledFloor(x - 1, y - 1);

                        if (n && s && e && w)
                        {
                            if (mapping.pillarPrefab != null) prefabToSpawn = mapping.pillarPrefab;
                        }
                        else if (mapping.externalCornerPrefab != null && ((n && e) || (n && w) || (s && e) || (s && w)))
                        {
                            prefabToSpawn = mapping.externalCornerPrefab;
                            if (n && e) rotationY = 0f;
                            else if (s && e) rotationY = 90f;
                            else if (s && w) rotationY = 180f;
                            else if (n && w) rotationY = 270f;
                        }
                        else if (mapping.internalCornerPrefab != null && ((!n && !e && ne) || (!n && !w && nw) || (!s && !e && se) || (!s && !w && sw)))
                        {
                            prefabToSpawn = mapping.internalCornerPrefab;
                            if (!n && !e && ne) rotationY = 0f;
                            else if (!s && !e && se) rotationY = 90f;
                            else if (!s && !w && sw) rotationY = 180f;
                            else if (!n && !w && nw) rotationY = 270f;
                        }
                        else if (prefabToSpawn == mapping.prefab)
                        {
                            if (n) rotationY = 0f;
                            else if (e) rotationY = 90f;
                            else if (s) rotationY = 180f;
                            else if (w) rotationY = 270f;
                        }
                    }

                    float finalRotation = rotationY + globalRotationOffset;
                    InstantiatePrefab(prefabToSpawn, position, Quaternion.Euler(0, finalRotation, 0), levelParent);
                }
            }
        }
        Debug.Log("Context-Aware Level generation complete!");
    }

    private ColorToPrefabMapping FindMapping(Color c)
    {
        foreach (var m in mappings)
        {
            if (IsColorSimilar(m.colorKey, c)) return m;
        }
        return null;
    }

    private bool IsColorSimilar(Color a, Color b, float tolerance = 0.01f)
    {
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance &&
               Mathf.Abs(a.a - b.a) < tolerance;
    }

    private bool IsTiledFloor(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return typeGrid[x, y] == TileType.Floor;
    }

    private bool IsWall(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return typeGrid[x, y] == TileType.Wall;
    }

    private void InstantiatePrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null) return;
        GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        newObj.transform.position = position;
        newObj.transform.rotation = rotation;
    }
}