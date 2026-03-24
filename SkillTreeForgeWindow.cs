using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class SkillTreeForgeWindow : EditorWindow
{
    // --- Configuration ---
    private const string TREE_PATH = "Assets/GameData/Progression/Trees/";

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private Vector2 nodeScroll;
    private string searchQuery = "";

    // Data Caches
    private List<ScriptableObject> allTrees = new List<ScriptableObject>();

    // Current Selection
    private ScriptableObject selectedTree;
    private int selectedNodeIndex = -1; // We now track the INDEX of the node inside the tree!
    private Editor cachedEditor;

    private enum EditorMode { TreeSettings, NodeArchitect }
    private EditorMode currentMode = EditorMode.NodeArchitect;

    [MenuItem("Tools/DwagonPub/Progression Forge")]
    public static void ShowWindow()
    {
        SkillTreeForgeWindow window = GetWindow<SkillTreeForgeWindow>("Progression Forge");
        window.minSize = new Vector2(950, 600);
        window.Show();
    }

    private void OnEnable()
    {
        if (!System.IO.Directory.Exists(TREE_PATH))
        {
            System.IO.Directory.CreateDirectory(TREE_PATH);
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
        allTrees.Clear();

        string[] treeGuids = AssetDatabase.FindAssets("t:SkillTree");
        foreach (string guid in treeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject tree = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (tree != null) allTrees.Add(tree);
        }

        allTrees = allTrees.OrderBy(t => t.name).ToList();
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
        GUILayout.Label("Skill Tree Architect", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Trees", EditorStyles.toolbarButton)) LoadAllAssets();
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

        foreach (ScriptableObject tree in allTrees)
        {
            if (!string.IsNullOrEmpty(searchQuery) && !tree.name.ToLower().Contains(searchQuery.ToLower())) continue;

            GUI.backgroundColor = selectedTree == tree ? new Color(0.4f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button(tree.name, EditorStyles.miniButtonLeft))
            {
                selectedTree = tree;
                selectedNodeIndex = -1; // Reset node selection when changing trees
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Forge New Skill Tree", GUILayout.Height(30))) CreateNewTree();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedTree != null)
        {
            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Architecting: {selectedTree.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reveal in Project", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(selectedTree);
                Selection.activeObject = selectedTree;
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Mode Toggles
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = currentMode == EditorMode.TreeSettings ? new Color(0.7f, 0.7f, 0.7f) : Color.white;
            if (GUILayout.Button("Tree Settings", EditorStyles.miniButtonLeft, GUILayout.Height(25))) currentMode = EditorMode.TreeSettings;

            GUI.backgroundColor = currentMode == EditorMode.NodeArchitect ? new Color(1f, 0.8f, 0.4f) : Color.white;
            if (GUILayout.Button("Node Architect", EditorStyles.miniButtonRight, GUILayout.Height(25))) currentMode = EditorMode.NodeArchitect;
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (currentMode == EditorMode.TreeSettings)
            {
                if (cachedEditor == null || cachedEditor.target != selectedTree)
                {
                    if (cachedEditor != null) DestroyImmediate(cachedEditor);
                    cachedEditor = Editor.CreateEditor(selectedTree);
                }
                cachedEditor.OnInspectorGUI();
            }
            else
            {
                DrawNodeArchitect();
            }
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a Tree to begin architecting.", EditorStyles.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    // --- DEEP SCANNER FOR INTERNAL NODE ARRAY ---
    private SerializedProperty GetNodesArray(SerializedObject so)
    {
        string[] potentialNames = { "nodes", "skillNodes", "treeNodes", "skills", "nodeList", "skillsList" };
        foreach (string name in potentialNames)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null && prop.isArray && prop.propertyType == SerializedPropertyType.Generic) return prop;
        }

        // Fallback: First array
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

    // --- AAA FEATURE: THE INTERNAL NODE ARCHITECT ---
    private void DrawNodeArchitect()
    {
        SerializedObject so = new SerializedObject(selectedTree);
        so.Update();

        SerializedProperty nodesArray = GetNodesArray(so);

        if (nodesArray == null)
        {
            EditorGUILayout.HelpBox("Could not locate a valid list of nodes inside this Skill Tree.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();

        // LEFT COLUMN: List of Nodes inside this Tree
        GUILayout.BeginVertical("helpBox", GUILayout.Width(250));
        GUILayout.Label("Tree Nodes", EditorStyles.boldLabel);

        nodeScroll = EditorGUILayout.BeginScrollView(nodeScroll);

        for (int i = 0; i < nodesArray.arraySize; i++)
        {
            SerializedProperty nodeProp = nodesArray.GetArrayElementAtIndex(i);

            // Try to find a display name for the button
            string nodeName = $"Node {i}";
            SerializedProperty nameProp = nodeProp.FindPropertyRelative("nodeName") ?? nodeProp.FindPropertyRelative("id") ?? nodeProp.FindPropertyRelative("skillName");
            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue)) nodeName = nameProp.stringValue;

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = selectedNodeIndex == i ? new Color(1f, 0.8f, 0.4f) : Color.white;
            if (GUILayout.Button(nodeName, EditorStyles.miniButtonLeft, GUILayout.Height(25)))
            {
                selectedNodeIndex = i;
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25), GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Delete Node?", "Are you sure you want to permanently delete this node from the tree?", "Yes", "Cancel"))
                {
                    nodesArray.DeleteArrayElementAtIndex(i);
                    if (selectedNodeIndex == i) selectedNodeIndex = -1;
                    so.ApplyModifiedProperties();
                    GUIUtility.ExitGUI(); // Prevent drawing errors after deletion
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("+ Add New Node", GUILayout.Height(30)))
        {
            nodesArray.arraySize++;
            selectedNodeIndex = nodesArray.arraySize - 1;
            so.ApplyModifiedProperties();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();

        // RIGHT COLUMN: The Inspector for the Selected Node
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));

        if (selectedNodeIndex >= 0 && selectedNodeIndex < nodesArray.arraySize)
        {
            SerializedProperty targetNodeProp = nodesArray.GetArrayElementAtIndex(selectedNodeIndex);

            GUILayout.Label("Node Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Draws the properties of the class just like the standard Unity Inspector!
            SerializedProperty iterator = targetNodeProp.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            iterator.NextVisible(true);
            while (!SerializedProperty.EqualContents(iterator, endProperty))
            {
                EditorGUILayout.PropertyField(iterator, true);
                iterator.NextVisible(false);
            }
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a node from the list on the left.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        so.ApplyModifiedProperties();
    }

    private void CreateNewTree()
    {
        string defaultName = $"New_SkillTree_{System.DateTime.Now.Ticks}";
        string path = TREE_PATH + defaultName + ".asset";

        ScriptableObject newTree = ScriptableObject.CreateInstance("SkillTree");

        if (newTree != null)
        {
            AssetDatabase.CreateAsset(newTree, path);
            AssetDatabase.SaveAssets();
            LoadAllAssets();
            selectedTree = newTree;
            selectedNodeIndex = -1;
            GUI.FocusControl(null);

            Debug.Log($"[Progression Forge] Successfully forged {defaultName} at {path}");
        }
        else
        {
            Debug.LogError($"[Progression Forge] Failed to create asset. Ensure you have a script named exactly 'SkillTree.cs'.");
        }
    }
}