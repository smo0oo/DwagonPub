using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class LootForgeWindow : EditorWindow
{
    // --- Configuration ---
    private const string LOOT_PATH = "Assets/GameData/LootTables/";

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchQuery = "";

    // Data Caches
    private List<LootTable> allLootTables = new List<LootTable>();

    // Current Selection
    private LootTable selectedTable;
    private Editor cachedEditor;

    [MenuItem("Tools/DwagonPub/Loot Balancer Forge")]
    public static void ShowWindow()
    {
        LootForgeWindow window = GetWindow<LootForgeWindow>("Loot Balancer");
        window.minSize = new Vector2(900, 600);
        window.Show();
    }

    private void OnEnable()
    {
        if (!System.IO.Directory.Exists(LOOT_PATH))
        {
            System.IO.Directory.CreateDirectory(LOOT_PATH);
            AssetDatabase.Refresh();
        }
        LoadAllAssets();
    }

    private void OnDisable()
    {
        if (cachedEditor != null) DestroyImmediate(cachedEditor);
    }

    private void LoadAllAssets()
    {
        allLootTables.Clear();

        string[] guids = AssetDatabase.FindAssets("t:LootTable");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LootTable table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            if (table != null) allLootTables.Add(table);
        }

        allLootTables = allLootTables.OrderBy(t => t.name).ToList();
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
        GUILayout.Label("Vault & Drop Chance Editor", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Vault", EditorStyles.toolbarButton)) LoadAllAssets();
        GUILayout.EndHorizontal();
    }

    private void DrawLeftPane()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(260));

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        foreach (LootTable table in allLootTables)
        {
            if (!string.IsNullOrEmpty(searchQuery) && !table.name.ToLower().Contains(searchQuery.ToLower())) continue;

            GUI.backgroundColor = selectedTable == table ? new Color(1f, 0.8f, 0.4f) : Color.white;
            if (GUILayout.Button(table.name, EditorStyles.miniButtonLeft))
            {
                selectedTable = table;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Forge New Loot Table", GUILayout.Height(30))) CreateNewAsset();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedTable != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Editing: {selectedTable.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reveal in Project", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(selectedTable);
                Selection.activeObject = selectedTable;
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // --- AAA FEATURE: INDEPENDENT DROP CHANCE EDITOR ---
            DrawIndependentBalancer();
            // ---------------------------------------------------

            EditorGUILayout.Space();

            if (cachedEditor == null || cachedEditor.target != selectedTable)
            {
                if (cachedEditor != null) DestroyImmediate(cachedEditor);
                cachedEditor = Editor.CreateEditor(selectedTable);
            }

            cachedEditor.OnInspectorGUI();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a Loot Table from the vault.", EditorStyles.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    // --- DEEP SCANNERS ---
    private SerializedProperty GetDropsList(SerializedObject so)
    {
        string[] commonNames = { "drops", "lootDrops", "items", "loot", "dropList", "possibleDrops", "lootTable", "entries" };
        foreach (string name in commonNames)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null && prop.isArray && prop.propertyType == SerializedPropertyType.Generic) return prop;
        }

        SerializedProperty iterator = so.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.isArray && iterator.propertyType == SerializedPropertyType.Generic && iterator.name != "m_Script")
            {
                return so.FindProperty(iterator.name);
            }
        }
        return null;
    }

    private SerializedProperty GetChanceProperty(SerializedProperty element)
    {
        string[] names = { "chance", "probability", "dropChance", "rate", "dropRate", "weight", "dropWeight" };
        foreach (string n in names) { var p = element.FindPropertyRelative(n); if (p != null) return p; }

        SerializedProperty iterator = element.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        iterator.NextVisible(true);
        while (!SerializedProperty.EqualContents(iterator, endProperty))
        {
            if (iterator.propertyType == SerializedPropertyType.Float || iterator.propertyType == SerializedPropertyType.Integer) return iterator;
            iterator.NextVisible(false);
        }
        return null;
    }

    private SerializedProperty GetItemProperty(SerializedProperty element)
    {
        string[] names = { "item", "itemData", "lootData", "reward", "drop", "prefab" };
        foreach (string n in names) { var p = element.FindPropertyRelative(n); if (p != null) return p; }

        SerializedProperty iterator = element.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        iterator.NextVisible(true);
        while (!SerializedProperty.EqualContents(iterator, endProperty))
        {
            if (iterator.propertyType == SerializedPropertyType.ObjectReference) return iterator;
            iterator.NextVisible(false);
        }
        return null;
    }
    // ----------------------

    private void DrawIndependentBalancer()
    {
        SerializedObject so = new SerializedObject(selectedTable);
        so.Update();

        SerializedProperty dropsList = GetDropsList(so);

        if (dropsList == null || !dropsList.isArray)
        {
            EditorGUILayout.HelpBox("Could not locate a valid list of drops in this Loot Table.", MessageType.Warning);
            return;
        }

        if (dropsList.arraySize == 0)
        {
            GUILayout.Label("No items in this Loot Table yet.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Independent Drop Chances (0.0 to 1.0)", EditorStyles.boldLabel);
        GUILayout.Space(5);

        for (int i = 0; i < dropsList.arraySize; i++)
        {
            SerializedProperty dropElement = dropsList.GetArrayElementAtIndex(i);

            SerializedProperty chanceProp = GetChanceProperty(dropElement);
            SerializedProperty itemProp = GetItemProperty(dropElement);

            if (chanceProp != null)
            {
                float currentChance = chanceProp.propertyType == SerializedPropertyType.Integer ? (chanceProp.intValue / 100f) : chanceProp.floatValue;
                currentChance = Mathf.Clamp(currentChance, 0f, 1f);

                string itemName = "Empty Slot";
                Texture2D iconTex = null;

                if (itemProp != null && itemProp.objectReferenceValue != null)
                {
                    itemName = itemProp.objectReferenceValue.name;

                    SerializedObject itemSO = new SerializedObject(itemProp.objectReferenceValue);
                    SerializedProperty iconProp = itemSO.FindProperty("icon") ?? itemSO.FindProperty("itemIcon");

                    if (iconProp != null && iconProp.objectReferenceValue is Sprite sprite)
                    {
                        iconTex = AssetPreview.GetAssetPreview(sprite);
                        if (iconTex == null) iconTex = sprite.texture;
                    }
                }

                // --- AAA UPDATE: 100x100 Card Layout ---
                EditorGUILayout.BeginHorizontal("box");

                // LEFT: Draw 100x100 Icon
                if (iconTex != null)
                {
                    GUILayout.Label(iconTex, GUILayout.Width(100), GUILayout.Height(100));
                }
                else
                {
                    GUILayout.Box("No\nIcon", GUILayout.Width(100), GUILayout.Height(100));
                }

                // RIGHT: Controls Stack
                GUILayout.BeginVertical();
                GUILayout.Space(10); // Push content down to center it next to the big image

                // Top row: Item Name and Percentage readout
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(itemName, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Label($"{(currentChance * 100f):F1}%", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight }, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                // Middle row: Slider
                EditorGUI.BeginChangeCheck();
                float newChance = EditorGUILayout.Slider(currentChance, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    if (chanceProp.propertyType == SerializedPropertyType.Integer)
                        chanceProp.intValue = Mathf.RoundToInt(newChance * 100f);
                    else
                        chanceProp.floatValue = newChance;

                    currentChance = newChance;
                }

                GUILayout.Space(5);

                // Bottom row: Probability Matrix Bar
                Rect rect = GUILayoutUtility.GetRect(18, 18, "TextField");
                Color barColor = GetChanceColor(currentChance);
                EditorGUI.ProgressBar(rect, currentChance, "");

                if (Event.current.type == EventType.Repaint)
                {
                    Rect colorRect = new Rect(rect.x, rect.y, rect.width * currentChance, rect.height);
                    EditorGUI.DrawRect(colorRect, barColor);
                }

                GUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                // ---------------------------------------
            }
        }

        so.ApplyModifiedProperties();
        EditorGUILayout.EndVertical();
    }

    private Color GetChanceColor(float percentage)
    {
        if (percentage >= 0.50f) return new Color(0.4f, 0.8f, 0.4f, 0.8f);
        if (percentage >= 0.20f) return new Color(0.4f, 0.6f, 1.0f, 0.8f);
        if (percentage >= 0.05f) return new Color(0.8f, 0.4f, 1.0f, 0.8f);
        return new Color(1.0f, 0.6f, 0.2f, 0.8f);
    }

    private void CreateNewAsset()
    {
        string defaultName = $"New_LootTable_{System.DateTime.Now.Ticks}";
        string path = LOOT_PATH + defaultName + ".asset";

        LootTable newTable = ScriptableObject.CreateInstance<LootTable>();
        AssetDatabase.CreateAsset(newTable, path);
        AssetDatabase.SaveAssets();

        LoadAllAssets();
        selectedTable = newTable;
        GUI.FocusControl(null);

        Debug.Log($"[Drop Forge] Successfully forged {defaultName} at {path}");
    }
}