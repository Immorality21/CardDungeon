using Assets.Scripts.UnitStats;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Balance.Editor
{
    /// <summary>
    /// Shared drawing for the balance window: the severity palette, table cells, and the editable
    /// cells that write straight back to the asset. Editing goes through <see cref="SerializedObject"/>
    /// rather than direct field assignment so every change is undoable and marks the asset dirty the
    /// way the inspector does.
    /// </summary>
    public static class BalanceGui
    {
        public const float RowHeight = 18f;

        private static GUIStyle _cellStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _wrapStyle;
        private static GUIStyle _wrapBoldStyle;
        private static GUIStyle _wrapMiniStyle;

        public static GUIStyle CellStyle
        {
            get
            {
                if (_cellStyle == null)
                {
                    _cellStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(4, 4, 0, 0),
                        clipping = TextClipping.Clip
                    };
                }
                return _cellStyle;
            }
        }

        public static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(4, 4, 0, 0),
                        clipping = TextClipping.Clip
                    };
                }
                return _headerStyle;
            }
        }

        public static GUIStyle TitleStyle
        {
            get
            {
                if (_titleStyle == null)
                {
                    _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                }
                return _titleStyle;
            }
        }

        public static GUIStyle WrapStyle
        {
            get
            {
                if (_wrapStyle == null)
                {
                    _wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
                }
                return _wrapStyle;
            }
        }

        public static GUIStyle WrapBoldStyle
        {
            get
            {
                if (_wrapBoldStyle == null)
                {
                    _wrapBoldStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
                }
                return _wrapBoldStyle;
            }
        }

        public static GUIStyle WrapMiniStyle
        {
            get
            {
                if (_wrapMiniStyle == null)
                {
                    _wrapMiniStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                }
                return _wrapMiniStyle;
            }
        }

        /// <summary>
        /// Prose that wraps to the panel width. Use this and never
        /// <see cref="EditorGUILayout.LabelField(string, GUIStyle, GUILayoutOption[])"/> for sentences:
        /// LabelField reserves exactly one line of height, so a word-wrapping style silently clips
        /// instead of wrapping. <see cref="GUILayout.Label(string, GUIStyle, GUILayoutOption[])"/> sizes
        /// itself to the wrapped content.
        /// </summary>
        public static void Paragraph(string text, GUIStyle style = null, Color? contentColor = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var previous = GUI.contentColor;
            if (contentColor.HasValue)
            {
                GUI.contentColor = contentColor.Value;
            }

            // ExpandWidth matters inside a horizontal row: without it the label is squeezed to its
            // minimum width beside a fixed-size sibling and wraps into a sliver instead of filling
            // the remaining space.
            GUILayout.Label(text, style ?? WrapStyle, GUILayout.ExpandWidth(true));
            GUI.contentColor = previous;
        }

        /// <summary>Text colour for a severity — readable against both editor skins.</summary>
        public static Color TextColorFor(BalanceSeverity severity)
        {
            switch (severity)
            {
                case BalanceSeverity.Critical:
                    return new Color(1f, 0.45f, 0.42f);
                case BalanceSeverity.Warning:
                    return new Color(1f, 0.78f, 0.28f);
                case BalanceSeverity.Info:
                    return new Color(0.6f, 0.8f, 1f);
                default:
                    return new Color(0.55f, 0.85f, 0.6f);
            }
        }

        /// <summary>Background wash behind an out-of-band cell. Ok cells stay unpainted.</summary>
        public static Color BackgroundColorFor(BalanceSeverity severity)
        {
            switch (severity)
            {
                case BalanceSeverity.Critical:
                    return new Color(0.55f, 0.12f, 0.12f, 0.42f);
                case BalanceSeverity.Warning:
                    return new Color(0.55f, 0.4f, 0.05f, 0.34f);
                case BalanceSeverity.Info:
                    return new Color(0.15f, 0.3f, 0.5f, 0.26f);
                default:
                    return Color.clear;
            }
        }

        public static string SymbolFor(BalanceSeverity severity)
        {
            switch (severity)
            {
                case BalanceSeverity.Critical:
                    return "!!";
                case BalanceSeverity.Warning:
                    return "!";
                case BalanceSeverity.Info:
                    return "i";
                default:
                    return "ok";
            }
        }

        /// <summary>A read-only table cell, tinted by severity.</summary>
        public static void Cell(string text, float width, BalanceSeverity severity = BalanceSeverity.Ok, string tooltip = null)
        {
            var rect = GUILayoutUtility.GetRect(width, RowHeight, GUILayout.Width(width), GUILayout.Height(RowHeight));
            var background = BackgroundColorFor(severity);
            if (background.a > 0f)
            {
                EditorGUI.DrawRect(rect, background);
            }

            var previous = GUI.contentColor;
            if (severity != BalanceSeverity.Ok)
            {
                GUI.contentColor = TextColorFor(severity);
            }
            EditorGUI.LabelField(rect, new GUIContent(text, TooltipForClipped(text, width, tooltip)), CellStyle);
            GUI.contentColor = previous;
        }

        /// <summary>
        /// Table cells are fixed-width and clip, so a value wider than its column would be unreadable
        /// with no way to see the rest. When that happens the full text is folded into the tooltip
        /// (ahead of any explanatory tooltip) so hovering always recovers it.
        /// </summary>
        private static string TooltipForClipped(string text, float width, string tooltip)
        {
            if (string.IsNullOrEmpty(text) || CellStyle.CalcSize(new GUIContent(text)).x <= width)
            {
                return tooltip;
            }

            return string.IsNullOrEmpty(tooltip) ? text : text + "\n\n" + tooltip;
        }

        public static void HeaderCell(string text, float width, string tooltip = null)
        {
            var rect = GUILayoutUtility.GetRect(width, RowHeight, GUILayout.Width(width), GUILayout.Height(RowHeight));
            EditorGUI.LabelField(rect, new GUIContent(text, tooltip), HeaderStyle);
        }

        /// <summary>
        /// An editable cell bound to a serialized field. Returns true when the value changed, so the
        /// caller can re-run the analysis and show the consequence immediately.
        /// </summary>
        public static bool EditableCell(
            SerializedObject serialized,
            string propertyPath,
            float width,
            BalanceSeverity severity = BalanceSeverity.Ok,
            string tooltip = null)
        {
            var property = serialized != null ? serialized.FindProperty(propertyPath) : null;
            if (property == null)
            {
                Cell("—", width);
                return false;
            }

            return EditableCell(property, width, severity, tooltip);
        }

        /// <summary>
        /// The <c>StatBlock.Values</c> list under <paramref name="blockPath"/>, from either a whole
        /// asset or a single array element (a <c>SphereGridNode</c> entry, say). Null when the
        /// path does not resolve, which is the failure mode string-based property paths have.
        /// </summary>
        private static SerializedProperty StatValues(SerializedObject serialized, SerializedProperty element, string blockPath)
        {
            if (element != null)
            {
                return element.FindPropertyRelative(blockPath + ".Values");
            }
            return serialized != null ? serialized.FindProperty(blockPath + ".Values") : null;
        }

        /// <summary>
        /// Finds the <c>Amount</c> property for one stat inside a block's list, or null when the
        /// block has no entry for it yet (the block is sparse).
        ///
        /// <para>Compares <c>intValue</c>, not <c>enumValueIndex</c>: the latter is the position in
        /// the enum's name list, which only equals the stored value while <see cref="StatType"/>'s
        /// members stay contiguous from zero. One explicitly-numbered member would silently break it.</para>
        /// </summary>
        private static SerializedProperty FindStatAmount(SerializedProperty values, StatType stat)
        {
            if (values == null)
            {
                return null;
            }

            // A block may legitimately hold duplicate entries for one stat - the runtime indexer
            // *sums* them. Editing only the first would show 5 where the game fights with 12, so
            // collapse them here first: the cell then always edits the value actually in play.
            int firstIndex = -1;
            int total = 0;
            for (int i = values.arraySize - 1; i >= 0; i--)
            {
                var entry = values.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("Type").intValue != (int)stat)
                {
                    continue;
                }
                total += entry.FindPropertyRelative("Amount").intValue;
                if (firstIndex >= 0)
                {
                    values.DeleteArrayElementAtIndex(firstIndex);
                }
                firstIndex = i;
            }

            if (firstIndex < 0)
            {
                return null;
            }

            var amount = values.GetArrayElementAtIndex(firstIndex).FindPropertyRelative("Amount");
            if (amount.intValue != total)
            {
                amount.intValue = total;
            }
            return amount;
        }

        /// <summary>Current value of one stat in a serialized block; 0 when it has no entry.</summary>
        public static int StatEntryValue(SerializedProperty element, string blockPath, StatType stat)
        {
            var amount = FindStatAmount(StatValues(null, element, blockPath), stat);
            return amount != null ? amount.intValue : 0;
        }

        /// <summary>
        /// An editable cell bound to one stat inside a <c>StatBlock</c> field. The block is a sparse
        /// list, so the entry for a stat may not exist yet - in that case this shows a zero and
        /// materialises the entry only once the designer types a non-zero value, which is what makes
        /// "type a number into the empty Luck column" work without littering every asset with zeroes.
        /// </summary>
        public static bool EditableStatCell(
            SerializedObject serialized,
            string blockPath,
            StatType stat,
            float width,
            BalanceSeverity severity = BalanceSeverity.Ok,
            string tooltip = null)
        {
            return EditableStatCell(serialized, null, blockPath, stat, width, severity, tooltip);
        }

        /// <summary>As above, for a block nested inside a serialized array element.</summary>
        public static bool EditableStatCell(
            SerializedProperty element,
            string blockPath,
            StatType stat,
            float width,
            BalanceSeverity severity = BalanceSeverity.Ok,
            string tooltip = null)
        {
            return EditableStatCell(null, element, blockPath, stat, width, severity, tooltip);
        }

        private static bool EditableStatCell(
            SerializedObject serialized,
            SerializedProperty element,
            string blockPath,
            StatType stat,
            float width,
            BalanceSeverity severity,
            string tooltip)
        {
            var values = StatValues(serialized, element, blockPath);
            if (values == null)
            {
                Cell("—", width);
                return false;
            }

            var amount = FindStatAmount(values, stat);
            if (amount != null)
            {
                return EditableCell(amount, width, severity, tooltip);
            }

            // The block is sparse, so this stat has no entry. Offer a button rather than an int
            // field: typing into a field that becomes a PropertyField on the next repaint loses
            // keyboard focus after the first keystroke, which is exactly the workflow this is for.
            var rect = GUILayoutUtility.GetRect(width, RowHeight, GUILayout.Width(width), GUILayout.Height(RowHeight));
            var background = BackgroundColorFor(severity);
            if (background.a > 0f)
            {
                EditorGUI.DrawRect(rect, background);
            }

            var label = new GUIContent("+", tooltip ?? "Not set. Click to give this unit a " + stat + " entry.");
            if (!GUI.Button(rect, label, EditorStyles.miniButton))
            {
                return false;
            }

            values.InsertArrayElementAtIndex(values.arraySize);
            var added = values.GetArrayElementAtIndex(values.arraySize - 1);
            added.FindPropertyRelative("Type").intValue = (int)stat;
            added.FindPropertyRelative("Amount").intValue = 0;
            return true;
        }

        public static bool EditableCell(
            SerializedProperty property,
            float width,
            BalanceSeverity severity = BalanceSeverity.Ok,
            string tooltip = null)
        {
            var rect = GUILayoutUtility.GetRect(width, RowHeight, GUILayout.Width(width), GUILayout.Height(RowHeight));
            var background = BackgroundColorFor(severity);
            if (background.a > 0f)
            {
                EditorGUI.DrawRect(rect, background);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(rect, property, new GUIContent("", tooltip));
            return EditorGUI.EndChangeCheck();
        }

        /// <summary>A clickable name cell that selects the asset in the Project window.</summary>
        public static void AssetCell(Object asset, string label, float width, BalanceSeverity severity = BalanceSeverity.Ok)
        {
            var rect = GUILayoutUtility.GetRect(width, RowHeight, GUILayout.Width(width), GUILayout.Height(RowHeight));
            var background = BackgroundColorFor(severity);
            if (background.a > 0f)
            {
                EditorGUI.DrawRect(rect, background);
            }

            if (asset == null)
            {
                EditorGUI.LabelField(rect, new GUIContent(label, TooltipForClipped(label, width, null)), CellStyle);
                return;
            }

            if (GUI.Button(rect, new GUIContent(label, TooltipForClipped(label, width, $"Select {asset.name}")), CellStyle))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
        }

        /// <summary>A compact severity tally, e.g. "2 critical  5 warning  3 info".</summary>
        public static void SeveritySummary(BalanceReport report)
        {
            if (report == null)
            {
                return;
            }

            DrawChip($"{report.CountOf(BalanceSeverity.Critical)} critical", BalanceSeverity.Critical);
            DrawChip($"{report.CountOf(BalanceSeverity.Warning)} warning", BalanceSeverity.Warning);
            DrawChip($"{report.CountOf(BalanceSeverity.Info)} info", BalanceSeverity.Info);
        }

        public static void DrawChip(string text, BalanceSeverity severity)
        {
            var content = new GUIContent(text);
            float width = EditorStyles.miniLabel.CalcSize(content).x + 12f;
            var rect = GUILayoutUtility.GetRect(width, RowHeight, GUILayout.Width(width), GUILayout.Height(RowHeight));
            EditorGUI.DrawRect(rect, BackgroundColorFor(severity));

            var previous = GUI.contentColor;
            GUI.contentColor = TextColorFor(severity);
            EditorGUI.LabelField(rect, content, EditorStyles.miniLabel);
            GUI.contentColor = previous;
        }

        /// <summary>Formats a possibly-infinite metric without printing "Infinity" in a table.</summary>
        public static string Number(float value, string format = "0.00")
        {
            if (float.IsInfinity(value))
            {
                return "never";
            }
            if (float.IsNaN(value))
            {
                return "—";
            }
            return value.ToString(format);
        }

        public static string Count(int value)
        {
            return value == int.MaxValue ? "never" : value.ToString();
        }

        public static void Separator()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
        }

        public static void SectionHeader(string title, string subtitle = null)
        {
            EditorGUILayout.Space(4f);
            GUILayout.Label(title, TitleStyle);
            Paragraph(subtitle, WrapMiniStyle);
            Separator();
        }
    }
}
