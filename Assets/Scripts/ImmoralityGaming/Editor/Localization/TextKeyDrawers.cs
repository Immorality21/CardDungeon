using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ImmoralityGaming.Localization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImmoralityGaming.Editor.Localization
{
    /// <summary>
    /// Inspector helpers for texts that live in the table: show a key's wording in the default language
    /// as an editable box, writing straight into the table (<see cref="Loc.EditorSet"/>).
    /// </summary>
    public static class TextKeyGUI
    {
        private const float GapAfterField = 2f;

        /// <summary>Lines of text box for a field: names and titles get one, the rest three.</summary>
        public static int LinesFor(string field)
        {
            return field == "name" || field == "title" || field == "label" ? 1 : 3;
        }

        public static float HeightFor(string field)
        {
            return LinesFor(field) * EditorGUIUtility.singleLineHeight + GapAfterField;
        }

        /// <summary>The wording of <paramref name="key"/> as an editable box in <paramref name="rect"/>.</summary>
        public static void TextBox(Rect rect, string label, string key, int lines)
        {
            string current = Loc.GetOrEmpty(key);
            var content = new GUIContent(label, key);
            EditorGUI.BeginChangeCheck();
            string edited;
            if (lines <= 1)
            {
                edited = EditorGUI.TextField(rect, content, current);
            }
            else
            {
                var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
                var boxRect = new Rect(rect.x + EditorGUIUtility.labelWidth + 2f, rect.y,
                    rect.width - EditorGUIUtility.labelWidth - 2f, rect.height - GapAfterField);
                EditorGUI.LabelField(labelRect, content);
                var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                edited = EditorGUI.TextArea(boxRect, current, style);
            }
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(key))
            {
                Loc.EditorSet(key, edited);
            }
        }

        /// <summary>Layout version of <see cref="TextBox"/>, for custom inspectors.</summary>
        public static void TextBoxLayout(string label, string key, int lines)
        {
            Rect rect = EditorGUILayout.GetControlRect(true, lines * EditorGUIUtility.singleLineHeight + GapAfterField);
            TextBox(rect, label, key, lines);
        }

        /// <summary>The texts a <see cref="TextKeysAttribute"/> field derives, one box each, laid out.
        /// For custom inspectors that draw the identity field themselves.</summary>
        public static void DerivedTextsLayout(SerializedProperty identity, string area, params string[] fields)
        {
            string id = IdOf(identity);
            foreach (string field in fields)
            {
                TextBoxLayout(LabelFor(field), Loc.KeyFor(area, id, field), LinesFor(field));
            }
        }

        public static string LabelFor(string field)
        {
            return ObjectNames.NicifyVariableName(field) + " (" + DefaultLanguage + ")";
        }

        public static string DefaultLanguage => Loc.Table.Languages.Count > 0 ? Loc.Table.Languages[0] : "en";

        /// <summary>The id a <see cref="TextKeysAttribute"/> field gives: its string or enum name; an
        /// empty top-level key falls back to the asset name, as save keys do.</summary>
        public static string IdOf(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                int index = property.enumValueIndex;
                return index >= 0 && index < property.enumNames.Length ? property.enumNames[index] : "";
            }
            string value = property.stringValue;
            if (string.IsNullOrEmpty(value) && property.depth == 0 && property.serializedObject.targetObject != null)
            {
                return property.serializedObject.targetObject.name;
            }
            return value;
        }
    }

    [CustomPropertyDrawer(typeof(TextKeysAttribute))]
    public class TextKeysDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + 2f;
            foreach (string field in ((TextKeysAttribute)attribute).Fields)
            {
                height += TextKeyGUI.HeightFor(field);
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var keys = (TextKeysAttribute)attribute;
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, property, label);
            float y = line.yMax + 2f;

            string id = TextKeyGUI.IdOf(property);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = true;
            foreach (string field in keys.Fields)
            {
                float h = TextKeyGUI.HeightFor(field);
                TextKeyGUI.TextBox(new Rect(position.x, y, position.width, h), TextKeyGUI.LabelFor(field),
                    Loc.KeyFor(keys.Area, id, field), TextKeyGUI.LinesFor(field));
                y += h;
            }
            GUI.enabled = wasEnabled;
        }
    }

    [CustomPropertyDrawer(typeof(TextKeyAttribute))]
    public class TextKeyDrawer : PropertyDrawer
    {
        private const int Lines = 2;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + 2f + Lines * EditorGUIUtility.singleLineHeight + 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(line, label.text, "[TextKey] needs a string field");
                return;
            }
            EditorGUI.PropertyField(line, property, label);
            var box = new Rect(position.x, line.yMax + 2f, position.width, Lines * EditorGUIUtility.singleLineHeight + 2f);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(property.stringValue)))
            {
                TextKeyGUI.TextBox(box, TextKeyGUI.DefaultLanguage, property.stringValue, Lines);
            }
        }
    }

    /// <summary>
    /// Finds every text key an asset uses through <see cref="TextKeysAttribute"/> and
    /// <see cref="TextKeyAttribute"/> fields, walking its serialized fields (nested classes and lists
    /// included). Registered with <see cref="LocUsageScanner"/> so the Texts window sees asset texts.
    /// </summary>
    [InitializeOnLoad]
    public static class AssetTextKeys
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static AssetTextKeys()
        {
            LocUsageScanner.Providers.Add(ScanProject);
        }

        private static IEnumerable<LocUsage> ScanProject()
        {
            var usages = new List<LocUsage>();
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is ScriptableObject)
                    {
                        foreach (string key in Collect(asset))
                        {
                            usages.Add(new LocUsage { Key = key, Path = path, Optional = true });
                        }
                    }
                }
            }
            return usages;
        }

        /// <summary>Every text key <paramref name="asset"/> uses.</summary>
        public static List<string> Collect(Object asset)
        {
            var keys = new List<string>();
            Walk(asset, asset.name, true, keys, 0);
            return keys;
        }

        private static void Walk(object target, string assetName, bool topLevel, List<string> keys, int depth)
        {
            if (target == null || depth > 8)
            {
                return;
            }
            for (var type = target.GetType(); type != null && type != typeof(ScriptableObject) && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(Fields | BindingFlags.DeclaredOnly))
                {
                    if (!IsSerialized(field))
                    {
                        continue;
                    }
                    object value = field.GetValue(target);

                    var derived = field.GetCustomAttribute<TextKeysAttribute>();
                    if (derived != null)
                    {
                        string id = value?.ToString() ?? "";
                        if (id.Length == 0 && topLevel)
                        {
                            id = assetName;
                        }
                        foreach (string f in derived.Fields)
                        {
                            keys.Add(Loc.KeyFor(derived.Area, id, f));
                        }
                        continue;
                    }

                    if (field.GetCustomAttribute<TextKeyAttribute>() != null)
                    {
                        if (value is string single)
                        {
                            AddKey(single, keys);
                        }
                        else if (value is IEnumerable<string> many)
                        {
                            foreach (string key in many)
                            {
                                AddKey(key, keys);
                            }
                        }
                        continue;
                    }

                    if (value == null || value is string || value is Object || field.FieldType.IsPrimitive || field.FieldType.IsEnum)
                    {
                        continue;
                    }
                    if (value is IEnumerable list)
                    {
                        foreach (object item in list)
                        {
                            if (item != null && !(item is Object) && IsWalkable(item.GetType()))
                            {
                                Walk(item, assetName, false, keys, depth + 1);
                            }
                        }
                    }
                    else if (IsWalkable(field.FieldType))
                    {
                        Walk(value, assetName, false, keys, depth + 1);
                    }
                }
            }
        }

        private static void AddKey(string key, List<string> keys)
        {
            if (!string.IsNullOrEmpty(key))
            {
                keys.Add(key);
            }
        }

        private static bool IsSerialized(FieldInfo field)
        {
            if (field.IsNotSerialized || field.IsStatic)
            {
                return false;
            }
            return field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
        }

        private static bool IsWalkable(Type type)
        {
            return !type.IsPrimitive && !type.IsEnum && type != typeof(string) &&
                   type.GetCustomAttribute<SerializableAttribute>() != null &&
                   type.Namespace != null && !type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal);
        }
    }
}
