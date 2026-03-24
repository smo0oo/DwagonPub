using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class AbilityForgeWindow : EditorWindow
{
    // --- Configuration ---
    private const string BASE_PATH = "Assets/GameData/Abilities/";
    private const string MELEE_PATH = BASE_PATH + "Melee/";
    private const string MAGIC_PATH = BASE_PATH + "Magic/";
    private const string RANGED_PATH = BASE_PATH + "Ranged/";
    private const string PASSIVE_PATH = BASE_PATH + "Passives/";

    private enum Tab { Melee, Magic, Ranged, Passives }
    private Tab currentTab = Tab.Magic;

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchQuery = "";

    // Data Caches
    private List<Ability> allAbilities = new List<Ability>();

    // Current Selection
    private Ability selectedAbility;
    private Editor cachedEditor;

    // Preview Caches
    private Editor prefabPreviewEditor;
    private GameObject currentPreviewPrefab;

    // --- AAA FEATURE: Hidden Editor Audio Player ---
    private AudioSource previewAudioSource;

    [MenuItem("Tools/DwagonPub/Ability Forge")]
    public static void ShowWindow()
    {
        AbilityForgeWindow window = GetWindow<AbilityForgeWindow>("Ability Forge");
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

        // Clean up our hidden audio player so it doesn't leak into the scene!
        if (previewAudioSource != null) DestroyImmediate(previewAudioSource.gameObject);
    }

    private void EnsureDirectoriesExist()
    {
        string[] paths = { MELEE_PATH, MAGIC_PATH, RANGED_PATH, PASSIVE_PATH };
        foreach (string path in paths)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[Ability Forge] Created missing directory: {path}");
            }
        }
        AssetDatabase.Refresh();
    }

    private void LoadAllAssets()
    {
        allAbilities.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Ability");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Ability ability = AssetDatabase.LoadAssetAtPath<Ability>(path);
            if (ability != null) allAbilities.Add(ability);
        }

        allAbilities = allAbilities.OrderBy(a => a.name).ToList();
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

        if (GUILayout.Toggle(currentTab == Tab.Melee, "Melee Skills", EditorStyles.toolbarButton)) currentTab = Tab.Melee;
        if (GUILayout.Toggle(currentTab == Tab.Magic, "Magic Spells", EditorStyles.toolbarButton)) currentTab = Tab.Magic;
        if (GUILayout.Toggle(currentTab == Tab.Ranged, "Ranged/Tactical", EditorStyles.toolbarButton)) currentTab = Tab.Ranged;
        if (GUILayout.Toggle(currentTab == Tab.Passives, "Passives & Auras", EditorStyles.toolbarButton)) currentTab = Tab.Passives;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh Grimoire", EditorStyles.toolbarButton)) LoadAllAssets();

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

        foreach (Ability ability in allAbilities)
        {
            string path = AssetDatabase.GetAssetPath(ability);
            bool matchesTab = false;

            if (currentTab == Tab.Melee && path.Contains("/Melee/")) matchesTab = true;
            else if (currentTab == Tab.Magic && path.Contains("/Magic/")) matchesTab = true;
            else if (currentTab == Tab.Ranged && path.Contains("/Ranged/")) matchesTab = true;
            else if (currentTab == Tab.Passives && path.Contains("/Passives/")) matchesTab = true;
            else if (!path.Contains("/Melee/") && !path.Contains("/Magic/") && !path.Contains("/Ranged/") && !path.Contains("/Passives/")) matchesTab = true;

            if (!matchesTab) continue;
            if (!string.IsNullOrEmpty(searchQuery) && !ability.name.ToLower().Contains(searchQuery.ToLower())) continue;

            GUI.backgroundColor = selectedAbility == ability ? new Color(0.8f, 0.4f, 1f) : Color.white;
            if (GUILayout.Button(ability.name, EditorStyles.miniButtonLeft))
            {
                SelectAsset(ability);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button($"Scribe New {currentTab} Ability", GUILayout.Height(30))) CreateNewAsset();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawRightPane()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedAbility != null)
        {
            // Title & Reveal Button
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Studying: {selectedAbility.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reveal in Project", EditorStyles.miniButtonRight, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(selectedAbility);
                Selection.activeObject = selectedAbility;
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Render Visuals and Sounds!
            EditorGUILayout.BeginHorizontal();
            DrawVisualPreviews();
            DrawAudioPreviews();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawEffectInjectionToolbar();
            EditorGUILayout.Space();

            // Standard Inspector
            if (cachedEditor == null || cachedEditor.target != selectedAbility)
            {
                if (cachedEditor != null) DestroyImmediate(cachedEditor);
                cachedEditor = Editor.CreateEditor(selectedAbility);
            }

            cachedEditor.OnInspectorGUI();
        }
        else
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a spell from the grimoire or scribe a new one.", EditorStyles.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    // --- AAA FEATURE: Dynamic Audio Scanner ---
    private void DrawAudioPreviews()
    {
        SerializedObject so = new SerializedObject(selectedAbility);
        SerializedProperty prop = so.GetIterator();
        bool enterChildren = true;

        List<SerializedProperty> audioProps = new List<SerializedProperty>();

        // Dynamically find ALL AudioClip properties inside this specific ability
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue is AudioClip)
            {
                audioProps.Add(prop.Copy());
            }
        }

        if (audioProps.Count > 0)
        {
            GUILayout.BeginVertical("helpBox", GUILayout.ExpandWidth(true));
            GUILayout.Label("Audio Soundboard", EditorStyles.centeredGreyMiniLabel);

            foreach (var audioProp in audioProps)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(audioProp.displayName, GUILayout.Width(100));

                AudioClip clip = audioProp.objectReferenceValue as AudioClip;

                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); // Green
                if (GUILayout.Button("► Play", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
                {
                    PlayAudioClip(clip);
                }

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // Red
                if (GUILayout.Button("◼ Stop", EditorStyles.miniButtonRight, GUILayout.Width(50)))
                {
                    StopAudioClip();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
    }

    private void PlayAudioClip(AudioClip clip)
    {
        if (clip == null) return;

        if (previewAudioSource == null)
        {
            // Creates a temporary GameObject that does not show up in your Hierarchy or save to the scene
            GameObject go = EditorUtility.CreateGameObjectWithHideFlags("AbilityAudioPreview", HideFlags.HideAndDontSave, typeof(AudioSource));
            previewAudioSource = go.GetComponent<AudioSource>();
        }

        previewAudioSource.clip = clip;
        previewAudioSource.Play();
    }

    private void StopAudioClip()
    {
        if (previewAudioSource != null && previewAudioSource.isPlaying)
        {
            previewAudioSource.Stop();
        }
    }
    // -----------------------------------------

    private void DrawVisualPreviews()
    {
        GUILayout.BeginHorizontal("helpBox", GUILayout.Width(350));

        SerializedObject so = new SerializedObject(selectedAbility);
        SerializedProperty iconProp = so.FindProperty("icon") ?? so.FindProperty("abilityIcon") ?? so.FindProperty("spellIcon");
        SerializedProperty prefabProp = so.FindProperty("vfxPrefab") ?? so.FindProperty("spellPrefab") ?? so.FindProperty("projectilePrefab") ?? so.FindProperty("abilityPrefab");

        // 2D Icon
        GUILayout.BeginVertical(GUILayout.Width(120));
        GUILayout.Label("Ability Icon", EditorStyles.centeredGreyMiniLabel);

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

        // 3D VFX/Projectile Preview
        GUILayout.BeginVertical(GUILayout.Width(220));
        GUILayout.Label("VFX Preview (Drag to Rotate)", EditorStyles.centeredGreyMiniLabel);

        if (prefabProp != null && prefabProp.objectReferenceValue != null)
        {
            GameObject targetPrefab = prefabProp.objectReferenceValue as GameObject;

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
            GUILayout.Box("No VFX Assigned", GUILayout.Width(200), GUILayout.Height(200));
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DrawEffectInjectionToolbar()
    {
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Quick Inject Payload (Bypasses Unity Dropdowns)", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("+ Damage Effect", GUILayout.Height(25))) InjectEffect(new DamageEffect());

        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("+ Heal Effect", GUILayout.Height(25))) InjectEffect(new HealEffect());

        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("+ Status Effect", GUILayout.Height(25))) InjectEffect(new ApplyStatusEffect());

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void InjectEffect(IAbilityEffect newEffect)
    {
        if (selectedAbility.friendlyEffects == null) selectedAbility.friendlyEffects = new List<IAbilityEffect>();

        SerializedObject so = new SerializedObject(selectedAbility);
        SerializedProperty effectsList = so.FindProperty("friendlyEffects") ?? so.FindProperty("hostileEffects") ?? so.FindProperty("effects");

        if (effectsList != null)
        {
            effectsList.arraySize++;
            SerializedProperty newElement = effectsList.GetArrayElementAtIndex(effectsList.arraySize - 1);
            newElement.managedReferenceValue = newEffect;

            so.ApplyModifiedProperties();
            Debug.Log($"[Ability Forge] Successfully injected {newEffect.GetType().Name} into {selectedAbility.name}!");
        }
        else
        {
            Debug.LogWarning("[Ability Forge] Could not find the effects list property on your Ability script.");
        }
    }

    private void SelectAsset(Ability ability)
    {
        selectedAbility = ability;
        StopAudioClip(); // Stop playing the old sound when switching spells!
        GUI.FocusControl(null);
    }

    private void CreateNewAsset()
    {
        string defaultName = $"New_{currentTab}_{System.DateTime.Now.Ticks}";
        string path = "";

        Ability newAbility = ScriptableObject.CreateInstance<Ability>();
        newAbility.name = "New Spell";

        switch (currentTab)
        {
            case Tab.Melee: path = MELEE_PATH + defaultName + ".asset"; break;
            case Tab.Magic: path = MAGIC_PATH + defaultName + ".asset"; break;
            case Tab.Ranged: path = RANGED_PATH + defaultName + ".asset"; break;
            case Tab.Passives: path = PASSIVE_PATH + defaultName + ".asset"; break;
        }

        AssetDatabase.CreateAsset(newAbility, path);
        LoadAllAssets();
        SelectAsset(newAbility);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Ability Forge] Successfully scribed {defaultName} at {path}");
    }
}