using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Splines;

public class WorldMapForgeWindow : EditorWindow
{
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchQuery = "";

    // Data Caches (Scene Objects!)
    private List<LocationNode> allNodes = new List<LocationNode>();
    private LocationNode selectedNode;
    private Editor cachedEditor;

    // Economy Baseline
    private float fuelPerHour = 5f;
    private float rationsPerHour = 2f;

    [MenuItem("Tools/DwagonPub/World Map Forge")]
    public static void ShowWindow()
    {
        WorldMapForgeWindow window = GetWindow<WorldMapForgeWindow>("Expedition Architect");
        window.minSize = new Vector2(1000, 600);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshSceneNodes();
        FetchEconomyBaselines();
    }

    private void OnDisable()
    {
        if (cachedEditor != null) DestroyImmediate(cachedEditor);
    }

    private void RefreshSceneNodes()
    {
        allNodes.Clear();
        LocationNode[] nodesInScene = FindObjectsByType<LocationNode>(FindObjectsSortMode.None);
        allNodes.AddRange(nodesInScene);
        allNodes = allNodes.OrderBy(n => n.gameObject.name).ToList();
    }

    private void FetchEconomyBaselines()
    {
        WagonResourceManager manager = FindAnyObjectByType<WagonResourceManager>();
        if (manager != null)
        {
            fuelPerHour = manager.fuelPerHour;
            rationsPerHour = manager.rationsPerHour;
        }
        else
        {
            fuelPerHour = 5f;
            rationsPerHour = 2f;
        }
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
        GUILayout.Label("World Map & Expedition Architect", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Sync Scene Nodes", EditorStyles.toolbarButton))
        {
            RefreshSceneNodes();
            FetchEconomyBaselines();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawLeftPane()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(250));

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        foreach (LocationNode node in allNodes)
        {
            if (node == null) continue;
            string displayName = string.IsNullOrEmpty(node.locationName) ? node.gameObject.name : node.locationName;
            if (!string.IsNullOrEmpty(searchQuery) && !displayName.ToLower().Contains(searchQuery.ToLower())) continue;

            Color btnColor = Color.white;
            if (node.nodeType == NodeType.Event) btnColor = new Color(1f, 0.8f, 0.4f);
            else if (node.nodeType == NodeType.DualModeLocation) btnColor = new Color(0.8f, 0.4f, 1f);
            else if (node.nodeType == NodeType.Waypoint) btnColor = new Color(0.7f, 0.7f, 0.7f);

            GUI.backgroundColor = selectedNode == node ? new Color(0.4f, 0.8f, 0.4f) : btnColor;
            if (GUILayout.Button(displayName, EditorStyles.miniButtonLeft))
            {
                selectedNode = node;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedNode != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Architecting: {selectedNode.gameObject.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ping in Hierarchy", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(selectedNode.gameObject);
                Selection.activeGameObject = selectedNode.gameObject;
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            DrawConnectionMatrix();
            EditorGUILayout.Space();
            DrawRouteEconomyBalancer();

            EditorGUILayout.Space();

            if (cachedEditor == null || cachedEditor.target != selectedNode)
            {
                if (cachedEditor != null) DestroyImmediate(cachedEditor);
                cachedEditor = Editor.CreateEditor(selectedNode);
            }

            cachedEditor.OnInspectorGUI();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a Location Node from the scene to architect routes.", EditorStyles.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawConnectionMatrix()
    {
        SerializedObject so = new SerializedObject(selectedNode);
        SerializedProperty connectionsProp = so.FindProperty("connections");

        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Route Connection Matrix", EditorStyles.boldLabel);
        GUILayout.Label("Click a node below to instantly build or tear down a road to that location.", EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(5);

        List<LocationNode> currentDestinations = new List<LocationNode>();
        if (connectionsProp != null)
        {
            for (int i = 0; i < connectionsProp.arraySize; i++)
            {
                var destProp = connectionsProp.GetArrayElementAtIndex(i).FindPropertyRelative("destinationNode");
                if (destProp != null && destProp.objectReferenceValue != null)
                {
                    currentDestinations.Add(destProp.objectReferenceValue as LocationNode);
                }
            }
        }

        int columns = 3;
        for (int i = 0; i < allNodes.Count; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = 0; j < columns; j++)
            {
                if (i + j < allNodes.Count)
                {
                    LocationNode targetNode = allNodes[i + j];

                    if (targetNode == selectedNode)
                    {
                        GUILayout.Button("[This Location]", EditorStyles.miniButton, GUILayout.Height(25));
                        continue;
                    }

                    bool isConnected = currentDestinations.Contains(targetNode);
                    GUI.backgroundColor = isConnected ? new Color(0.4f, 0.8f, 0.4f) : Color.white;

                    string displayName = string.IsNullOrEmpty(targetNode.locationName) ? targetNode.gameObject.name : targetNode.locationName;
                    string prefix = isConnected ? "↔ " : "+ ";

                    if (GUILayout.Button(prefix + displayName, EditorStyles.miniButton, GUILayout.Height(25)))
                    {
                        if (isConnected) RemoveConnection(so, connectionsProp, targetNode);
                        else AddConnection(so, connectionsProp, targetNode);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        so.ApplyModifiedProperties();
        EditorGUILayout.EndVertical();
    }

    private void AddConnection(SerializedObject so, SerializedProperty list, LocationNode target)
    {
        GameObject roadGO = new GameObject($"Road_{selectedNode.gameObject.name}_to_{target.gameObject.name}");

        roadGO.transform.SetParent(selectedNode.transform);
        roadGO.transform.localPosition = Vector3.zero;

        SplineContainer splineContainer = roadGO.AddComponent<SplineContainer>();
        Spline spline = splineContainer.Spline;
        spline.Clear();

        Vector3 startPos = roadGO.transform.InverseTransformPoint(selectedNode.transform.position);
        Vector3 endPos = roadGO.transform.InverseTransformPoint(target.transform.position);

        spline.Add(new BezierKnot(startPos));
        spline.Add(new BezierKnot(endPos));

        spline.SetTangentMode(0, TangentMode.Continuous);
        spline.SetTangentMode(1, TangentMode.Continuous);

        list.arraySize++;
        SerializedProperty newElement = list.GetArrayElementAtIndex(list.arraySize - 1);

        newElement.FindPropertyRelative("destinationNode").objectReferenceValue = target;
        newElement.FindPropertyRelative("roadSpline").objectReferenceValue = splineContainer;
        newElement.FindPropertyRelative("travelTimeHours").intValue = 2;
        newElement.FindPropertyRelative("ambushChance").intValue = 2;
        newElement.FindPropertyRelative("roadColor").colorValue = Color.white;
        newElement.FindPropertyRelative("combatSceneName").stringValue = "DomeBattle";

        so.ApplyModifiedProperties();
    }

    private void RemoveConnection(SerializedObject so, SerializedProperty list, LocationNode target)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            var destProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("destinationNode");
            if (destProp != null && destProp.objectReferenceValue == target)
            {
                var splineProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("roadSpline");
                if (splineProp != null && splineProp.objectReferenceValue != null)
                {
                    SplineContainer container = splineProp.objectReferenceValue as SplineContainer;
                    if (container != null) DestroyImmediate(container.gameObject);
                }

                list.DeleteArrayElementAtIndex(i);
                break;
            }
        }
        so.ApplyModifiedProperties();
    }

    private void DrawRouteEconomyBalancer()
    {
        SerializedObject so = new SerializedObject(selectedNode);
        SerializedProperty connectionsProp = so.FindProperty("connections");

        if (connectionsProp == null || connectionsProp.arraySize == 0) return;

        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Route Economy & Threat Predictor", EditorStyles.boldLabel);
        GUILayout.Space(5);

        for (int i = 0; i < connectionsProp.arraySize; i++)
        {
            SerializedProperty connProp = connectionsProp.GetArrayElementAtIndex(i);
            SerializedProperty destProp = connProp.FindPropertyRelative("destinationNode");

            if (destProp == null || destProp.objectReferenceValue == null) continue;

            LocationNode destNode = destProp.objectReferenceValue as LocationNode;
            string destName = string.IsNullOrEmpty(destNode.locationName) ? destNode.gameObject.name : destNode.locationName;

            EditorGUILayout.BeginVertical("box");

            GUILayout.Label($"Route to: {destName}", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(200));
            SerializedProperty timeProp = connProp.FindPropertyRelative("travelTimeHours");

            EditorGUI.BeginChangeCheck();
            timeProp.intValue = EditorGUILayout.IntSlider("Travel Hours", timeProp.intValue, 0, 48);
            if (EditorGUI.EndChangeCheck()) so.ApplyModifiedProperties();

            float fuelCost = timeProp.intValue * fuelPerHour;
            float rationCost = timeProp.intValue * rationsPerHour;

            GUI.contentColor = fuelCost > 100 ? new Color(1f, 0.4f, 0.4f) : Color.white;
            GUILayout.Label($"Fuel Cost: ~{fuelCost:F0} ({fuelPerHour}/hr)", EditorStyles.miniLabel);

            GUI.contentColor = rationCost > 100 ? new Color(1f, 0.4f, 0.4f) : Color.white;
            GUILayout.Label($"Ration Cost: ~{rationCost:F0} ({rationsPerHour}/hr)", EditorStyles.miniLabel);
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            SerializedProperty ambushProp = connProp.FindPropertyRelative("ambushChance");

            EditorGUI.BeginChangeCheck();
            ambushProp.intValue = EditorGUILayout.IntSlider("Ambush Risk (/10)", ambushProp.intValue, 0, 10);
            if (EditorGUI.EndChangeCheck()) so.ApplyModifiedProperties();

            SerializedProperty lootProp = connProp.FindPropertyRelative("forageLootTable");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Forage Loot:", EditorStyles.miniLabel, GUILayout.Width(75));
            lootProp.objectReferenceValue = EditorGUILayout.ObjectField(lootProp.objectReferenceValue, typeof(LootTable), false);
            EditorGUILayout.EndHorizontal();

            GUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }
}