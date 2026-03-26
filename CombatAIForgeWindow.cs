using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class CombatAIForgeWindow : EditorWindow
{
    // --- Configuration ---
    private const string AI_PATH = "Assets/GameData/AIProfiles/";

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchQuery = "";

    // Data Caches
    private List<AIBehaviorProfile> allProfiles = new List<AIBehaviorProfile>();
    private AIBehaviorProfile selectedProfile;

    // UI Styling
    private GUIStyle gambitKeywordStyle;

    [MenuItem("Tools/DwagonPub/Combat AI Architect")]
    public static void ShowWindow()
    {
        CombatAIForgeWindow window = GetWindow<CombatAIForgeWindow>("AI Architect");
        window.minSize = new Vector2(1000, 600);
        window.Show();
    }

    private void OnEnable()
    {
        if (!System.IO.Directory.Exists(AI_PATH))
        {
            System.IO.Directory.CreateDirectory(AI_PATH);
            AssetDatabase.Refresh();
        }
        LoadAllAssets();
    }

    private void LoadAllAssets()
    {
        allProfiles.Clear();
        string[] guids = AssetDatabase.FindAssets("t:AIBehaviorProfile");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AIBehaviorProfile profile = AssetDatabase.LoadAssetAtPath<AIBehaviorProfile>(path);
            if (profile != null) allProfiles.Add(profile);
        }
        allProfiles = allProfiles.OrderBy(p => p.name).ToList();
    }

    private void InitializeStyles()
    {
        if (gambitKeywordStyle == null)
        {
            gambitKeywordStyle = new GUIStyle(EditorStyles.boldLabel);
            gambitKeywordStyle.normal.textColor = new Color(0.4f, 0.8f, 1f); // Cyan logic words
            gambitKeywordStyle.alignment = TextAnchor.MiddleCenter;
        }
    }

    private void OnGUI()
    {
        InitializeStyles();
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawLeftPane();
        DrawRightPane();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Combat AI & Gambit Architect", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Library", EditorStyles.toolbarButton)) LoadAllAssets();
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
        GUILayout.Space(5);

        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        foreach (AIBehaviorProfile profile in allProfiles)
        {
            if (profile == null) continue;
            if (!string.IsNullOrEmpty(searchQuery) && !profile.name.ToLower().Contains(searchQuery.ToLower())) continue;

            GUI.backgroundColor = selectedProfile == profile ? new Color(0.4f, 0.8f, 1f) : Color.white;

            if (GUILayout.Button(profile.name, EditorStyles.miniButtonLeft, GUILayout.Height(25)))
            {
                selectedProfile = profile;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Forge New AI Profile", GUILayout.Height(30))) CreateNewAsset();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedProfile != null)
        {
            SerializedObject so = new SerializedObject(selectedProfile);
            so.Update();

            // HEADER
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Architecting Gambit: {selectedProfile.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ping in Project", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(selectedProfile);
                Selection.activeObject = selectedProfile;
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Delete Profile?", $"Permanently delete {selectedProfile.name}?", "Yes", "Cancel"))
                {
                    string path = AssetDatabase.GetAssetPath(selectedProfile);
                    AssetDatabase.DeleteAsset(path);
                    selectedProfile = null;
                    LoadAllAssets();
                    GUIUtility.ExitGUI();
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            DrawHealthTriggersSection(so);
            EditorGUILayout.Space();

            DrawReactiveTriggersSection(so);

            so.ApplyModifiedProperties();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select an AI Profile from the library to architect its gambits.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawHealthTriggersSection(SerializedObject so)
    {
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Phase & Threshold Gambits", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Health Trigger", EditorStyles.miniButton))
        {
            so.FindProperty("healthTriggers").arraySize++;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(5);

        SerializedProperty listProp = so.FindProperty("healthTriggers");

        if (listProp.arraySize == 0)
        {
            GUILayout.Label("No health thresholds defined.", EditorStyles.centeredGreyMiniLabel);
        }

        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty triggerProp = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            // THE GAMBIT LOGIC SENTENCE
            GUILayout.Label("IF HP <", gambitKeywordStyle, GUILayout.Width(60));

            SerializedProperty hpProp = triggerProp.FindPropertyRelative("healthPercentage");
            hpProp.floatValue = EditorGUILayout.Slider(hpProp.floatValue, 0f, 1f, GUILayout.Width(120));
            GUILayout.Label($"({Mathf.RoundToInt(hpProp.floatValue * 100)}%)", EditorStyles.miniLabel, GUILayout.Width(40));

            GUILayout.Label("THEN USE", gambitKeywordStyle, GUILayout.Width(75));
            SerializedProperty abilityProp = triggerProp.FindPropertyRelative("abilityToUse");
            abilityProp.objectReferenceValue = EditorGUILayout.ObjectField(abilityProp.objectReferenceValue, typeof(ScriptableObject), false);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(25)))
            {
                listProp.DeleteArrayElementAtIndex(i);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // PHASE TRANSITION MECHANICS (Expanded settings)
            GUILayout.Space(5);
            SerializedProperty phaseProp = triggerProp.FindPropertyRelative("isPhaseTransition");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(65); // Indent
            EditorGUILayout.PropertyField(phaseProp, new GUIContent("Trigger Boss Phase Transition?"), GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            if (phaseProp.boolValue)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(65); // Indent
                EditorGUILayout.BeginVertical("helpBox");

                GUILayout.Label("Phase Transition Rules", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(triggerProp.FindPropertyRelative("invulnerabilityDuration"), new GUIContent("Invuln Time (s)"));
                EditorGUILayout.PropertyField(triggerProp.FindPropertyRelative("phaseAnimationTrigger"), new GUIContent("Anim Trigger"));
                EditorGUILayout.PropertyField(triggerProp.FindPropertyRelative("clearDebuffsOnPhase"), new GUIContent("Clear Debuffs"));

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawReactiveTriggersSection(SerializedObject so)
    {
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Reactive & Defensive Gambits", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Reactive Trigger", EditorStyles.miniButton))
        {
            so.FindProperty("reactiveTriggers").arraySize++;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(5);

        SerializedProperty listProp = so.FindProperty("reactiveTriggers");

        if (listProp.arraySize == 0)
        {
            GUILayout.Label("No reactive behaviors defined.", EditorStyles.centeredGreyMiniLabel);
        }

        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty triggerProp = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            // THE GAMBIT LOGIC SENTENCE
            GUILayout.Label("IF HIT BY", gambitKeywordStyle, GUILayout.Width(75));

            // Optional: Specific Ability OR Type
            SerializedProperty specificAbilityProp = triggerProp.FindPropertyRelative("specificAbilityTrigger");
            if (specificAbilityProp.objectReferenceValue != null)
            {
                specificAbilityProp.objectReferenceValue = EditorGUILayout.ObjectField(specificAbilityProp.objectReferenceValue, typeof(ScriptableObject), false, GUILayout.Width(120));
            }
            else
            {
                // If no specific ability is slotted, show the Enum dropdown for Ability Type
                // Assuming AbilityType is an enum you have defined elsewhere, we use PropertyField
                EditorGUILayout.PropertyField(triggerProp.FindPropertyRelative("triggerType"), GUIContent.none, GUILayout.Width(120));
            }

            // A tiny button to swap between triggering by exact ability vs ability type
            if (GUILayout.Button("Swap", EditorStyles.miniButton, GUILayout.Width(45)))
            {
                if (specificAbilityProp.objectReferenceValue != null) specificAbilityProp.objectReferenceValue = null;
                // Otherwise, the user just drags an ability into the field next time
            }

            GUILayout.Label("THEN", gambitKeywordStyle, GUILayout.Width(45));

            SerializedProperty actionProp = triggerProp.FindPropertyRelative("reactionAction");
            EditorGUILayout.PropertyField(actionProp, GUIContent.none, GUILayout.Width(130));

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(25)))
            {
                listProp.DeleteArrayElementAtIndex(i);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // REACTION PARAMETERS
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(80); // Indent

            SerializedProperty chanceProp = triggerProp.FindPropertyRelative("chanceToReact");
            GUILayout.Label("Chance:", EditorStyles.miniLabel, GUILayout.Width(50));
            chanceProp.floatValue = EditorGUILayout.Slider(chanceProp.floatValue, 0f, 1f, GUILayout.Width(100));

            AIReactionAction currentAction = (AIReactionAction)actionProp.enumValueIndex;

            if (currentAction == AIReactionAction.CastReactionAbility)
            {
                GUILayout.Label("Cast:", EditorStyles.miniLabel, GUILayout.Width(35));
                SerializedProperty reactAbilProp = triggerProp.FindPropertyRelative("reactionAbility");
                reactAbilProp.objectReferenceValue = EditorGUILayout.ObjectField(reactAbilProp.objectReferenceValue, typeof(ScriptableObject), false);
            }
            else if (currentAction == AIReactionAction.LeapBackward || currentAction == AIReactionAction.RollAway || currentAction == AIReactionAction.TeleportAway)
            {
                GUILayout.Label("Dist:", EditorStyles.miniLabel, GUILayout.Width(35));
                EditorGUILayout.PropertyField(triggerProp.FindPropertyRelative("movementDistance"), GUIContent.none, GUILayout.Width(50));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private void CreateNewAsset()
    {
        string defaultName = $"NewAIProfile_{System.DateTime.Now.Ticks}";
        string path = AI_PATH + defaultName + ".asset";

        AIBehaviorProfile newAsset = ScriptableObject.CreateInstance<AIBehaviorProfile>();

        AssetDatabase.CreateAsset(newAsset, path);
        AssetDatabase.SaveAssets();

        LoadAllAssets();
        selectedProfile = newAsset;
        GUI.FocusControl(null);

        Debug.Log($"[Combat AI Architect] Forged {defaultName} at {path}");
    }
}