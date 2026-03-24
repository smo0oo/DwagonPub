using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class BestiaryForgeWindow : EditorWindow
{
    // --- Configuration ---
    private const string BASE_PATH = "Assets/GameData/Enemies/";
    private const string CLASS_PATH = BASE_PATH + "Classes/";
    private const string PROFILE_PATH = BASE_PATH + "AIProfiles/";

    // --- AAA FEATURE: The 3rd Tab for Prefabs! ---
    private enum Tab { Classes, AIProfiles, Models }
    private Tab currentTab = Tab.Classes;

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchQuery = "";

    // Data Caches
    private List<EnemyClass> allClasses = new List<EnemyClass>();
    private List<AIBehaviorProfile> allProfiles = new List<AIBehaviorProfile>();
    private List<GameObject> allEnemyPrefabs = new List<GameObject>(); // The Prefab Cache

    // Current Selection
    private EnemyClass selectedClass;
    private AIBehaviorProfile selectedProfile;
    private GameObject selectedPrefab;
    private Editor cachedEditor;

    // Simulator Settings
    private int simulatorLevel = 1;

    [MenuItem("Tools/DwagonPub/Bestiary Forge")]
    public static void ShowWindow()
    {
        BestiaryForgeWindow window = GetWindow<BestiaryForgeWindow>("Bestiary Forge");
        window.minSize = new Vector2(950, 650);
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
    }

    private void EnsureDirectoriesExist()
    {
        string[] paths = { CLASS_PATH, PROFILE_PATH };
        foreach (string path in paths)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[Bestiary Forge] Created missing directory: {path}");
            }
        }
        AssetDatabase.Refresh();
    }

    private void LoadAllAssets()
    {
        allClasses.Clear();
        allProfiles.Clear();
        allEnemyPrefabs.Clear();

        // 1. Load Classes
        string[] classGuids = AssetDatabase.FindAssets("t:EnemyClass");
        foreach (string guid in classGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyClass eClass = AssetDatabase.LoadAssetAtPath<EnemyClass>(path);
            if (eClass != null) allClasses.Add(eClass);
        }

        // 2. Load Profiles
        string[] profileGuids = AssetDatabase.FindAssets("t:AIBehaviorProfile");
        foreach (string guid in profileGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AIBehaviorProfile profile = AssetDatabase.LoadAssetAtPath<AIBehaviorProfile>(path);
            if (profile != null) allProfiles.Add(profile);
        }

        // 3. Load Enemy Prefabs (AAA Component Scanning!)
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // If it's a prefab and it has your custom AI script attached, it belongs in the Bestiary!
            if (obj != null && obj.GetComponentInChildren<EnemyAI>() != null)
            {
                allEnemyPrefabs.Add(obj);
            }
        }

        allClasses = allClasses.OrderBy(c => c.name).ToList();
        allProfiles = allProfiles.OrderBy(p => p.name).ToList();
        allEnemyPrefabs = allEnemyPrefabs.OrderBy(p => p.name).ToList();
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

        if (GUILayout.Toggle(currentTab == Tab.Classes, "Enemy Classes (Stats)", EditorStyles.toolbarButton)) currentTab = Tab.Classes;
        if (GUILayout.Toggle(currentTab == Tab.AIProfiles, "AI Profiles (Behaviors)", EditorStyles.toolbarButton)) currentTab = Tab.AIProfiles;

        // The new Models tab!
        if (GUILayout.Toggle(currentTab == Tab.Models, "3D Models & Animation", EditorStyles.toolbarButton)) currentTab = Tab.Models;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Bestiary", EditorStyles.toolbarButton)) LoadAllAssets();

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

        if (currentTab == Tab.Classes)
        {
            foreach (EnemyClass eClass in allClasses)
            {
                if (!string.IsNullOrEmpty(searchQuery) && !eClass.name.ToLower().Contains(searchQuery.ToLower())) continue;

                GUI.backgroundColor = selectedClass == eClass ? new Color(1f, 0.4f, 0.4f) : Color.white;
                if (GUILayout.Button(eClass.name, EditorStyles.miniButtonLeft)) SelectAsset(eClass, null, null);
                GUI.backgroundColor = Color.white;
            }
        }
        else if (currentTab == Tab.AIProfiles)
        {
            foreach (AIBehaviorProfile profile in allProfiles)
            {
                if (!string.IsNullOrEmpty(searchQuery) && !profile.name.ToLower().Contains(searchQuery.ToLower())) continue;

                GUI.backgroundColor = selectedProfile == profile ? new Color(0.4f, 0.8f, 1f) : Color.white;
                if (GUILayout.Button(profile.name, EditorStyles.miniButtonLeft)) SelectAsset(null, profile, null);
                GUI.backgroundColor = Color.white;
            }
        }
        else if (currentTab == Tab.Models)
        {
            foreach (GameObject prefab in allEnemyPrefabs)
            {
                if (!string.IsNullOrEmpty(searchQuery) && !prefab.name.ToLower().Contains(searchQuery.ToLower())) continue;

                GUI.backgroundColor = selectedPrefab == prefab ? new Color(0.6f, 1f, 0.6f) : Color.white;
                if (GUILayout.Button(prefab.name, EditorStyles.miniButtonLeft)) SelectAsset(null, null, prefab);
                GUI.backgroundColor = Color.white;
            }
        }

        EditorGUILayout.EndScrollView();

        if (currentTab != Tab.Models)
        {
            GUILayout.Space(5);
            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button($"Create New {currentTab}", GUILayout.Height(30))) CreateNewAsset();
            GUI.backgroundColor = Color.white;
        }

        GUILayout.EndVertical();
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        UnityEngine.Object targetObj = null;
        if (currentTab == Tab.Classes) targetObj = selectedClass;
        else if (currentTab == Tab.AIProfiles) targetObj = selectedProfile;
        else if (currentTab == Tab.Models) targetObj = selectedPrefab;

        if (targetObj != null)
        {
            // Title & Reveal Button
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Dissecting: {targetObj.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (currentTab == Tab.Models)
            {
                if (GUILayout.Button("Open in Prefab Mode", EditorStyles.miniButton, GUILayout.Width(140)))
                {
                    AssetDatabase.OpenAsset(targetObj);
                }
            }

            if (GUILayout.Button("Reveal in Project", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(targetObj);
                Selection.activeObject = targetObj;
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // --- AAA FEATURES ---
            if (currentTab == Tab.Classes && selectedClass != null)
            {
                DrawScalingSimulator();
                DrawStandardInspector(targetObj);
            }
            else if (currentTab == Tab.AIProfiles && selectedProfile != null)
            {
                DrawBehaviorInjectionToolbar();
                DrawStandardInspector(targetObj);
            }
            else if (currentTab == Tab.Models && selectedPrefab != null)
            {
                DrawPrefabPreviewViewer();
            }
            // --------------------

        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a target from the Bestiary.", EditorStyles.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawStandardInspector(UnityEngine.Object targetObj)
    {
        EditorGUILayout.Space();
        if (cachedEditor == null || cachedEditor.target != targetObj)
        {
            if (cachedEditor != null) DestroyImmediate(cachedEditor);
            cachedEditor = Editor.CreateEditor(targetObj);
        }
        cachedEditor.OnInspectorGUI();
    }

    // --- AAA FEATURE 3: 3D Model & Animation Extractor ---
    private void DrawPrefabPreviewViewer()
    {
        EditorGUILayout.BeginHorizontal();

        // 1. The 3D Interactive View
        GUILayout.BeginVertical("helpBox", GUILayout.Width(350));
        GUILayout.Label("3D Character View (Drag to Rotate)", EditorStyles.centeredGreyMiniLabel);

        if (cachedEditor == null || cachedEditor.target != selectedPrefab)
        {
            if (cachedEditor != null) DestroyImmediate(cachedEditor);
            cachedEditor = Editor.CreateEditor(selectedPrefab);
        }

        // This renders the actual 3D model!
        Rect previewRect = GUILayoutUtility.GetRect(330, 330);
        cachedEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
        GUILayout.EndVertical();

        // 2. The Animation Extractor
        GUILayout.BeginVertical("helpBox", GUILayout.ExpandWidth(true));
        GUILayout.Label("Animation Extractor", EditorStyles.boldLabel);

        Animator anim = selectedPrefab.GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;

            GUILayout.Label($"Found {clips.Length} Animations inside '{anim.runtimeAnimatorController.name}'", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // Draw a neat wrapping grid of animations
            int columns = 2;
            for (int i = 0; i < clips.Length; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < columns; j++)
                {
                    if (i + j < clips.Length)
                    {
                        AnimationClip clip = clips[i + j];
                        if (GUILayout.Button($"► {clip.name}", GUILayout.Height(30)))
                        {
                            // By selecting the clip, Unity's built-in animation previewer
                            // (bottom right of the inspector) will instantly start playing it!
                            Selection.activeObject = clip;
                            EditorGUIUtility.PingObject(clip);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(15);
            GUILayout.Label("Note: Click an animation to highlight it. Look at the bottom-right of your main Unity Inspector to see it play!", EditorStyles.wordWrappedMiniLabel);
        }
        else
        {
            GUILayout.Label("No Animator or RuntimeAnimatorController found on this prefab.", EditorStyles.helpBox);
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawScalingSimulator()
    {
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("AAA Level Scaling Simulator", EditorStyles.boldLabel);
        GUILayout.Label("Drag the slider to see exact stats at any level based on EnemyAI.cs math!", EditorStyles.wordWrappedMiniLabel);

        GUILayout.Space(5);
        simulatorLevel = EditorGUILayout.IntSlider("Target Level", simulatorLevel, 1, 100);

        float hpScale = Mathf.Pow(1.15f, simulatorLevel - 1);
        float dmgScale = Mathf.Pow(1.10f, simulatorLevel - 1);
        float armorScale = (simulatorLevel - 1) * 0.02f;

        int simHealth = Mathf.RoundToInt(selectedClass.maxHealth * hpScale);
        float simDamage = selectedClass.damageMultiplier * dmgScale;
        float simArmor = Mathf.Clamp(selectedClass.damageMitigation + armorScale, 0f, 0.85f);

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        GUILayout.BeginVertical("box", GUILayout.Width(120));
        GUILayout.Label("Max Health", EditorStyles.centeredGreyMiniLabel);
        GUILayout.Label($"{simHealth}", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 16 });
        GUILayout.EndVertical();

        GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
        GUILayout.BeginVertical("box", GUILayout.Width(120));
        GUILayout.Label("Damage Multiplier", EditorStyles.centeredGreyMiniLabel);
        GUILayout.Label($"x{simDamage:F2}", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 16 });
        GUILayout.EndVertical();

        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        GUILayout.BeginVertical("box", GUILayout.Width(120));
        GUILayout.Label("Armor Mitigation", EditorStyles.centeredGreyMiniLabel);
        GUILayout.Label($"{(simArmor * 100f):F1}%", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 16 });
        GUILayout.EndVertical();

        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawBehaviorInjectionToolbar()
    {
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Quick Inject Triggers", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("+ Health Phase Trigger", GUILayout.Height(25))) InjectTrigger("healthTriggers");

        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("+ Reactive Dodge/Counter", GUILayout.Height(25))) InjectTrigger("reactiveTriggers");

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void InjectTrigger(string listPropertyName)
    {
        SerializedObject so = new SerializedObject(selectedProfile);
        SerializedProperty triggerList = so.FindProperty(listPropertyName);

        if (triggerList != null)
        {
            triggerList.arraySize++;
            so.ApplyModifiedProperties();
            Debug.Log($"[Bestiary Forge] Added new trigger to {selectedProfile.name}!");
        }
        else
        {
            Debug.LogWarning($"[Bestiary Forge] Could not find the list '{listPropertyName}' on AIBehaviorProfile.");
        }
    }

    private void SelectAsset(EnemyClass eClass, AIBehaviorProfile profile, GameObject prefab)
    {
        selectedClass = eClass;
        selectedProfile = profile;
        selectedPrefab = prefab;
        simulatorLevel = 1;
        GUI.FocusControl(null);
    }

    private void CreateNewAsset()
    {
        string defaultName = $"New_{currentTab}_{System.DateTime.Now.Ticks}";
        string path = "";

        if (currentTab == Tab.Classes)
        {
            path = CLASS_PATH + defaultName + ".asset";
            EnemyClass newClass = ScriptableObject.CreateInstance<EnemyClass>();
            AssetDatabase.CreateAsset(newClass, path);
            LoadAllAssets();
            SelectAsset(newClass, null, null);
        }
        else if (currentTab == Tab.AIProfiles)
        {
            path = PROFILE_PATH + defaultName + ".asset";
            AIBehaviorProfile newProfile = ScriptableObject.CreateInstance<AIBehaviorProfile>();
            AssetDatabase.CreateAsset(newProfile, path);
            LoadAllAssets();
            SelectAsset(null, newProfile, null);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Bestiary Forge] Successfully created {defaultName} at {path}");
    }
}