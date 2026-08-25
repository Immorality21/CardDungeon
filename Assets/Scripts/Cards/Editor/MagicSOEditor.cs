using Assets.Scripts.Cards;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MagicSO))]
public class MagicSOEditor : Editor
{
    private ReorderableList _effectsList;

    private void OnEnable()
    {
        var effectsProp = serializedObject.FindProperty("Effects");
        _effectsList = new ReorderableList(serializedObject, effectsProp, true, true, true, true);

        _effectsList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Effects");
        };

        _effectsList.elementHeightCallback = index =>
        {
            var element = effectsProp.GetArrayElementAtIndex(index);
            var effectType = (SpellEffectType)element.FindPropertyRelative("EffectType").enumValueIndex;
            int lines = 4; // EffectType + Power + ScalingStat + UnlockLevel
            if (effectType == SpellEffectType.Damage)
            {
                lines = 6; // + PowerMode + DamageType
            }
            else if (effectType == SpellEffectType.Heal)
            {
                lines = 5; // + PowerMode
            }
            else if (effectType == SpellEffectType.Buff || effectType == SpellEffectType.Debuff)
            {
                lines = 6; // + BuffType + Duration
            }
            else if (effectType == SpellEffectType.HealthCost)
            {
                lines = 4; // EffectType + Power + PowerMode + UnlockLevel (no scaling stat)
            }
            return lines * (EditorGUIUtility.singleLineHeight + 2) + 4;
        };

        _effectsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = effectsProp.GetArrayElementAtIndex(index);
            rect.y += 2;
            float lineHeight = EditorGUIUtility.singleLineHeight + 2;

            var effectTypeProp = element.FindPropertyRelative("EffectType");
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                effectTypeProp);
            rect.y += lineHeight;

            var effectType = (SpellEffectType)effectTypeProp.enumValueIndex;

            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative("Power"));
            rect.y += lineHeight;

            // How Power is read. Sits directly under Power because it changes what the number
            // means: 10 is 10 damage in BasePower/Flat and 10% of a health bar in PercentOfMaxHealth.
            // Buff/Debuff magnitudes are stat deltas and ignore the mode, so it is not drawn for them.
            bool honoursPowerMode = effectType == SpellEffectType.Damage
                                 || effectType == SpellEffectType.Heal
                                 || effectType == SpellEffectType.HealthCost;
            if (honoursPowerMode)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("PowerMode"),
                    new GUIContent("Power Mode", "BasePower adds the caster's Scaling Stat. Flat uses "
                        + "Power as authored. PercentOfMaxHealth reads Power as a percentage of the "
                        + "max health of the unit the effect lands on (the caster, for a health cost), "
                        + "rounded down with a floor of 1, and takes no upgrade bonus."));
                rect.y += lineHeight;
            }

            // Which caster stat is added to Power. Sits next to Power because the two are read
            // together: Power is the floor, the stat is what a good caster brings. A health cost has
            // no caster contribution at all, so the field would be a lie there.
            if (effectType != SpellEffectType.HealthCost)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("ScalingStat"),
                    new GUIContent("Scaling Stat", "Caster stat added to Power. Damage/heals add it in "
                        + "full; buffs and debuffs add a quarter of it, since their Power is a flat stat "
                        + "delta and full scaling would dwarf the stat being buffed."));
                rect.y += lineHeight;
            }

            if (effectType == SpellEffectType.Damage)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("DamageType"));
                rect.y += lineHeight;
            }
            else if (effectType == SpellEffectType.Buff || effectType == SpellEffectType.Debuff)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("BuffType"));
                rect.y += lineHeight;

                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("Duration"));
                rect.y += lineHeight;
            }

            // Upgrade level at which this effect unlocks (0 = always active).
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative("UnlockLevel"));
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Auto-generate a stable Key from the asset's name, then show it read-only.
        // Save data + catalog lookups key off this, so it must never change once set — it is
        // generated only while empty, and renaming the asset later does not touch it.
        var keyProp = serializedObject.FindProperty("Key");
        if (string.IsNullOrEmpty(keyProp.stringValue))
        {
            keyProp.stringValue = $"{target.name}-{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(keyProp, new GUIContent("Key (auto)"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("DisplayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Rarity"));

        EditorGUILayout.Space(8);
        _effectsList.DoLayoutList();

        EditorGUILayout.Space(8);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Tags"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TagDuration"));

        serializedObject.ApplyModifiedProperties();
    }
}
