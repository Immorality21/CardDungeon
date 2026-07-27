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
            int lines = 2; // EffectType + Power
            if (effectType == SpellEffectType.Damage)
            {
                lines = 3; // + DamageType
            }
            else if (effectType == SpellEffectType.Buff || effectType == SpellEffectType.Debuff)
            {
                lines = 4; // + BuffType + Duration
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
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("Key"));
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
