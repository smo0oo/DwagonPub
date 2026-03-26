using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using Object = UnityEngine.Object;

public class ItemForgeWindow : EditorWindow
{
    // --- Configuration ---
    private const string ITEM_PATH = "Assets/GameData/Items/";

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchQuery = "";
    private ItemType selectedFilter = ItemType.Weapon;

    // Data Caches
    private List<ItemData> allItems = new List<ItemData>();
    private ItemData selectedItem;

    // Cached Editors for nested stats and 3D previews
    private Dictionary<Object, Editor> cachedEditors = new Dictionary<Object, Editor>();
    private Dictionary<Object, Editor> prefabEditors = new Dictionary<Object, Editor>();

    [MenuItem("Tools/DwagonPub/Item Forge")]
    public static void ShowWindow()
    {
        ItemForgeWindow window = GetWindow<ItemForgeWindow>("Item Architect");
        window.minSize = new Vector2(1000, 600);
        window.Show();
    }

    private void OnEnable()
    {
        if (!System.IO.Directory.Exists(ITEM_PATH))
        {
            System.IO.Directory.CreateDirectory(ITEM_PATH);
            AssetDatabase.Refresh();
        }
        LoadAllAssets();
    }

    private void OnDisable()
    {
        ClearCachedEditors();
    }

    private void ClearCachedEditors()
    {
        foreach (var editor in cachedEditors.Values)
        {
            if (editor != null) DestroyImmediate(editor);
        }
        cachedEditors.Clear();

        foreach (var editor in prefabEditors.Values)
        {
            if (editor != null) DestroyImmediate(editor);
        }
        prefabEditors.Clear();
    }

    private void LoadAllAssets()
    {
        allItems.Clear();
        string[] guids = AssetDatabase.FindAssets("t:ItemData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null) allItems.Add(item);
        }
        allItems = allItems.OrderBy(i => i.itemType).ThenBy(i => i.itemName).ToList();
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
        GUILayout.Label("Item & Equipment Architect", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Library", EditorStyles.toolbarButton)) LoadAllAssets();
        GUILayout.EndHorizontal();
    }

    private void DrawLeftPane()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(250));

        // Search Bar
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(5);

        // --- NEW: Category Button Grid ---
        GUILayout.Label("Categories", EditorStyles.boldLabel);
        Array itemTypes = Enum.GetValues(typeof(ItemType));
        int columns = 2;

