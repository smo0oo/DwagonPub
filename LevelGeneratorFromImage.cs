using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class LevelGeneratorFromImage : EditorWindow
{
    private Texture2D blueprintTexture;
    private float gridSize = 10f;

    [Header("Boundary Settings")]
    public Color boundaryColor = Color.white;
    [Range(0f, 1f)] public float boundaryTolerance = 0.1f;

    [Header("Global Adjustments")]
    private float globalRotationOffset = 0f;
    private bool flipPathCorners = false;
    private bool showDebugLogs = true;

    public List<ColorToPrefabMapping> mappings = new List<ColorToPrefabMapping>();

    private SerializedObject serializedObject;
    private SerializedProperty mappingsProperty;

    // Config Restoration
    private string newConfigName = "NewDungeonConfig";
    private int selectedConfigIndex = 0;
    private GeneratorConfig[] allConfigs;
    private string[] configNames;

    // Grid Data
    private TileType[,] typeGrid;
    private bool[,] boundaryGrid;
    private bool[,] occupiedGrid;
    private int width;
    private int height;

    private Vector2 scrollPosition;

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
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("AAA Macro-Room Generator", EditorStyles.boldLabel);

        // --- CONFIG MANAGER ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Configuration Manager", EditorStyles.boldLabel);
        if (configNames != null && configNames.Length > 0)
        {
            int newIndex = EditorGUILayout.Popup("Load Preset", selectedConfigIndex, configNames);
            if (newIndex != selectedConfigIndex) { selectedConfigIndex = newIndex; LoadConfiguration(); }
        }
        else { GUILayout.Label("No Configs Found."); }

        EditorGUILayout.BeginHorizontal();
        newConfigName = EditorGUILayout.TextField("New Config Name", newConfigName);
        if (GUILayout.Button("Save Preset", GUILayout.Width(100))) SaveConfiguration();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        blueprintTexture = (Texture2D)EditorGUILayout.ObjectField("Blueprint Texture", blueprintTexture, typeof(Texture2D), false);
        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);

        EditorGUILayout.Space();
        GUILayout.Label("Boundary (Guide Only)", EditorStyles.boldLabel);
        boundaryColor = EditorGUILayout.ColorField("Boundary Color", boundaryColor);
        boundaryTolerance = EditorGUILayout.Slider("Tolerance", boundaryTolerance, 0.01f, 0.5f);

        EditorGUILayout.Space();
        GUILayout.Label("Logic Fixes", EditorStyles.boldLabel);
        globalRotationOffset = EditorGUILayout.FloatField("Global Rotation", globalRotationOffset);
        flipPathCorners = EditorGUILayout.Toggle("Flip Path Corners", flipPathCorners);
        showDebugLogs = EditorGUILayout.Toggle("Show Logs", showDebugLogs);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Level", GUILayout.Height(30)))
        {
            if (blueprintTexture != null && mappings.Count > 0) GenerateLevel();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Color Mappings", EditorStyles.boldLabel);

        serializedObject.Update();
        EditorGUILayout.PropertyField(mappingsProperty, true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(20);
        EditorGUILayout.EndScrollView();
    }

    private void GenerateLevel()
    {
        GameObject levelParentGO = GameObject.Find(blueprintTexture.name + " Level");
        if (levelParentGO != null) DestroyImmediate(levelParentGO);
        Transform levelParent = new GameObject(blueprintTexture.name + " Level").transform;

        width = blueprintTexture.width;
        height = blueprintTexture.height;
        Color32[] allPixels = blueprintTexture.GetPixels32();

        typeGrid = new TileType[width, height];
        boundaryGrid = new bool[width, height];
        occupiedGrid = new bool[width, height];

        // Pass 0: Pre-Scan
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color32 c = allPixels[y * width + x];
                if (IsColorSimilar(c, boundaryColor, boundaryTolerance)) { boundaryGrid[x, y] = true; typeGrid[x, y] = TileType.None; continue; }
                if (c.a == 0) { typeGrid[x, y] = TileType.None; continue; }

                ColorToPrefabMapping match = FindMapping(c);
                if (match != null) typeGrid[x, y] = match.type;
            }
        }

        // Pass 1: Macro Rooms (Islands)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (occupiedGrid[x, y] || boundaryGrid[x, y] || typeGrid[x, y] != TileType.Room) continue;

                Color32 c = allPixels[y * width + x];
                ColorToPrefabMapping mapping = FindMapping(c);

                if (mapping != null)
                {
                    List<Vector2Int> island = FloodFill(x, y, mapping.colorKey, allPixels);

                    int minX = width, minY = height, maxX = 0, maxY = 0;
                    foreach (var p in island)
                    {
                        if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                        if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
                        occupiedGrid[p.x, p.y] = true;
                    }

                    // Shape Dimensions (in tiles)
                    int roomWidthTiles = maxX - minX + 1;
                    int roomHeightTiles = maxY - minY + 1;

                    // Rotation Logic (Corner Missing Check)
                    float roomRotation = 0f;
                    if (!IsColorSimilar(GetPixel(maxX, maxY, allPixels), mapping.colorKey, boundaryTolerance)) roomRotation = 0f;       // Top-Right missing
                    else if (!IsColorSimilar(GetPixel(maxX, minY, allPixels), mapping.colorKey, boundaryTolerance)) roomRotation = 90f;  // Bottom-Right missing
                    else if (!IsColorSimilar(GetPixel(minX, minY, allPixels), mapping.colorKey, boundaryTolerance)) roomRotation = 180f; // Bottom-Left missing
                    else if (!IsColorSimilar(GetPixel(minX, maxY, allPixels), mapping.colorKey, boundaryTolerance)) roomRotation = 270f; // Top-Left missing

                    if (mapping.roomVariants.Count > 0)
                    {
                        GameObject prefab = mapping.roomVariants[Random.Range(0, mapping.roomVariants.Count)];

                        // Spawn at Bottom-Left (MinX, MinY)
                        Vector3 basePos = new Vector3(minX * gridSize, 0, minY * gridSize);

                        // Apply Correction for Rotation swinging the pivot
                        Vector3 offset = GetMacroRoomCorrection(roomRotation, roomWidthTiles, roomHeightTiles, gridSize);

                        InstantiatePrefab(prefab, basePos + offset, Quaternion.Euler(0, roomRotation + mapping.manualYRotation, 0), levelParent);
                    }
                }
            }
        }

        // Pass 2: Micro Tiles (Bitmasking)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (occupiedGrid[x, y] || boundaryGrid[x, y] || typeGrid[x, y] == TileType.None || typeGrid[x, y] == TileType.Room) continue;

                ColorToPrefabMapping mapping = FindMapping(allPixels[y * width + x]);
                if (mapping == null) continue;

                Vector3 pivot = new Vector3((x + 1) * gridSize, 0, (y + 1) * gridSize);
                float targetRot = 0f;
                GameObject spawn = mapping.prefab;

                bool n = IsPath(x, y + 1), e = IsPath(x + 1, y), s = IsPath(x, y - 1), w = IsPath(x - 1, y);

                if (mapping.type == TileType.Wall)
                {
                    int mask = (n ? 1 : 0) + (e ? 2 : 0) + (s ? 4 : 0) + (w ? 8 : 0);
                    if (mask == 1) targetRot = 0f + mapping.wallCorrections.faceNorthOffset;
                    else if (mask == 2) targetRot = 90f + mapping.wallCorrections.faceEastOffset;
                    else if (mask == 4) targetRot = 180f + mapping.wallCorrections.faceSouthOffset;
                    else if (mask == 8) targetRot = 270f + mapping.wallCorrections.faceWestOffset;
                    else if (mask == 3) { targetRot = 0f; if (mapping.externalCornerPrefab) spawn = mapping.externalCornerPrefab; }
                    else if (mask == 6) { targetRot = 90f; if (mapping.externalCornerPrefab) spawn = mapping.externalCornerPrefab; }
                    else if (mask == 12) { targetRot = 180f; if (mapping.externalCornerPrefab) spawn = mapping.externalCornerPrefab; }
                    else if (mask == 9) { targetRot = 270f; if (mapping.externalCornerPrefab) spawn = mapping.externalCornerPrefab; }
                    else if (mask == 0)
                    {
                        if (mapping.roomCornerPrefab) spawn = mapping.roomCornerPrefab;
                        bool ns = IsSolid(x, y + 1), es = IsSolid(x + 1, y), ss = IsSolid(x, y - 1), ws = IsSolid(x - 1, y);
                        if (ns && es) targetRot = 0f + mapping.cornerCorrections.topRightOffset;
                        else if (es && ss) targetRot = 90f + mapping.cornerCorrections.bottomRightOffset;
                        else if (ss && ws) targetRot = 180f + mapping.cornerCorrections.bottomLeftOffset;
                        else if (ws && ns) targetRot = 270f + mapping.cornerCorrections.topLeftOffset;
                    }
                }
                else if (mapping.type != TileType.Floor)
                {
                    int m = (n ? 1 : 0) + (e ? 2 : 0) + (s ? 4 : 0) + (w ? 8 : 0);
                    if (mapping.type == TileType.Corner) targetRot = m == 3 ? 0 : m == 6 ? 90 : m == 12 ? 180 : 270;
                    else if (n || s) targetRot = 0f; else targetRot = 90f;
                    if (mapping.type == TileType.Corner && flipPathCorners) targetRot += 180f;
                }

                float finalRot = targetRot - (float)mapping.meshFacing + mapping.manualYRotation + globalRotationOffset;
                InstantiatePrefab(spawn, pivot + GetPivotCorrection(finalRot, gridSize), Quaternion.Euler(0, finalRot, 0), levelParent);
            }
        }
        if (showDebugLogs) Debug.Log("Level generation complete!");
    }

    // --- CORRECTION LOGIC ---
    // Calculates the offset needed to keep a Bottom-Left pivot room inside its bounds after rotation
    private Vector3 GetMacroRoomCorrection(float rotation, int wTiles, int hTiles, float size)
    {
        // Standardize rotation
        float r = (rotation % 360 + 360) % 360;

        // 0 deg: No shift needed. Pivot is BL.
        if (r < 1) return Vector3.zero;

        // 90 deg: Pivot swings. We need to shift it +Z (Up) to fit the vertical slot?
        // Wait, standard +90 rotation means +X becomes +Z? 
        // Let's assume standard 2D rotation logic on the X/Z plane.
        // If we rotate 90, we usually need to shift X+

        if (Mathf.Abs(r - 90) < 1f) return new Vector3(0, 0, wTiles * size);
        if (Mathf.Abs(r - 180) < 1f) return new Vector3(wTiles * size, 0, hTiles * size);
        if (Mathf.Abs(r - 270) < 1f) return new Vector3(hTiles * size, 0, 0);

        return Vector3.zero;
    }

    private Color32 GetPixel(int x, int y, Color32[] pixels)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return new Color32(0, 0, 0, 0);
        return pixels[y * width + x];
    }

    private List<Vector2Int> FloodFill(int sx, int sy, Color target, Color32[] pixels)
    {
        List<Vector2Int> res = new List<Vector2Int>();
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        Color32 t32 = target;
        q.Enqueue(new Vector2Int(sx, sy));
        occupiedGrid[sx, sy] = true;
        while (q.Count > 0)
        {
            Vector2Int c = q.Dequeue(); res.Add(c);
            int[] dx = { 1, -1, 0, 0 }; int[] dy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = c.x + dx[i], ny = c.y + dy[i];
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && !occupiedGrid[nx, ny] && !boundaryGrid[nx, ny])
                {
                    Color32 nc = pixels[ny * width + nx];
                    if (nc.r == t32.r && nc.g == t32.g && nc.b == t32.b)
                    {
                        occupiedGrid[nx, ny] = true; q.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }
        return res;
    }

    private bool IsSolid(int x, int y) { if (x < 0 || x >= width || y < 0 || y >= height) return false; return typeGrid[x, y] == TileType.Wall || boundaryGrid[x, y]; }
    private bool IsPath(int x, int y) { if (x < 0 || x >= width || y < 0 || y >= height) return false; return typeGrid[x, y] != TileType.None && typeGrid[x, y] != TileType.Wall; }
    private bool IsColorSimilar(Color32 a, Color32 b, float t) { int tol = (int)(t * 255); return Mathf.Abs(a.r - b.r) < tol && Mathf.Abs(a.g - b.g) < tol && Mathf.Abs(a.b - b.b) < tol; }
    private ColorToPrefabMapping FindMapping(Color32 c) { foreach (var m in mappings) if (IsColorSimilar(m.colorKey, c, boundaryTolerance)) return m; return null; }
    private Vector3 GetPivotCorrection(float r, float s) { r = (r % 360 + 360) % 360; return r < 1 ? Vector3.zero : r < 91 ? new Vector3(0, 0, -s) : r < 181 ? new Vector3(-s, 0, -s) : new Vector3(-s, 0, 0); }
    private void InstantiatePrefab(GameObject p, Vector3 pos, Quaternion rot, Transform par) { if (p) { GameObject o = (GameObject)PrefabUtility.InstantiatePrefab(p, par); o.transform.position = pos; o.transform.rotation = rot; } }

    private void RefreshConfigList() { string[] guids = AssetDatabase.FindAssets("t:GeneratorConfig"); allConfigs = new GeneratorConfig[guids.Length]; configNames = new string[guids.Length]; for (int i = 0; i < guids.Length; i++) { string path = AssetDatabase.GUIDToAssetPath(guids[i]); allConfigs[i] = AssetDatabase.LoadAssetAtPath<GeneratorConfig>(path); configNames[i] = allConfigs[i].name; } }
    private void SaveConfiguration()
    {
        if (string.IsNullOrWhiteSpace(newConfigName)) return;
        GeneratorConfig newConfig = CreateInstance<GeneratorConfig>();
        newConfig.mappings = new List<ColorToPrefabMapping>(mappings);
        string path = $"Assets/GeneratorConfigs/{newConfigName}.asset";
        if (!Directory.Exists("Assets/GeneratorConfigs")) Directory.CreateDirectory("Assets/GeneratorConfigs");
        AssetDatabase.CreateAsset(newConfig, path); AssetDatabase.SaveAssets(); RefreshConfigList();
    }
    private void LoadConfiguration() { if (allConfigs != null && selectedConfigIndex < allConfigs.Length) mappings = new List<ColorToPrefabMapping>(allConfigs[selectedConfigIndex].mappings); }
}