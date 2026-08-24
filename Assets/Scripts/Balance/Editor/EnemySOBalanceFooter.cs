using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Balance.Editor
{
    /// <summary>
    /// Adds a derived-numbers footer to the <see cref="EnemySO"/> inspector: what this enemy does to
    /// the reference party, right where the stats are authored. The balance window is where tuning
    /// happens, but the mistakes are made in the inspector, so the consequence belongs here too.
    ///
    /// The footer is deliberately cheap — closed-form only, no simulation — so it can recompute on
    /// every inspector repaint.
    /// </summary>
    [CustomEditor(typeof(EnemySO))]
    public class EnemySOBalanceFooter : UnityEditor.Editor
    {
        private static List<HeroSO> _heroes;
        private static BalanceRulesSO _rules;
        private static PartyBaseline _party;
        private static double _cachedAt = -1d;

        // The party is rebuilt at most a few times a second; hero assets rarely change mid-session.
        private const double CacheSeconds = 2d;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var enemy = target as EnemySO;
            if (enemy == null)
            {
                return;
            }

            var party = GetParty();
            if (party == null || party.Size == 0)
            {
                return;
            }

            var metrics = EnemyMetrics.Compute(enemy, party, _rules);

            EditorGUILayout.Space(8f);
            BalanceGui.SectionHeader("Balance", $"Measured against {party.SourceLabel}.");

            float ceiling = metrics.DangerCeiling(_rules);
            var dangerSeverity = metrics.SoloDangerIndex >= 1f
                ? BalanceSeverity.Critical
                : metrics.SoloDangerIndex > ceiling
                    ? BalanceSeverity.Warning
                    : metrics.SoloDangerIndex < _rules.MinMeaningfulDanger
                        ? BalanceSeverity.Warning
                        : BalanceSeverity.Ok;

            Row("Danger index", BalanceGui.Number(metrics.SoloDangerIndex), dangerSeverity,
                $"Ticks for the party to win divided by ticks for the party to die. Band ceiling {ceiling:0.00}.");

            var ttkSeverity = metrics.PartyTurnsToKill > metrics.TimeToKillCeiling(_rules)
                ? BalanceSeverity.Warning
                : metrics.PartyTurnsToKill < _rules.MinEnemyTimeToKill
                    ? BalanceSeverity.Info
                    : BalanceSeverity.Ok;

            Row("Party turns to kill", BalanceGui.Number(metrics.PartyTurnsToKill, "0.0"), ttkSeverity,
                $"Band {_rules.MinEnemyTimeToKill:0.0}–{metrics.TimeToKillCeiling(_rules):0.0} turns, basic attacks only.");

            Row("Damage per turn", BalanceGui.Number(metrics.EffectiveDamagePerTurn, "0.0"), BalanceSeverity.Ok,
                $"Plain hit {metrics.AverageDamagePerHit:0.0} x archetype cadence {metrics.OffenseMultiplier:0.00}.");

            var killSeverity = metrics.FewestHitsToKillAHero <= 1
                ? BalanceSeverity.Critical
                : metrics.FewestHitsToKillAHero < _rules.MinHitsToKillHero
                    ? BalanceSeverity.Warning
                    : BalanceSeverity.Ok;

            Row("Kills a hero in",
                metrics.FewestHitsToKillAHero == int.MaxValue
                    ? "never"
                    : $"{metrics.FewestHitsToKillAHero} hit(s) — {metrics.FastestKillTarget}",
                killSeverity,
                $"Target is at least {_rules.MinHitsToKillHero} hits.");

            if (metrics.ActionShareVsParty >= 1.5f)
            {
                Row("Acts", $"{metrics.ActionShareVsParty:0.0}x as often as a hero", BalanceSeverity.Info,
                    "Agility is a threat multiplier the raw stats do not show.");
            }

            if (metrics.ResistanceCount == 0)
            {
                Row("Resistances", "none", BalanceSeverity.Info,
                    "Without a resistance this enemy makes every damage type identical, so element choice "
                    + "never matters against it.");
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Balance Analyzer", GUILayout.Width(170f)))
            {
                BalanceWindow.OpenFor(enemy);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void Row(string label, string value, BalanceSeverity severity, string tooltip)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(150f));
            BalanceGui.Cell(value, 260f, severity, tooltip);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static PartyBaseline GetParty()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_party != null && now - _cachedAt < CacheSeconds)
            {
                return _party;
            }

            _rules = BalanceAssetCollector.LoadOrCreateRules(false);
            _heroes = BalanceAssetCollector.FindAll<HeroSO>();
            _party = PartyBaseline.Build(_heroes, _rules.ReferenceHeroXp);
            _party.SourceLabel = $"{_rules.ReferenceHeroXp} XP party, no gear";
            _cachedAt = now;

            return _party;
        }
    }
}