        for (int i = 0; i < itemTypes.Length; i += columns)
        {
            GUILayout.BeginHorizontal();
            for (int j = 0; j < columns; j++)
            {
                if (i + j < itemTypes.Length)
                {
                    ItemType t = (ItemType)itemTypes.GetValue(i + j);
                    GUI.backgroundColor = selectedFilter == t ? new Color(0.6f, 0.9f, 0.6f) : Color.white;
                    if (GUILayout.Button(t.ToString(), EditorStyles.miniButton, GUILayout.Height(22)))
                    {
                        selectedFilter = t;
                        GUI.FocusControl(null);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(5);

        // List View
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        foreach (ItemData item in allItems)
        {
            if (item == null) continue;
            if (item.itemType != selectedFilter) continue;
            if (!string.IsNullOrEmpty(searchQuery) && !item.itemName.ToLower().Contains(searchQuery.ToLower())) continue;

            // Highlight color based on rarity
            Color btnColor = Color.white;
            if (item.stats != null)
            {
                btnColor = GetRarityColor(item.stats.rarity);
            }

            GUI.backgroundColor = selectedItem == item ? Color.Lerp(btnColor, Color.white, 0.5f) : btnColor;

            if (GUILayout.Button(item.itemName, EditorStyles.miniButtonLeft, GUILayout.Height(25)))
            {
                selectedItem = item;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button($"Forge New {selectedFilter}", GUILayout.Height(30))) CreateNewAsset();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private Color GetRarityColor(ItemStats.Rarity rarity)
    {
        switch (rarity)
        {
            case ItemStats.Rarity.Common: return new Color(0.9f, 0.9f, 0.9f);
            case ItemStats.Rarity.Uncommon: return new Color(0.5f, 0.9f, 0.5f);
            case ItemStats.Rarity.Rare: return new Color(0.4f, 0.6f, 1f);
            case ItemStats.Rarity.Epic: return new Color(0.8f, 0.4f, 1f);
            case ItemStats.Rarity.Legendary: return new Color(1f, 0.6f, 0f);
            default: return Color.white;
        }
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedItem != null)
        {
            SerializedObject so = new SerializedObject(selectedItem);
            so.Update();

            // --- HEADER ---
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Forging: {selectedItem.itemName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ping in Project", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(selectedItem);
                Selection.activeObject = selectedItem;
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Delete Item?", $"Permanently delete {selectedItem.itemName}?", "Yes", "Cancel"))
                {
                    string path = AssetDatabase.GetAssetPath(selectedItem);
                    AssetDatabase.DeleteAsset(path);
                    selectedItem = null;
                    LoadAllAssets();
                    GUIUtility.ExitGUI();
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            DrawIdentitySection(so);
            EditorGUILayout.Space();

            DrawPropertiesSection(so);
            EditorGUILayout.Space();

            DrawNestedStatsEditor(so);

            so.ApplyModifiedProperties();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select an Item from the library to edit.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawIdentitySection(SerializedObject so)
    {
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Item Identity & Lore", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        // 2D Visual Icon
        GUILayout.BeginVertical(GUILayout.Width(100));
        GUILayout.Label("2D Icon", EditorStyles.miniLabel);
        SerializedProperty iconProp = so.FindProperty("icon");
        iconProp.objectReferenceValue = EditorGUILayout.ObjectField(iconProp.objectReferenceValue, typeof(Sprite), false, GUILayout.Width(100), GUILayout.Height(100));
        GUILayout.EndVertical();

        GUILayout.Space(10);

        // --- NEW: 3D Visual Prefab ---
        GUILayout.BeginVertical(GUILayout.Width(100));
        GUILayout.Label("3D Model", EditorStyles.miniLabel);
        SerializedProperty prefabProp = so.FindProperty("equippedPrefab");

        if (prefabProp.objectReferenceValue != null)
        {
            Object prefab = prefabProp.objectReferenceValue;
            if (!prefabEditors.ContainsKey(prefab) || prefabEditors[prefab] == null)
            {
                prefabEditors[prefab] = Editor.CreateEditor(prefab);
            }
            Rect previewRect = GUILayoutUtility.GetRect(100, 100);
            prefabEditors[prefab].OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
        }
        else
        {
            GUILayout.Box("No Prefab", EditorStyles.helpBox, GUILayout.Width(100), GUILayout.Height(100));
        }

        // Field to drop the prefab into
        prefabProp.objectReferenceValue = EditorGUILayout.ObjectField(prefabProp.objectReferenceValue, typeof(GameObject), false, GUILayout.Width(100));
        GUILayout.EndVertical();

        GUILayout.Space(10);

        // Core Strings
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        GUIStyle titleStyle = new GUIStyle(EditorStyles.textField) { fontStyle = FontStyle.Bold };

        SerializedProperty nameProp = so.FindProperty("itemName");
        GUILayout.Label("Item Name (Internal)", EditorStyles.miniLabel);
        nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue, titleStyle);

        SerializedProperty displayProp = so.FindProperty("displayName");
        GUILayout.Label("Display Name (UI)", EditorStyles.miniLabel);
        displayProp.stringValue = EditorGUILayout.TextField(displayProp.stringValue);

        SerializedProperty idProp = so.FindProperty("id");
        GUILayout.Label("Database ID", EditorStyles.miniLabel);
        idProp.stringValue = EditorGUILayout.TextField(idProp.stringValue);

        GUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        SerializedProperty descProp = so.FindProperty("description");
        GUILayout.Label("Item Description", EditorStyles.miniLabel);
        GUIStyle descStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        descProp.stringValue = EditorGUILayout.TextArea(descProp.stringValue, descStyle, GUILayout.Height(60));

        EditorGUILayout.EndVertical();
    }

    private void DrawPropertiesSection(SerializedObject so)
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Core Mechanics", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        // Left Column
        GUILayout.BeginVertical();
        EditorGUILayout.PropertyField(so.FindProperty("itemType"));
        EditorGUILayout.PropertyField(so.FindProperty("itemValue"), new GUIContent("Gold Value"));
        EditorGUILayout.PropertyField(so.FindProperty("isUnique"));
        GUILayout.EndVertical();

        // Right Column
        GUILayout.BeginVertical();
        EditorGUILayout.PropertyField(so.FindProperty("isStackable"));
        if (so.FindProperty("isStackable").boolValue)
        {
            EditorGUILayout.PropertyField(so.FindProperty("maxStackSize"));
        }
        EditorGUILayout.PropertyField(so.FindProperty("levelRequirement"));
        GUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        EditorGUILayout.PropertyField(so.FindProperty("allowedClasses"), true);

        EditorGUILayout.EndVertical();
    }

    private void DrawNestedStatsEditor(SerializedObject so)
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Item Stats & Attributes", EditorStyles.boldLabel);
        GUILayout.Space(5);

        SerializedProperty statsProp = so.FindProperty("stats");
        EditorGUILayout.PropertyField(statsProp, new GUIContent("Stats Payload"));

        if (statsProp.objectReferenceValue != null)
        {
            GUILayout.Space(10);

            Object targetObj = statsProp.objectReferenceValue;

            if (!cachedEditors.ContainsKey(targetObj) || cachedEditors[targetObj] == null)
            {
                cachedEditors[targetObj] = Editor.CreateEditor(targetObj);
            }

            GUIStyle nestedBox = new GUIStyle("helpBox");
            EditorGUILayout.BeginVertical(nestedBox);

            GUILayout.Label($"Editing: {targetObj.GetType().Name}", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            cachedEditors[targetObj].OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(targetObj);
            }

            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("No ItemStats assigned. Assign one or create a new one below.", MessageType.Info);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Weapon Stats", EditorStyles.miniButton)) CreateNestedStat<ItemWeaponStats>(statsProp);
            if (GUILayout.Button("Create Armour Stats", EditorStyles.miniButton)) CreateNestedStat<ItemArmourStats>(statsProp);
            GUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void CreateNestedStat<T>(SerializedProperty statsProp) where T : ItemStats
    {
        string path = EditorUtility.SaveFilePanelInProject($"Create new {typeof(T).Name}", $"New_{typeof(T).Name}", "asset", "Save new stat block");
        if (string.IsNullOrEmpty(path)) return;

        T newStats = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(newStats, path);
        AssetDatabase.SaveAssets();

        statsProp.objectReferenceValue = newStats;
        statsProp.serializedObject.ApplyModifiedProperties();
    }

    private void CreateNewAsset()
    {
        string defaultName = $"New{selectedFilter}_{System.DateTime.Now.Ticks}";
        string path = ITEM_PATH + defaultName + ".asset";

        ItemData newAsset = ScriptableObject.CreateInstance<ItemData>();
        newAsset.itemType = selectedFilter;

        AssetDatabase.CreateAsset(newAsset, path);
        AssetDatabase.SaveAssets();

        LoadAllAssets();
        selectedItem = newAsset;
        GUI.FocusControl(null);

        Debug.Log($"[Item Architect] Forged {defaultName} at {path}");
    }
}