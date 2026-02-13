using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

[CustomEditor(typeof(EntityDebugger))]
public class EntityDebuggerEditor : Editor
{
    private EntityDebugger debugger;
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;

    private const float MAX_ACCEPTABLE_MS = 0.5f;
    private const float LOD_DISTANCE_WARNING = 40f;

    private List<float> perfHistory = new List<float>();
    private const int HISTORY_LENGTH = 120;

    private void OnEnable()
    {
        debugger = (EntityDebugger)target;
        if (Application.isPlaying)
        {
            debugger.healthComponent = debugger.GetComponent<Health>();
            debugger.navAgent = debugger.GetComponent<UnityEngine.AI.NavMeshAgent>();

            debugger.aiComponent = debugger.GetComponent<EnemyAI>();
            debugger.abilityHolder = debugger.GetComponent<EnemyAbilityHolder>();

            debugger.playerMovement = debugger.GetComponent<PlayerMovement>();
            debugger.playerAbilityHolder = debugger.GetComponent<PlayerAbilityHolder>();
        }
    }

    public override void OnInspectorGUI()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            labelStyle = new GUIStyle(EditorStyles.label) { richText = true };
        }

        serializedObject.Update();

        EditorGUILayout.Space();
        string entityType = debugger.playerMovement != null ? "PLAYER" : "ENTITY";
        EditorGUILayout.LabelField($"{entityType} Performance Monitor", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter });
        EditorGUILayout.Space();

        DrawHealthSection();
        EditorGUILayout.Space();
        DrawPerformanceSection();
        EditorGUILayout.Space();

        if (debugger.playerMovement != null) DrawPlayerLogicSection();
        else DrawAISection();

        EditorGUILayout.Space();
        DrawAbilitiesSection(); // Smart handles both
        EditorGUILayout.Space();
        DrawNavSection();

        if (Application.isPlaying) Repaint();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPerformanceSection()
    {
        EditorGUILayout.LabelField("Performance Metrics (CPU)", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Check either Player or Enemy time
        float currentCpuTime = 0f;
        if (debugger.aiComponent != null) currentCpuTime = debugger.aiComponent.LastExecutionTimeMs;
        else if (debugger.playerMovement != null) currentCpuTime = debugger.playerMovement.LastExecutionTimeMs;

        if (Application.isPlaying)
        {
            perfHistory.Add(currentCpuTime);
            if (perfHistory.Count > HISTORY_LENGTH) perfHistory.RemoveAt(0);
        }

        Rect graphRect = EditorGUILayout.GetControlRect(false, 60);
        DrawPerformanceGraph(graphRect, perfHistory, 2.0f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Current: {currentCpuTime:F3} ms", GUILayout.Width(120));
        float avg = perfHistory.Count > 0 ? perfHistory.Average() : 0;
        EditorGUILayout.LabelField($"Avg: {avg:F3} ms");
        float max = perfHistory.Count > 0 ? perfHistory.Max() : 0;
        GUIStyle maxStyle = new GUIStyle(EditorStyles.label);
        if (max > MAX_ACCEPTABLE_MS) maxStyle.normal.textColor = Color.red;
        EditorGUILayout.LabelField($"Peak: {max:F3} ms", maxStyle);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawPerformanceGraph(Rect rect, List<float> history, float maxVal)
    {
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        if (history == null || history.Count < 2) return;

        float limitY = rect.y + rect.height * (1 - (MAX_ACCEPTABLE_MS / maxVal));
        if (limitY >= rect.y && limitY <= rect.yMax)
        {
            Handles.color = new Color(1f, 1f, 0f, 0.3f);
            Handles.DrawLine(new Vector3(rect.x, limitY), new Vector3(rect.xMax, limitY));
        }

        Handles.color = Color.green;
        Vector3[] points = new Vector3[history.Count];
        float stepX = rect.width / (HISTORY_LENGTH - 1);

        for (int i = 0; i < history.Count; i++)
        {
            float val = history[i];
            float normalizedH = Mathf.Clamp01(val / maxVal);
            float yPos = rect.y + rect.height * (1 - normalizedH);
            float xPos = rect.x + (i * stepX);
            points[i] = new Vector3(xPos, yPos, 0);
            if (val > MAX_ACCEPTABLE_MS) Handles.color = Color.red;
        }

        Handles.DrawAAPolyLine(2.0f, points);
        Handles.color = Color.white;
    }

    private void DrawHealthSection()
    {
        EditorGUILayout.LabelField("Health & Status", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (debugger.healthComponent != null)
        {
            float current = debugger.healthComponent.currentHealth;
            float max = debugger.healthComponent.maxHealth;
            float percent = max > 0 ? current / max : 0;
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, percent, $"{current} / {max} HP");
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawAISection()
    {
        if (debugger.aiComponent == null) return;
        EditorGUILayout.LabelField("AI Brain", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        FieldInfo stateField = typeof(EnemyAI).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance);
        if (stateField != null)
        {
            var state = stateField.GetValue(debugger.aiComponent);
            string stateName = state != null ? state.GetType().Name : "Null";
            string color = stateName.Contains("Idle") ? "cyan" : (stateName.Contains("Attack") ? "red" : "yellow");
            EditorGUILayout.LabelField($"Current State: <b><color={color}>{stateName}</color></b>", labelStyle);
        }
        EditorGUILayout.EndVertical();
    }

    // --- NEW: Player Logic Section ---
    private void DrawPlayerLogicSection()
    {
        EditorGUILayout.LabelField("Player Controller", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField($"Mode: {debugger.playerMovement.currentMode}");
        EditorGUILayout.LabelField($"Moving To Attack: {debugger.playerMovement.IsMovingToAttack}");

        string targetName = debugger.playerMovement.TargetObject != null ? debugger.playerMovement.TargetObject.name : "None";
        EditorGUILayout.LabelField($"Target: {targetName}");

        EditorGUILayout.EndVertical();
    }

    private void DrawAbilitiesSection()
    {
        EditorGUILayout.LabelField("Ability Cooldowns", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Handle ENEMY cooldowns
        if (debugger.abilityHolder != null)
        {
            FieldInfo cooldownsField = typeof(EnemyAbilityHolder).GetField("cooldowns", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cooldownsField != null)
            {
                var cooldowns = cooldownsField.GetValue(debugger.abilityHolder) as Dictionary<Ability, float>;
                DrawCooldownList(cooldowns);
            }
        }
        // Handle PLAYER cooldowns
        else if (debugger.playerAbilityHolder != null)
        {
            // Assuming PlayerAbilityHolder also has a 'cooldowns' dictionary. If public, cast directly.
            // Using reflection just to be safe as I cannot see PlayerAbilityHolder code.
            FieldInfo cooldownsField = typeof(PlayerAbilityHolder).GetField("cooldowns", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cooldownsField != null)
            {
                var cooldowns = cooldownsField.GetValue(debugger.playerAbilityHolder) as Dictionary<Ability, float>;
                DrawCooldownList(cooldowns);
            }
            else
            {
                EditorGUILayout.LabelField("No 'cooldowns' field found in PlayerAbilityHolder.");
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCooldownList(Dictionary<Ability, float> cooldowns)
    {
        if (cooldowns != null && cooldowns.Count > 0)
        {
            foreach (var kvp in cooldowns)
            {
                float timeRemaining = Mathf.Max(0, kvp.Value - Time.time);
                string n = kvp.Key != null ? kvp.Key.abilityName : "?";
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(n, GUILayout.Width(150));
                if (timeRemaining > 0) { GUI.color = new Color(1f, 0.5f, 0f); EditorGUILayout.LabelField($"{timeRemaining:F1}s", EditorStyles.boldLabel); }
                else { GUI.color = Color.green; EditorGUILayout.LabelField("READY", EditorStyles.boldLabel); }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("All Abilities Ready");
        }
    }

    private void DrawNavSection()
    {
        if (debugger.navAgent == null) return;
        EditorGUILayout.LabelField("Navigation", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Speed: {debugger.navAgent.velocity.magnitude:F2}");
        EditorGUILayout.EndVertical();
    }
}