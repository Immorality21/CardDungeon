using System.Collections.Generic;
using Assets.Scripts.Enemies.Behaviors;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Enemies.Editor
{
    /// <summary>
    /// Inspector for <see cref="EnemyBehaviorSO"/>. It exists because the default one draws every
    /// field on every action — a Heal showing a damage Multiplier and a magic slot, an Attack showing
    /// a debuff Duration — which makes an authored behaviour unreadable at a glance and invites
    /// setting numbers that do nothing.
    ///
    /// <para>Each row draws only what its <see cref="EnemyActionKind"/> uses, and the header lays the
    /// list out in the order the planner will actually resolve it — tiers highest first, with each
    /// entry's gate, weight and condition count — so a priority mistake is visible while editing.
    /// For what a behaviour is *worth*, the analyzer's Enemies tab has the offense multiplier.</para>
    /// </summary>
    [CustomEditor(typeof(EnemyBehaviorSO))]
    public class EnemyBehaviorSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("DisplayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Archetype"));

            EditorGUILayout.Space();
            DrawSelectionSummary();

            EditorGUILayout.Space();
            DrawActions(serializedObject.FindProperty("Actions"));

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>How the planner will read this list, tier by tier, in resolution order.</summary>
        private void DrawSelectionSummary()
        {
            var behavior = (EnemyBehaviorSO)target;
            if (behavior.Actions == null || behavior.Actions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No actions: this enemy will fall back to a plain attack every turn.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Resolution order", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "A telegraph in flight always delivers. Otherwise: gates roll, the highest eligible "
                + "Priority takes the turn, and Weight picks between ties inside it.",
                MessageType.None);

            // Group by priority, highest first, so the tiers read the way they resolve.
            var tiers = new List<int>();
            foreach (var entry in behavior.Actions)
            {
                if (entry != null && !tiers.Contains(entry.Priority))
                {
                    tiers.Add(entry.Priority);
                }
            }
            tiers.Sort();
            tiers.Reverse();

            foreach (int tier in tiers)
            {
                EditorGUILayout.LabelField($"  Priority {tier}", EditorStyles.miniBoldLabel);
                foreach (var entry in behavior.Actions)
                {
                    if (entry == null || entry.Priority != tier)
                    {
                        continue;
                    }
                    string gate = entry.ChanceGate > 0f
                        ? $"{entry.ChanceGate:0%} gate"
                        : "always considered";
                    string conditions = entry.Conditions != null && entry.Conditions.Count > 0
                        ? $", {entry.Conditions.Count} condition(s)"
                        : "";
                    EditorGUILayout.LabelField(
                        $"      {Describe(entry)}  —  weight {entry.Weight:0.##}, {gate}{conditions}");
                }
            }

            if (BehaviorWarning(behavior, out string warning))
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
        }

        /// <summary>
        /// Authoring mistakes worth catching in the inspector rather than in the analyzer, because
        /// they make an action dead rather than merely mistuned.
        /// </summary>
        private static bool BehaviorWarning(EnemyBehaviorSO behavior, out string warning)
        {
            warning = null;

            // An ungated entry alone in the top tier takes every turn, so nothing below it can run.
            int top = int.MinValue;
            foreach (var entry in behavior.Actions)
            {
                if (entry != null && entry.Priority > top)
                {
                    top = entry.Priority;
                }
            }

            bool ungatedUnconditionalAtTop = false;
            foreach (var entry in behavior.Actions)
            {
                if (entry == null || entry.Priority != top)
                {
                    continue;
                }
                bool unconditional = entry.Conditions == null || entry.Conditions.Count == 0;
                if (entry.ChanceGate <= 0f && unconditional)
                {
                    ungatedUnconditionalAtTop = true;
                }
            }

            int below = 0;
            foreach (var entry in behavior.Actions)
            {
                if (entry != null && entry.Priority < top)
                {
                    below++;
                }
            }

            if (ungatedUnconditionalAtTop && below > 0)
            {
                warning = $"An action at Priority {top} has no gate and no conditions, so it takes "
                        + $"every turn and the {below} action(s) below it can never run. Give it a "
                        + "ChanceGate or a condition, or move it down to compete on Weight.";
                return true;
            }

            foreach (var entry in behavior.Actions)
            {
                if (entry != null && entry.Telegraphed && !entry.CanTelegraph)
                {
                    warning = $"'{Describe(entry)}' is marked Telegraphed, but only HeavyAttack and "
                            + "AoeAttack can wind up. The flag is ignored.";
                    return true;
                }
            }

            return false;
        }

        private static string Describe(EnemyActionEntry entry)
        {
            string name = string.IsNullOrEmpty(entry.Label) ? entry.Kind.ToString() : entry.Label;
            return entry.IsTelegraphed ? name + " (telegraphed)" : name;
        }

        private void DrawActions(SerializedProperty actions)
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            for (int i = 0; i < actions.arraySize; i++)
            {
                var element = actions.GetArrayElementAtIndex(i);
                DrawAction(element, i, actions);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Add action"))
            {
                actions.arraySize++;
            }
        }

        private void DrawAction(SerializedProperty entry, int index, SerializedProperty actions)
        {
            var kind = entry.FindPropertyRelative("Kind");
            var label = entry.FindPropertyRelative("Label");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            string title = string.IsNullOrEmpty(label.stringValue)
                ? ((EnemyActionKind)kind.enumValueIndex).ToString()
                : label.stringValue;
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, $"{index}.  {title}", true);

            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)) && index > 0)
            {
                actions.MoveArrayElement(index, index - 1);
            }
            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24))
                && index < actions.arraySize - 1)
            {
                actions.MoveArrayElement(index, index + 1);
            }
            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24)))
            {
                actions.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (!entry.isExpanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.PropertyField(label);
            EditorGUILayout.PropertyField(kind);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Priority"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Weight"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("ChanceGate"));

            // Only the fields this kind actually reads. Everything else would be a number that
            // silently does nothing, which is how a behaviour ends up mis-authored.
            var actionKind = (EnemyActionKind)kind.enumValueIndex;
            switch (actionKind)
            {
                case EnemyActionKind.Attack:
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Multiplier"));
                    break;

                case EnemyActionKind.HeavyAttack:
                case EnemyActionKind.AoeAttack:
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Multiplier"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Telegraphed"));
                    break;

                case EnemyActionKind.Heal:
                    EditorGUILayout.PropertyField(
                        entry.FindPropertyRelative("Power"), new GUIContent("Heal amount"));
                    break;

                case EnemyActionKind.Debuff:
                    EditorGUILayout.PropertyField(
                        entry.FindPropertyRelative("Power"), new GUIContent("Debuff amount"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Duration"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("TargetStat"));
                    break;

                case EnemyActionKind.CastMagic:
                    EditorGUILayout.PropertyField(
                        entry.FindPropertyRelative("Magic"),
                        new GUIContent("Magic", "Leave empty to draw from this enemy's own Draw list."));
                    if (entry.FindPropertyRelative("Magic").objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(
                            "Picks from the enemy's Spells, weighted by each entry's CastWeight - "
                            + "so what it throws is what you can steal from it.",
                            MessageType.None);
                    }
                    break;
            }

            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Conditions"), true);
            EditorGUILayout.EndVertical();
        }
    }
}
