using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ItemForgeWindow : EditorWindow
{
    // --- Configuration: Define your strict folder paths here! ---
    private const string BASE_PATH = "Assets/GameData/Items/";
    private const string WEAPON_PATH = BASE_PATH + "Weapons/";
    private const string ARMOR_PATH = BASE_PATH + "Armor/";
    private const string CONSUMABLE_PATH = BASE_PATH + "Consumables/";
    private const string TRINKET_PATH = BASE_PATH + "Trinkets/";
    private const string AFFIX_PATH = BASE_PATH + "Affixes/";

    private enum Tab { Weapons, Armor, Consumables, Trinkets, Affixes }
    private Tab currentTab = Tab.Weapons;

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchQuery = "";

    // Data Caches
    private List<ItemData> allItems = new List<ItemData>();
    private List<ItemAffix> allAffixes = new List<ItemAffix>();

    // Current Selection
    private ItemData selectedItem;
    private ItemAffix selectedAffix;
    private Editor cachedEditor;

    // Preview Caches
    private Editor prefabPreviewEditor;
    private GameObject currentPreviewPrefab;

    [MenuItem("Tools/DwagonPub/Item Forge")]
    public static void ShowWindow()
    {
        ItemForgeWindow window = GetWindow<ItemForgeWindow>("Item Forge");
        window.minSize = new Vector2(900, 600);
        window.Show();
    }

    private void OnEnable()
    {
        EnsureDirectoriesExist();
        LoadAllAssets();
    }

    private void OnDisable()
    {
        if (cachedEditor != null) DestroyImmediate(cachedEditor);
        if (prefabPreviewEditor != null) DestroyImmediate(prefabPreviewEditor);
    }

    private void EnsureDirectoriesExist()
    {
        string[] paths = { WEAPON_PATH, ARMOR_PATH, CONSUMABLE_PATH, TRINKET_PATH, AFFIX_PATH };
        foreach (string path in paths)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[Item Forge] Created missing directory: {path}");
            }
        }
        AssetDatabase.Refresh();
    }

    private void LoadAllAssets()
    {
        allItems.Clear();
        allAffixes.Clear();

        string[] itemGuids = AssetDatabase.FindAssets("t:ItemData");
        foreach (string guid in itemGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null) allItems.Add(item);
        }

        string[] affixGuids = AssetDatabase.FindAssets("t:ItemAffix");
        foreach (string guid in affixGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemAffix affix = AssetDatabase.LoadAssetAtPath<ItemAffix>(path);
            if (affix != null) allAffixes.Add(affix);
        }

        allItems = allItems.OrderBy(i => i.name).ToList();
        allAffixes = allAffixes.OrderBy(a => a.name).ToList();
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

        if (GUILayout.Toggle(currentTab == Tab.Weapons, "Weapons", EditorStyles.toolbarButton)) currentTab = Tab.Weapons;
        if (GUILayout.Toggle(currentTab == Tab.Armor, "Armor", EditorStyles.toolbarButton)) currentTab = Tab.Armor;
        if (GUILayout.Toggle(currentTab == Tab.Consumables, "Consumables", EditorStyles.toolbarButton)) currentTab = Tab.Consumables;
        if (GUILayout.Toggle(currentTab == Tab.Trinkets, "Trinkets", EditorStyles.toolbarButton)) currentTab = Tab.Trinkets;
        if (GUILayout.Toggle(currentTab == Tab.Affixes, "Affixes", EditorStyles.toolbarButton)) currentTab = Tab.Affixes;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Library", EditorStyles.toolbarButton)) LoadAllAssets();

        GUILayout.EndHorizontal();
    }

    private void DrawLeftPane()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(260));

        // Search Bar
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        // The Asset List
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        if (currentTab == Tab.Affixes)
        {
            foreach (ItemAffix affix in allAffixes)
            {
                if (!string.IsNullOrEmpty(searchQuery) && !affix.name.ToLower().Contains(searchQuery.ToLower())) continue;

                GUI.backgroundColor = selectedAffix == affix ? Color.cyan : Color.white;
                if (GUILayout.Button(affix.name, EditorStyles.miniButtonLeft)) SelectAsset(null, affix);
                GUI.backgroundColor = Color.white;
            }
        }
        else
        {
            foreach (ItemData item in allItems)
            {
                bool matchesTab = false;
                if (item.stats != null)
                {
                    if (currentTab == Tab.Weapons && item.stats is ItemWeaponStats) matchesTab = true;
                    else if (currentTab == Tab.Armor && item.stats is ItemArmourStats) matchesTab = true;
                    else if (currentTab == Tab.Consumables && item.stats is ItemConsumableStats) matchesTab = true;
                    else if (currentTab == Tab.Trinkets && item.stats is ItemTrinketStats) matchesTab = true;
                }
                else if (currentTab == Tab.Weapons) matchesTab = true;

                if (!matchesTab) continue;
                if (!string.IsNullOrEmpty(searchQuery) && !item.name.ToLower().Contains(searchQuery.ToLower())) continue;

                GUI.backgroundColor = selectedItem == item ? Color.cyan : Color.white;
                if (GUILayout.Button(item.name, EditorStyles.miniButtonLeft)) SelectAsset(item, null);
                GUI.backgroundColor = Color.white;
            }
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button($"Create New {currentTab}", GUILayout.Height(30))) CreateNewAsset();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        UnityEngine.Object targetObj = currentTab == Tab.Affixes ? (UnityEngine.Object)selectedAffix : selectedItem;

        if (targetObj != null)
        {
            // Title & Reveal Button
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Editing: {targetObj.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reveal in Project", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(targetObj);
                Selection.activeObject = targetObj;
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // --- AAA FEATURE: Visual Previews ---
            if (currentTab != Tab.Affixes && selectedItem != null)
            {
                DrawVisualPreviews();
            }
            // ------------------------------------

            EditorGUILayout.Space();

            // Standard Inspector
            if (cachedEditor == null || cachedEditor.target != targetObj)
            {
                if (cachedEditor != null) DestroyImmediate(cachedEditor);
                cachedEditor = Editor.CreateEditor(targetObj);
            }

            cachedEditor.OnInspectorGUI();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select an item from the library or create a new one.", EditorStyles.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawVisualPreviews()
    {
        EditorGUILayout.BeginHorizontal("helpBox");

        // 1. Extract exactly from your ItemData.cs structure!
        SerializedObject so = new SerializedObject(selectedItem);
        SerializedProperty iconProp = so.FindProperty("icon");
        SerializedProperty prefabProp = so.FindProperty("equippedPrefab");

        // 2. Draw 2D Icon
        GUILayout.BeginVertical(GUILayout.Width(120));
        GUILayout.Label("2D Icon", EditorStyles.centeredGreyMiniLabel);

        if (iconProp != null && iconProp.objectReferenceValue != null)
        {
            Sprite iconSprite = iconProp.objectReferenceValue as Sprite;
            if (iconSprite != null)
            {
                Rect rect = GUILayoutUtility.GetRect(100, 100);
                GUI.DrawTexture(rect, iconSprite.texture, ScaleMode.ScaleToFit);
            }
        }
        else
        {
            GUILayout.Box("No Icon", GUILayout.Width(100), GUILayout.Height(100));
        }
        GUILayout.EndVertical();

        // 3. Draw 3D Interactive Prefab Preview
        GUILayout.BeginVertical(GUILayout.Width(220));
        GUILayout.Label("3D View (Drag to Rotate)", EditorStyles.centeredGreyMiniLabel);

        if (prefabProp != null && prefabProp.objectReferenceValue != null)
        {
            GameObject targetPrefab = prefabProp.objectReferenceValue as GameObject;

            // Only rebuild the preview editor if the prefab actually changed
            if (prefabPreviewEditor == null || currentPreviewPrefab != targetPrefab)
            {
                if (prefabPreviewEditor != null) DestroyImmediate(prefabPreviewEditor);
                prefabPreviewEditor = Editor.CreateEditor(targetPrefab);
                currentPreviewPrefab = targetPrefab;
            }

            if (prefabPreviewEditor != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(200, 200);
                prefabPreviewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
            }
        }
        else
        {
            GUILayout.Box("No Prefab Assigned", GUILayout.Width(200), GUILayout.Height(200));
        }
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void SelectAsset(ItemData item, ItemAffix affix)
    {
        selectedItem = item;
        selectedAffix = affix;
        GUI.FocusControl(null);
    }

    private void CreateNewAsset()
    {
        string defaultName = $"New_{currentTab}_{System.DateTime.Now.Ticks}";
        string path = "";

        if (currentTab == Tab.Affixes)
        {
            path = AFFIX_PATH + defaultName + ".asset";
            ItemAffix newAffix = ScriptableObject.CreateInstance<ItemAffix>();
            AssetDatabase.CreateAsset(newAffix, path);
            LoadAllAssets();
            SelectAsset(null, newAffix);
        }
        else
        {
            ItemData newItem = ScriptableObject.CreateInstance<ItemData>();
            newItem.itemName = "New Item";

            switch (currentTab)
            {
                case Tab.Weapons: path = WEAPON_PATH + defaultName + ".asset"; newItem.stats = new ItemWeaponStats(); break;
                case Tab.Armor: path = ARMOR_PATH + defaultName + ".asset"; newItem.stats = new ItemArmourStats(); break;
                case Tab.Consumables: path = CONSUMABLE_PATH + defaultName + ".asset"; newItem.stats = new ItemConsumableStats(); break;
                case Tab.Trinkets: path = TRINKET_PATH + defaultName + ".asset"; newItem.stats = new ItemTrinketStats(); break;
            }

            AssetDatabase.CreateAsset(newItem, path);
            LoadAllAssets();
            SelectAsset(newItem, null);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Item Forge] Successfully forged {defaultName} at {path}");
    }
}