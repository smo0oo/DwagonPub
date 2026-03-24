using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class WagonWorkshopForgeWindow : EditorWindow
{
    // --- Configuration ---
    private const string UPGRADE_PATH = "Assets/GameData/WagonUpgrades/";

    private Vector2 leftScroll;
    private Vector2 rightScroll;

    // Data Caches
    private List<WagonUpgradeData> allUpgrades = new List<WagonUpgradeData>();
    private Dictionary<WagonUpgradeData, Editor> previewEditors = new Dictionary<WagonUpgradeData, Editor>();

    // Current Selection
    private WagonUpgradeType selectedType = WagonUpgradeType.Wheel;

    [MenuItem("Tools/DwagonPub/Wagon Workshop Forge")]
    public static void ShowWindow()
    {
        WagonWorkshopForgeWindow window = GetWindow<WagonWorkshopForgeWindow>("Wagon Workshop");
        window.minSize = new Vector2(1100, 600);
        window.Show();
    }

    private void OnEnable()
    {
        if (!System.IO.Directory.Exists(UPGRADE_PATH))
        {
            System.IO.Directory.CreateDirectory(UPGRADE_PATH);
            AssetDatabase.Refresh();
        }
        LoadAllAssets();
    }

    private void OnDisable()
    {
        ClearPreviewEditors();
    }

    private void LoadAllAssets()
    {
        allUpgrades.Clear();

        string[] guids = AssetDatabase.FindAssets("t:WagonUpgradeData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WagonUpgradeData upgrade = AssetDatabase.LoadAssetAtPath<WagonUpgradeData>(path);
            if (upgrade != null) allUpgrades.Add(upgrade);
        }
    }

    private void ClearPreviewEditors()
    {
        foreach (var editor in previewEditors.Values)
        {
            if (editor != null) DestroyImmediate(editor);
        }
        previewEditors.Clear();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawLeftPane();
        DrawRightPane();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Wagon Economy & Blueprint Balancer", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Blueprints", EditorStyles.toolbarButton)) LoadAllAssets();
        GUILayout.EndHorizontal();
    }

    private void DrawLeftPane()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(200));
        GUILayout.Label("Upgrade Categories", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        foreach (WagonUpgradeType type in System.Enum.GetValues(typeof(WagonUpgradeType)))
        {
            int count = allUpgrades.Count(u => u != null && u.type == type);

            GUI.backgroundColor = selectedType == type ? new Color(1f, 0.8f, 0.4f) : Color.white;
            if (GUILayout.Button($"{type} ({count})", EditorStyles.miniButtonLeft, GUILayout.Height(30)))
            {
                selectedType = type;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button($"Forge New {selectedType}", GUILayout.Height(30))) CreateNewAsset();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        // Filter out nulls first to prevent index 0 errors during the rendering frame!
        List<WagonUpgradeData> categoryUpgrades = allUpgrades
            .Where(u => u != null && u.type == selectedType)
            .OrderBy(u => u.goldCost)
            .ToList();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"{selectedType} Progression Matrix", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (categoryUpgrades.Count > 0)
        {
            DrawEconomySpreadsheet(categoryUpgrades);
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"No {selectedType} upgrades found in the project. Create one to begin balancing.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawEconomySpreadsheet(List<WagonUpgradeData> upgrades)
    {
        float maxGold = Mathf.Max(1, upgrades.Max(u => u.goldCost));

        float GetStatWeight(WagonUpgradeData u) => (u.speedBonus * 10f) + (u.efficiencyBonus * 10f) + u.storageSlotsAdded + u.defenseBonus + (u.comfortBonus * 5f);
        float maxStatWeight = Mathf.Max(1, upgrades.Max(u => GetStatWeight(u)));

        EditorGUILayout.BeginHorizontal("toolbar");
        GUILayout.Label("Preview", GUILayout.Width(110));
        GUILayout.Label("Identity", GUILayout.Width(150));
        GUILayout.Label("Economy", GUILayout.Width(180));
        GUILayout.Label("Core Stats Matrix", GUILayout.ExpandWidth(true));
        GUILayout.Label("Action", GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        foreach (WagonUpgradeData upgrade in upgrades)
        {
            if (upgrade == null) continue; // Ultimate safety check

            SerializedObject so = new SerializedObject(upgrade);
            so.Update();

            EditorGUILayout.BeginHorizontal("box");

            // --- COLUMN 1: Visual Preview ---
            GUILayout.BeginVertical(GUILayout.Width(110));
            if (upgrade.visualPrefab != null)
            {
                if (!previewEditors.ContainsKey(upgrade) || previewEditors[upgrade] == null || previewEditors[upgrade].target != upgrade.visualPrefab)
                {
                    if (previewEditors.ContainsKey(upgrade) && previewEditors[upgrade] != null) DestroyImmediate(previewEditors[upgrade]);
                    previewEditors[upgrade] = Editor.CreateEditor(upgrade.visualPrefab);
                }

                Rect previewRect = GUILayoutUtility.GetRect(100, 100);
                previewEditors[upgrade].OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
            }
            else
            {
                GUILayout.Box("No Prefab", GUILayout.Width(100), GUILayout.Height(100));
            }
            GUILayout.EndVertical();

            // --- COLUMN 2: Identity ---
            GUILayout.BeginVertical(GUILayout.Width(150));
            GUILayout.Space(5);
            so.FindProperty("upgradeName").stringValue = EditorGUILayout.TextField(so.FindProperty("upgradeName").stringValue, EditorStyles.boldLabel);

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            SerializedProperty iconProp = so.FindProperty("icon");
            iconProp.objectReferenceValue = EditorGUILayout.ObjectField(iconProp.objectReferenceValue, typeof(Sprite), false, GUILayout.Width(40), GUILayout.Height(40));

            GUILayout.BeginVertical();
            GUILayout.Label("ID Link:", EditorStyles.miniLabel);
            so.FindProperty("id").stringValue = EditorGUILayout.TextField(so.FindProperty("id").stringValue);
            GUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // --- COLUMN 3: Economy ---
            GUILayout.BeginVertical(GUILayout.Width(180));
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Gold Cost", GUILayout.Width(70));
            so.FindProperty("goldCost").intValue = EditorGUILayout.IntField(so.FindProperty("goldCost").intValue);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            float costRatio = upgrade.goldCost / maxGold;
            float statRatio = GetStatWeight(upgrade) / maxStatWeight;

            GUILayout.Label("Cost Scaling", EditorStyles.miniLabel);
            DrawMiniProgressBar(costRatio, new Color(1f, 0.8f, 0.4f));

            GUILayout.Space(5);
            GUILayout.Label("Power Scaling", EditorStyles.miniLabel);
            DrawMiniProgressBar(statRatio, new Color(0.4f, 0.8f, 1f));

            GUILayout.EndVertical();

            // --- COLUMN 4: Stats Matrix ---
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            DrawStatField(so, "speedBonus", "Speed Mod");
            DrawStatField(so, "efficiencyBonus", "Fuel Efficiency");
            DrawStatField(so, "comfortBonus", "Comfort Mod");
            GUILayout.EndVertical();

            GUILayout.Space(10);

            GUILayout.BeginVertical();
            DrawStatField(so, "storageSlotsAdded", "Bonus Storage");
            DrawStatField(so, "defenseBonus", "Armor Rating");

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("3D Model:", EditorStyles.miniLabel, GUILayout.Width(60));
            SerializedProperty prefabProp = so.FindProperty("visualPrefab");
            prefabProp.objectReferenceValue = EditorGUILayout.ObjectField(prefabProp.objectReferenceValue, typeof(GameObject), false);
            EditorGUILayout.EndHorizontal();
            GUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // --- COLUMN 5: Actions ---
            GUILayout.BeginVertical(GUILayout.Width(60));
            GUILayout.Space(30);
            if (GUILayout.Button("Ping", EditorStyles.miniButton))
            {
                EditorGUIUtility.PingObject(upgrade);
                Selection.activeObject = upgrade;
            }

            bool isDeleted = false; // THE FIX: Track the deletion state safely
            if (GUILayout.Button("Delete", EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Delete Upgrade?", $"Permanently delete {upgrade.name}?", "Yes", "Cancel"))
                {
                    string path = AssetDatabase.GetAssetPath(upgrade);
                    AssetDatabase.DeleteAsset(path);
                    isDeleted = true;
                }
            }
            GUILayout.EndVertical();

            // --- CRITICAL FIX: Close layouts BEFORE deciding what to do with the SerializedObject ---
            EditorGUILayout.EndHorizontal();

            if (isDeleted)
            {
                LoadAllAssets();
                GUIUtility.ExitGUI(); // Force-stops the drawing loop for this frame so Unity doesn't crash!
            }
            else
            {
                so.ApplyModifiedProperties(); // Only apply if we DIDN'T just delete it
            }
        }
    }

    private void DrawStatField(SerializedObject so, string propertyName, string displayName)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(displayName, EditorStyles.miniLabel, GUILayout.Width(90));
        SerializedProperty prop = so.FindProperty(propertyName);

        if (prop != null)
        {
            if (prop.propertyType == SerializedPropertyType.Float)
                prop.floatValue = EditorGUILayout.FloatField(prop.floatValue, GUILayout.Width(50));
            else if (prop.propertyType == SerializedPropertyType.Integer)
                prop.intValue = EditorGUILayout.IntField(prop.intValue, GUILayout.Width(50));
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawMiniProgressBar(float percentage, Color color)
    {
        Rect rect = GUILayoutUtility.GetRect(150, 8, "TextField");
        percentage = Mathf.Clamp01(percentage);

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1f));
            Rect fillRect = new Rect(rect.x, rect.y, rect.width * percentage, rect.height);
            EditorGUI.DrawRect(fillRect, color);
        }
    }

    private void CreateNewAsset()
    {
        string defaultName = $"New_{selectedType}_{System.DateTime.Now.Ticks}";
        string path = UPGRADE_PATH + defaultName + ".asset";

        WagonUpgradeData newUpgrade = ScriptableObject.CreateInstance<WagonUpgradeData>();
        newUpgrade.type = selectedType;
        newUpgrade.upgradeName = $"Tier X {selectedType}";
        newUpgrade.id = $"{selectedType.ToString().ToLower()}_tier_x";

        AssetDatabase.CreateAsset(newUpgrade, path);
        AssetDatabase.SaveAssets();

        LoadAllAssets();
        GUI.FocusControl(null);

        Debug.Log($"[Wagon Forge] Successfully forged {defaultName} at {path}");
    }
}