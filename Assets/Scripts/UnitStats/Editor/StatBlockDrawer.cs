using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.UnitStats.Editor
{
    /// <summary>
    /// Draws a <see cref="StatBlock"/> as one labelled row per stat, so authoring a hero or enemy
    /// feels like the four int fields it replaced rather than a raw list of {Type, Amount} pairs.
    ///
    /// <para>Without this the default inspector shows a reorderable list whose new entries default to
    /// <see cref="StatType.None"/> — easy to leave unset, easy to duplicate, and it hides which stats
    /// a unit is missing. Here every stat in <see cref="StatCatalog.Types"/> always has a row, so a stat
    /// added to the enum shows up in every inspector immediately.</para>
    ///
    /// <para>The block stays <b>sparse</b> on disk: a stat with no entry shows a <c>+</c> button
    /// instead of a field and writes nothing until that button is clicked. That keeps assets free of
    /// noise and means a new stat does not need back-filling across existing content.</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(StatBlock))]
    public class StatBlockDrawer : PropertyDrawer
    {
        private const float RowSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float row = EditorGUIUtility.singleLineHeight + RowSpacing;
            if (!property.isExpanded)
            {
                return row;
            }

            // header + one row per stat + the tidy-up button
            return row * (StatCatalog.Types.Count + 2);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var values = property.FindPropertyRelative("Values");
            float rowHeight = EditorGUIUtility.singleLineHeight;
            var rect = new Rect(position.x, position.y, position.width, rowHeight);

            string summary = values != null ? SummaryOf(values) : "(unavailable)";
            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded,
                new GUIContent(label.text + "   " + summary, label.tooltip), true);

            if (!property.isExpanded || values == null)
            {
                return;
            }

            EditorGUI.indentLevel++;
            foreach (var stat in StatCatalog.Types)
            {
                rect.y += rowHeight + RowSpacing;
                DrawStatRow(rect, values, stat);
            }

            rect.y += rowHeight + RowSpacing;
            var buttonRect = EditorGUI.IndentedRect(rect);
            buttonRect.width = Mathf.Min(buttonRect.width, 180f);
            if (GUI.Button(buttonRect, "Remove zero / unset entries", EditorStyles.miniButton))
            {
                Compact(values);
            }
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// One stat's row. A stat with an entry gets a normal int field; clearing it back to 0 leaves
        /// the entry in place rather than deleting it mid-edit, and the tidy-up button is how zeroes
        /// get cleaned away.
        ///
        /// <para>A stat with <b>no</b> entry gets a <b>button</b>, not an int field. An int field that
        /// turns into a <see cref="EditorGUI.PropertyField"/> the moment the entry materialises loses
        /// keyboard focus after the first keystroke: typing "12" into an empty Intelligence row
        /// stored 1 and swallowed the 2, and because a zeroed row is deliberately kept, the stray 1
        /// then survived the tidy-up button too. Authoring a caster is exactly the workflow that hit
        /// it. The button creates the entry at 0 in one click, after which the field is stable.</para>
        /// </summary>
        private static void DrawStatRow(Rect rect, SerializedProperty values, StatType stat)
        {
            var definition = StatCatalog.Of(stat);
            var amount = FindAmount(values, stat);
            var content = new GUIContent(definition.DisplayName, definition.Description);

            if (amount != null)
            {
                EditorGUI.PropertyField(rect, amount, content);
                return;
            }

            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            EditorGUI.LabelField(labelRect, content);

            var buttonRect = new Rect(labelRect.xMax, rect.y, Mathf.Min(24f, rect.width - labelRect.width), rect.height);
            var buttonLabel = new GUIContent("+", "Not set, so it reads as 0. Click to add a "
                + definition.DisplayName + " entry.");
            if (!GUI.Button(buttonRect, buttonLabel, EditorStyles.miniButton))
            {
                return;
            }

            values.InsertArrayElementAtIndex(values.arraySize);
            var added = values.GetArrayElementAtIndex(values.arraySize - 1);
            // intValue, not enumValueIndex: the latter is a position in the enum's name list and
            // only matches the stored value while StatType stays contiguous from zero.
            added.FindPropertyRelative("Type").intValue = (int)stat;
            added.FindPropertyRelative("Amount").intValue = 0;
        }

        /// <summary>
        /// The <c>Amount</c> property for a stat, collapsing duplicates into the first entry first.
        /// The runtime indexer <b>sums</b> duplicates, so editing only one of them would show a
        /// different number from the one the game uses.
        /// </summary>
        private static SerializedProperty FindAmount(SerializedProperty values, StatType stat)
        {
            int keep = -1;
            int total = 0;
            for (int i = values.arraySize - 1; i >= 0; i--)
            {
                var entry = values.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("Type").intValue != (int)stat)
                {
                    continue;
                }
                total += entry.FindPropertyRelative("Amount").intValue;
                if (keep >= 0)
                {
                    values.DeleteArrayElementAtIndex(keep);
                }
                keep = i;
            }

            if (keep < 0)
            {
                return null;
            }

            var amount = values.GetArrayElementAtIndex(keep).FindPropertyRelative("Amount");
            if (amount.intValue != total)
            {
                amount.intValue = total;
            }
            return amount;
        }

        private static void Compact(SerializedProperty values)
        {
            for (int i = values.arraySize - 1; i >= 0; i--)
            {
                var entry = values.GetArrayElementAtIndex(i);
                bool unset = entry.FindPropertyRelative("Type").intValue == (int)StatType.None;
                bool zero = entry.FindPropertyRelative("Amount").intValue == 0;
                if (unset || zero)
                {
                    values.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private static string SummaryOf(SerializedProperty values)
        {
            var parts = new List<string>();
            foreach (var stat in StatCatalog.Types)
            {
                var amount = FindAmountReadOnly(values, stat);
                if (amount != 0)
                {
                    parts.Add(StatCatalog.ShortName(stat) + " " + amount);
                }
            }
            return parts.Count > 0 ? string.Join("  ", parts.ToArray()) : "(no stats)";
        }

        /// <summary>Summed value for a stat without mutating the array — safe to call while drawing a label.</summary>
        private static int FindAmountReadOnly(SerializedProperty values, StatType stat)
        {
            int total = 0;
            for (int i = 0; i < values.arraySize; i++)
            {
                var entry = values.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("Type").intValue == (int)stat)
                {
                    total += entry.FindPropertyRelative("Amount").intValue;
                }
            }
            return total;
        }
    }
}
