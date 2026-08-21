using Assets.Scripts.Cards;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MagicComboSO))]
public class MagicComboSOEditor : Editor
{
    private ReorderableList _effectsList;

    private void OnEnable()
    {
        var effectsProp = serializedObject.FindProperty("BonusEffects");
        _effectsList = new ReorderableList(serializedObject, effectsProp, true, true, true, true);

        _effectsList.drawHeaderCallback = rect =>
        {
            // No Scaling Stat field here on purpose: EffectResolver runs combo effects with
            // flatPower = true, which uses Power flat. Combo power belongs to the combo,
            // not to whoever happened to land the second tag.
            EditorGUI.LabelField(rect, new GUIContent("Bonus Effects (flat power — not caster-scaled)",
                "Combo effects ignore the caster's stats by design."));
        };

        _effectsList.elementHeightCallback = index =>
        {
            var element = effectsProp.GetArrayElementAtIndex(index);
            var effectType = (SpellEffectType)element.FindPropertyRelative("EffectType").enumValueIndex;
            int lines = 3; // EffectType + Power + UnlockLevel
            if (effectType == SpellEffectType.Damage)
            {
                lines = 4; // + DamageType
            }
            else if (effectType == SpellEffectType.Buff || effectType == SpellEffectType.Debuff)
            {
                lines = 5; // + BuffType + Duration
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
        // Discovery + upgrade data key off this, so it must never change once set — it is
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

        EditorGUILayout.PropertyField(serializedObject.FindProperty("ComboName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("RequiredTags"), true);

        EditorGUILayout.Space(8);
        _effectsList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
}
