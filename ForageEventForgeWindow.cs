using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ForageEventForgeWindow : EditorWindow
{
    // --- Configuration ---
    private const string EVENT_PATH = "Assets/GameData/ForageEvents/";

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchQuery = "";

    // Data Caches
    private List<ForageEventData> allEvents = new List<ForageEventData>();

    // Current Selection
    private ForageEventData selectedEvent;

    [MenuItem("Tools/DwagonPub/Forage Event Forge")]
    public static void ShowWindow()
    {
        ForageEventForgeWindow window = GetWindow<ForageEventForgeWindow>("Forage Architect");
        window.minSize = new Vector2(900, 600);
        window.Show();
    }

    private void OnEnable()
    {
        if (!System.IO.Directory.Exists(EVENT_PATH))
        {
            System.IO.Directory.CreateDirectory(EVENT_PATH);
            AssetDatabase.Refresh();
        }
        LoadAllAssets();
    }

    private void LoadAllAssets()
    {
        allEvents.Clear();

        string[] guids = AssetDatabase.FindAssets("t:ForageEventData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ForageEventData evt = AssetDatabase.LoadAssetAtPath<ForageEventData>(path);
            if (evt != null) allEvents.Add(evt);
        }

        allEvents = allEvents.OrderBy(e => e.eventType).ThenBy(e => e.name).ToList();
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
        GUILayout.Label("Narrative Foraging Architect", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Events", EditorStyles.toolbarButton)) LoadAllAssets();
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

        foreach (ForageEventData evt in allEvents)
        {
            if (evt == null) continue;
            if (!string.IsNullOrEmpty(searchQuery) && !evt.name.ToLower().Contains(searchQuery.ToLower())) continue;

            Color typeColor = GetEventColor(evt.eventType);

            GUI.backgroundColor = selectedEvent == evt ? Color.Lerp(typeColor, Color.white, 0.5f) : typeColor;

            if (GUILayout.Button(evt.name, EditorStyles.miniButtonLeft, GUILayout.Height(22)))
            {
                selectedEvent = evt;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Scribe New Event", GUILayout.Height(30))) CreateNewAsset();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private Color GetEventColor(ForageEventType type)
    {
        switch (type)
        {
            case ForageEventType.Bust: return new Color(0.7f, 0.7f, 0.7f);
            case ForageEventType.StandardLoot: return new Color(0.6f, 0.9f, 0.6f);
            case ForageEventType.Jackpot: return new Color(1f, 0.8f, 0.4f);
            case ForageEventType.Ambush: return new Color(1f, 0.5f, 0.5f);
            case ForageEventType.HiddenDungeon: return new Color(0.8f, 0.6f, 1f);
            case ForageEventType.DialogueEvent: return new Color(0.4f, 0.8f, 1f);
            default: return Color.white;
        }
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedEvent != null)
        {
            SerializedObject so = new SerializedObject(selectedEvent);
            so.Update();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Editing: {selectedEvent.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ping in Project", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(selectedEvent);
                Selection.activeObject = selectedEvent;
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Delete Event?", $"Are you sure you want to permanently delete {selectedEvent.name}?", "Yes", "Cancel"))
                {
                    string path = AssetDatabase.GetAssetPath(selectedEvent);
                    AssetDatabase.DeleteAsset(path);
                    selectedEvent = null;
                    LoadAllAssets();
                    GUIUtility.ExitGUI();
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            DrawNarrativeCard(so);
            EditorGUILayout.Space();

            DrawMechanicsSection(so);

            so.ApplyModifiedProperties();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a Forage Event from the library to edit.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawNarrativeCard(SerializedObject so)
    {
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Narrative Presentation", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(150));
        SerializedProperty artProp = so.FindProperty("contextualArt");

        Texture2D tex = null;
        if (artProp.objectReferenceValue != null && artProp.objectReferenceValue is Sprite sprite)
        {
            tex = AssetPreview.GetAssetPreview(sprite);
            if (tex == null) tex = sprite.texture;
        }

        if (tex != null)
        {
            GUILayout.Label(tex, GUILayout.Width(150), GUILayout.Height(150));
        }
        else
        {
            GUILayout.Box("No\nContextual\nArt", GUILayout.Width(150), GUILayout.Height(150));
        }

        artProp.objectReferenceValue = EditorGUILayout.ObjectField(artProp.objectReferenceValue, typeof(Sprite), false, GUILayout.Width(150));
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        SerializedProperty typeProp = so.FindProperty("eventType");
        EditorGUILayout.PropertyField(typeProp);
        GUILayout.Space(5);

        SerializedProperty nameProp = so.FindProperty("eventName");
        GUILayout.Label("Event Title (UI Header)", EditorStyles.miniLabel);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.textField);
        titleStyle.fontStyle = FontStyle.Bold;
        nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue, titleStyle);

        GUILayout.Space(10);

        SerializedProperty storyProp = so.FindProperty("storySnippet");
        GUILayout.Label("Story Snippet (Main Body Text)", EditorStyles.miniLabel);

        GUIStyle storyStyle = new GUIStyle(EditorStyles.textArea);
        storyStyle.wordWrap = true;
        storyProp.stringValue = EditorGUILayout.TextArea(storyProp.stringValue, storyStyle, GUILayout.Height(85));

        GUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawMechanicsSection(SerializedObject so)
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Rewards & Mechanics", EditorStyles.boldLabel);
        GUILayout.Space(5);

        ForageEventType currentType = (ForageEventType)so.FindProperty("eventType").enumValueIndex;

        SerializedProperty lootProp = so.FindProperty("rewardTable");
        SerializedProperty sceneProp = so.FindProperty("linkedSceneName");
        SerializedProperty btnProp = so.FindProperty("acceptButtonText");
        SerializedProperty convoProp = so.FindProperty("conversationTitle");

        EditorGUILayout.PropertyField(btnProp);
        GUILayout.Space(5);

        if (currentType == ForageEventType.StandardLoot || currentType == ForageEventType.Jackpot || currentType == ForageEventType.Ambush)
        {
            EditorGUILayout.PropertyField(lootProp);
        }

        if (currentType == ForageEventType.HiddenDungeon || currentType == ForageEventType.Ambush)
        {
            EditorGUILayout.PropertyField(sceneProp);
            if (string.IsNullOrEmpty(sceneProp.stringValue))
            {
                EditorGUILayout.HelpBox("A Linked Scene Name is required for Ambush or Hidden Dungeon events!", MessageType.Warning);
            }
        }

        GUILayout.Space(5);
        GUILayout.Label("Pixel Crushers Integration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(convoProp, new GUIContent("Dialogue Conversation"));

        if (!string.IsNullOrEmpty(convoProp.stringValue))
        {
            EditorGUILayout.HelpBox("When this event is accepted, it will trigger the Dialogue System. You can use Lua in that conversation to start/stop quests!", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void CreateNewAsset()
    {
        string defaultName = $"NewEvent_{System.DateTime.Now.Ticks}";
        string path = EVENT_PATH + defaultName + ".asset";

        ForageEventData newAsset = ScriptableObject.CreateInstance<ForageEventData>();
        AssetDatabase.CreateAsset(newAsset, path);
        AssetDatabase.SaveAssets();

        LoadAllAssets();
        selectedEvent = newAsset;
        GUI.FocusControl(null);

        Debug.Log($"[Forage Architect] Successfully scribed {defaultName} at {path}");
    }
}