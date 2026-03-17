#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Ability))]
[CanEditMultipleObjects] 
public class AbilityEditor : Editor
{
    private bool showCore = true;
    private bool showTargeting = true;
    private bool showCasting = false;
    private bool showCombo = false; 
    private bool showPayload = false;
    private bool showVisuals = false;
    private bool showEffects = true;
    private bool showAI = true; // Now defaults to TRUE so you don't miss the AI properties!

    private readonly string[] coreProps = { "abilityName", "displayName", "rank", "icon", "description", "cooldown", "manaCost" };
    private readonly string[] targetingProps = { "abilityType", "locksPlayerActivity", "requiresWeaponType", "requiredWeaponCategories", "range", "canHitCaster" };
    private readonly string[] castingProps = { "showCastBar", "canMoveWhileCasting", "castTime", "triggersGlobalCooldown", "telegraphDuration", "telegraphAnimationTrigger", "attackStyleIndex", "overrideTriggerName", "movementLockDuration", "randomizeAttackStyle", "maxRandomVariants" };
    private readonly string[] comboProps = { "nextComboLink", "comboWindow", "bypassGcdOnCombo" }; 
    private readonly string[] payloadProps = { "playerProjectilePrefab", "enemyProjectilePrefab", "projectileSpawnDelay", "useCoroutineForProjectiles", "projectileCount", "burstDelay", "spreadAngle", "attackBoxSize", "attackBoxCenter", "hitboxOpenDelay", "hitboxCloseDelay", "aoeRadius", "placementPrefab", "manaDrain", "tickRate" };
    private readonly string[] visualProps = { "targetingReticleOverride", "castingVFX", "castingVFXAnchor", "castingVFXPositionOffset", "castingVFXRotationOffset", "attachCastingVFX", "castVFX", "castVFXAnchor", "castVFXPositionOffset", "castVFXRotationOffset", "attachCastVFX", "castVFXDelay", "styleVFXOverrides", "hitVFX", "hitVFXPositionOffset", "hitVFXRotationOffset", "windupSound", "castSound", "impactSound", "screenShakeIntensity", "screenShakeDuration" };
    
    // Purged the legacy string array
    private readonly string[] effectProps = { "onCastEffects", "friendlyEffects", "hostileEffects" };
    private readonly string[] aiProps = { "priority", "isAreaEffect", "usageType", "enemyTelegraphPrefab", "isMajorTacticalThreat" };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.Space(5);

        DrawCategory("Core Identity", ref showCore, coreProps);
        DrawCategory("Targeting & Requirements", ref showTargeting, targetingProps);
        DrawCategory("Casting & Animation", ref showCasting, castingProps);
        DrawCategory("Manual Combo System", ref showCombo, comboProps); 
        DrawCategory("Payload Mechanics", ref showPayload, payloadProps);
        DrawCategory("Visuals, Audio & Game Feel", ref showVisuals, visualProps);
        DrawCategory("Effect Pipelines", ref showEffects, effectProps);
        DrawCategory("AI & Tactics", ref showAI, aiProps);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCategory(string title, ref bool isExpanded, string[] properties)
    {
        EditorGUILayout.BeginVertical("box");
        GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
        foldoutStyle.fontStyle = FontStyle.Bold;
        
        isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, foldoutStyle);
        if (isExpanded)
        {
            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;
            foreach (string propName in properties)
            {
                SerializedProperty prop = serializedObject.FindProperty(propName);
                if (prop != null) EditorGUILayout.PropertyField(prop, true);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(3);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    [MenuItem("CONTEXT/Ability/Add On Cast Effect/Sequence Effect")] private static void AddOnCastSequenceEffect(MenuCommand command) { Ability a = (Ability)command.context; a.onCastEffects.Add(new SequenceEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add On Cast Effect/Heal Effect")] private static void AddOnCastHealEffect(MenuCommand command) { Ability a = (Ability)command.context; a.onCastEffects.Add(new HealEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add On Cast Effect/Damage Effect")] private static void AddOnCastDamageEffect(MenuCommand command) { Ability a = (Ability)command.context; a.onCastEffects.Add(new DamageEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add On Cast Effect/Mana Effect")] private static void AddOnCastManaEffect(MenuCommand command) { Ability a = (Ability)command.context; a.onCastEffects.Add(new ManaEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add On Cast Effect/Apply Status Effect")] private static void AddOnCastStatusEffect(MenuCommand command) { Ability a = (Ability)command.context; a.onCastEffects.Add(new ApplyStatusEffect()); EditorUtility.SetDirty(a); }
    
    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Heal Effect")] private static void AddFriendlyHealEffect(MenuCommand command) { Ability a = (Ability)command.context; a.friendlyEffects.Add(new HealEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Damage Effect")] private static void AddFriendlyDamageEffect(MenuCommand command) { Ability a = (Ability)command.context; a.friendlyEffects.Add(new DamageEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Mana Effect")] private static void AddFriendlyManaEffect(MenuCommand command) { Ability a = (Ability)command.context; a.friendlyEffects.Add(new ManaEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Apply Status Effect")] private static void AddFriendlyStatusEffect(MenuCommand command) { Ability a = (Ability)command.context; a.friendlyEffects.Add(new ApplyStatusEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Sequence Effect")] private static void AddFriendlySequenceEffect(MenuCommand command) { Ability a = (Ability)command.context; a.friendlyEffects.Add(new SequenceEffect()); EditorUtility.SetDirty(a); }
    
    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Damage Effect")] private static void AddHostileDamageEffect(MenuCommand command) { Ability a = (Ability)command.context; a.hostileEffects.Add(new DamageEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Heal Effect")] private static void AddHostileHealEffect(MenuCommand command) { Ability a = (Ability)command.context; a.hostileEffects.Add(new HealEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Mana Effect")] private static void AddHostileManaEffect(MenuCommand command) { Ability a = (Ability)command.context; a.hostileEffects.Add(new ManaEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Apply Status Effect")] private static void AddHostileStatusEffect(MenuCommand command) { Ability a = (Ability)command.context; a.hostileEffects.Add(new ApplyStatusEffect()); EditorUtility.SetDirty(a); }
    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Sequence Effect")] private static void AddHostileSequenceEffect(MenuCommand command) { Ability a = (Ability)command.context; a.hostileEffects.Add(new SequenceEffect()); EditorUtility.SetDirty(a); }
}
#endif