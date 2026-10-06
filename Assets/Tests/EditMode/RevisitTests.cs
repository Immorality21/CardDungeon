using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Revisits (docs/plans/REVISITS.md): the pure rules that turn chosen conditions into a
    /// <see cref="RunFear"/>, what that fear does to damage, healing and rewards, and the project's
    /// real rules asset and run list.
    /// </summary>
    public class RevisitTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                Object.DestroyImmediate(obj);
            }
            _created.Clear();
            RunFear.Current = RunFear.None;
        }

        private RevisitRulesSO MakeRules()
        {
            var rules = ScriptableObject.CreateInstance<RevisitRulesSO>();
            _created.Add(rules);
            rules.BaseEnemyStatPercent = 50;
            rules.BaseRewardPercent = 25;
            rules.RewardPercentPerFear = 10;
            rules.Modifiers = new List<RunModifier>
            {
                Modifier("health", 3, 1, RunModifierEffectKind.EnemyHealthPercent, 25),
                Modifier("damage", 3, 1, RunModifierEffectKind.EnemyDamagePercent, 25),
                Modifier("swarm", 2, 2, RunModifierEffectKind.ExtraEnemiesPerRoom, 1),
                Modifier("no-heal", 2, 2, RunModifierEffectKind.HeroHealingPercent, -50),
            };
            return rules;
        }

        private static RunModifier Modifier(string key, int maxRank, int fear, RunModifierEffectKind kind, int amount)
        {
            return new RunModifier
            {
                Key = key,
                DisplayName = key,
                Description = "+{0}",
                MaxRank = maxRank,
                FearPerRank = fear,
                Effects = new List<RunModifierEffect> { new RunModifierEffect { Kind = kind, AmountPerRank = amount } },
            };
        }

        private static List<RunModifierSelection> Pick(params (string Key, int Rank)[] picks)
        {
            var list = new List<RunModifierSelection>();
            foreach (var pick in picks)
            {
                list.Add(new RunModifierSelection(pick.Key, pick.Rank));
            }
            return list;
        }

        // --- Resolve ----------------------------------------------------------------------

        [Test]
        public void Resolve_NotARevisit_IsTheIdentity()
        {
            var fear = RevisitOps.Resolve(MakeRules(), false, Pick(("health", 3)));

            Assert.AreSame(RunFear.None, fear);
            Assert.AreEqual(1f, fear.EnemyHealthMultiplier);
            Assert.AreEqual(1f, fear.RewardMultiplier);
        }

        [Test]
        public void Resolve_RevisitWithNoConditions_AppliesTheBaseFiftyPercentToEveryStat()
        {
            var fear = RevisitOps.Resolve(MakeRules(), true, null);

            Assert.IsTrue(fear.IsRevisit);
            Assert.AreEqual(0, fear.Level);
            Assert.AreEqual(1.5f, fear.EnemyStatMultiplier, 1e-4f);
            Assert.AreEqual(1f, fear.EnemyHealthMultiplier, 1e-4f, "conditions only");
            Assert.AreEqual(1f, fear.EnemyDamageMultiplier, 1e-4f, "conditions only");
            Assert.AreEqual(1.25f, fear.RewardMultiplier, 1e-4f, "the base pays for itself");
            Assert.AreEqual(1f, fear.HeroHealingMultiplier, 1e-4f);
        }

        [Test]
        public void Resolve_ConditionsStackOnTheBase_AndAddFear()
        {
            var fear = RevisitOps.Resolve(MakeRules(), true,
                Pick(("health", 2), ("damage", 1), ("swarm", 1), ("no-heal", 2)));

            Assert.AreEqual(2 + 1 + 2 + 4, fear.Level);
            Assert.AreEqual(1.5f, fear.EnemyStatMultiplier, 1e-4f, "the base, untouched by conditions");
            Assert.AreEqual(1.5f, fear.EnemyHealthMultiplier, 1e-4f, "2 x 25, on top of the base");
            Assert.AreEqual(1.25f, fear.EnemyDamageMultiplier, 1e-4f);
            Assert.AreEqual(1, fear.ExtraEnemiesPerRoom);
            Assert.AreEqual(0f, fear.HeroHealingMultiplier, 1e-4f, "two ranks of -50% is no healing");
            Assert.AreEqual(1f + (25 + 9 * 10) / 100f, fear.RewardMultiplier, 1e-4f);
        }

        [Test]
        public void Resolve_UnknownKeysAndOverRanks_AreIgnoredAndClamped()
        {
            var fear = RevisitOps.Resolve(MakeRules(), true, Pick(("gone", 3), ("health", 9)));

            Assert.AreEqual(3, fear.Level, "rank clamped to the condition's max of 3");
            Assert.AreEqual(1.75f, fear.EnemyHealthMultiplier, 1e-4f);
        }

        [Test]
        public void Resolve_SpeedCondition_ScalesEnemyAgility()
        {
            var rules = MakeRules();
            rules.Modifiers.Add(Modifier("speed", 2, 2, RunModifierEffectKind.EnemyAgilityPercent, 25));

            var fear = RevisitOps.Resolve(rules, true, Pick(("speed", 2)));

            Assert.AreEqual(1.5f, fear.EnemyAgilityMultiplier, 1e-4f);
            Assert.AreEqual(4, fear.Level);
            Assert.AreEqual(9, fear.ScaleEnemyAgility(6));
            Assert.AreEqual(1f, RevisitOps.Resolve(rules, true, null).EnemyAgilityMultiplier, 1e-4f,
                "the Speed condition stacks on the base; the base itself rides EnemyStatMultiplier");
        }

        [Test]
        public void Summary_NamesEveryChangeAndTheReward()
        {
            var rules = MakeRules();
            rules.Modifiers.Add(Modifier("speed", 2, 2, RunModifierEffectKind.EnemyAgilityPercent, 25));

            string baseLine = RevisitOps.Summary(RevisitOps.Resolve(rules, true, null));
            Assert.AreEqual("Fear level 0: enemies +50% to every stat. Rewards +25% XP, gold and Essence.", baseLine);

            string hot = RevisitOps.Summary(RevisitOps.Resolve(rules, true,
                Pick(("speed", 1), ("swarm", 1), ("no-heal", 2))));
            StringAssert.Contains("+25% more speed", hot);
            StringAssert.Contains("+1 foe per fight", hot);
            StringAssert.Contains("no healing", hot);

            Assert.AreEqual(string.Empty, RevisitOps.Summary(RunFear.None));
        }

        // --- Picker edits -----------------------------------------------------------------

        [Test]
        public void CycleRank_StepsUpThenWrapsToOff()
        {
            var rules = MakeRules();
            var picks = new List<RunModifierSelection>();

            RevisitOps.CycleRank(rules, picks, "swarm");
            Assert.AreEqual(1, RevisitOps.RankOf(picks, "swarm"));
            RevisitOps.CycleRank(rules, picks, "swarm");
            Assert.AreEqual(2, RevisitOps.RankOf(picks, "swarm"));
            RevisitOps.CycleRank(rules, picks, "swarm");
            Assert.AreEqual(0, RevisitOps.RankOf(picks, "swarm"));
            Assert.IsEmpty(picks, "rank 0 is removed, so only what is on is saved");
        }

        [Test]
        public void SetRank_ClampsAndIgnoresUnknownKeys()
        {
            var rules = MakeRules();
            var picks = new List<RunModifierSelection>();

            RevisitOps.SetRank(rules, picks, "damage", 7);
            RevisitOps.SetRank(rules, picks, "gone", 1);

            Assert.AreEqual(1, picks.Count);
            Assert.AreEqual(3, RevisitOps.RankOf(picks, "damage"));
        }

        [Test]
        public void Describe_FillsTheRanksTotal()
        {
            var modifier = Modifier("no-heal", 2, 2, RunModifierEffectKind.HeroHealingPercent, -50);
            modifier.Description = "{0}% less healing";

            Assert.AreEqual("50% less healing", RevisitOps.Describe(modifier, 0), "rank 0 previews rank 1");
            Assert.AreEqual("100% less healing", RevisitOps.Describe(modifier, 2));
        }

        // --- Scarcity ---------------------------------------------------------------------

        private RevisitRulesSO RulesWithScarce(string itemKey)
        {
            var rules = MakeRules();
            var item = ScriptableObject.CreateInstance<ItemSO>();
            _created.Add(item);
            item.Key = itemKey;
            rules.NewBestFearOnly = new List<ItemSO> { item };
            return rules;
        }

        [Test]
        public void ApplyScarcity_FirstRevisit_PaysTheScarceMaterial()
        {
            var rules = RulesWithScarce("VoidShard");
            var fear = RevisitOps.Resolve(rules, true, null);

            RevisitOps.ApplyScarcity(fear, rules, -1);

            Assert.IsFalse(fear.Withholds("VoidShard"));
        }

        [Test]
        public void ApplyScarcity_AtOrBelowTheBestFear_WithholdsIt()
        {
            var rules = RulesWithScarce("VoidShard");
            var fear = RevisitOps.Resolve(rules, true, Pick(("health", 2)));

            RevisitOps.ApplyScarcity(fear, rules, 2);

            Assert.IsTrue(fear.Withholds("VoidShard"));
            Assert.IsFalse(fear.Withholds("ScrapIron"), "only the listed materials are withheld");
        }

        [Test]
        public void ApplyScarcity_BeatingTheBestFear_PaysIt()
        {
            var rules = RulesWithScarce("VoidShard");
            var fear = RevisitOps.Resolve(rules, true, Pick(("health", 3)));

            RevisitOps.ApplyScarcity(fear, rules, 2);

            Assert.IsFalse(fear.Withholds("VoidShard"));
        }

        [Test]
        public void ApplyScarcity_NeverTouchesTheSharedNone()
        {
            var rules = RulesWithScarce("VoidShard");

            RevisitOps.ApplyScarcity(RunFear.None, rules, 5);

            Assert.IsFalse(RunFear.None.Withholds("VoidShard"));
        }

        // --- What the fear does -----------------------------------------------------------

        [Test]
        public void DamageCondition_RaisesWhatTheEnemyHitsWith()
        {
            var fear = new RunFear { EnemyStatMultiplier = 1.5f, EnemyDamageMultiplier = 1.5f };
            var stats = new Assets.Scripts.UnitStats.StatBlock();
            stats[Assets.Scripts.UnitStats.StatType.Strength] = 4;
            stats[Assets.Scripts.UnitStats.StatType.Intelligence] = 2;
            stats[Assets.Scripts.UnitStats.StatType.Endurance] = 2;

            fear.ScaleEnemyStats(stats);

            Assert.AreEqual(9, stats[Assets.Scripts.UnitStats.StatType.Strength], "4 x 1.5 = 6, x 1.5 = 9");
            Assert.AreEqual(4, stats[Assets.Scripts.UnitStats.StatType.Intelligence], "2 x 1.5 = 3, x 1.5 = 4.5 -> 4 (RoundToInt rounds halves to even)");
            Assert.AreEqual(3, stats[Assets.Scripts.UnitStats.StatType.Endurance], "defence is not damage");
            Assert.AreEqual(2.25f, fear.EnemySpellPowerScale, 1e-4f);
        }

        [Test]
        public void OverTimePower_ScalesLikeEnemySpells()
        {
            var fear = new RunFear { EnemyStatMultiplier = 1.5f };

            Assert.AreEqual(6, fear.ScaleEnemyOverTimePower(4));
            Assert.AreEqual(0, fear.ScaleEnemyOverTimePower(0));
            Assert.AreEqual(4, RunFear.None.ScaleEnemyOverTimePower(4));
        }

        [Test]
        public void EnemyPoison_OnAFearLevel_TicksHarder_ButAHeroPoisonDoesNot()
        {
            var effect = new Assets.Scripts.Cards.SpellEffect
            {
                EffectType = Assets.Scripts.Cards.SpellEffectType.Debuff,
                BuffType = Assets.Scripts.Cards.BuffType.Poisoned,
                Power = 4,
                Duration = 3,
                ScalingStat = Assets.Scripts.UnitStats.StatType.None,
            };
            var enemy = new MockCombatUnit("Foe", 5, 0, 20, 5, false);
            var hero = new MockCombatUnit("Hero", 5, 0, 40, 5, true);
            var otherEnemy = new MockCombatUnit("Other", 5, 0, 40, 5, false);
            var executor = new Assets.Scripts.Cards.Effects.DebuffEffectExecutor();

            RunFear.Current = new RunFear { IsRevisit = true, EnemyStatMultiplier = 1.5f };
            var tracker = new Assets.Scripts.Cards.CombatBuffTracker();
            executor.Execute(effect, enemy, new System.Collections.Generic.List<ICombatUnit> { hero }, tracker,
                new Assets.Scripts.Cards.EffectResult());
            executor.Execute(effect, hero, new System.Collections.Generic.List<ICombatUnit> { otherEnemy }, tracker,
                new Assets.Scripts.Cards.EffectResult());

            int heroBefore = hero.Stats.Health;
            int enemyBefore = otherEnemy.Stats.Health;
            tracker.ResolveOverTime(hero);
            tracker.ResolveOverTime(otherEnemy);

            Assert.AreEqual(6, heroBefore - hero.Stats.Health, "the enemy's poison: 4 x 1.5");
            Assert.AreEqual(4, enemyBefore - otherEnemy.Stats.Health, "the hero's poison is untouched");
        }

        [Test]
        public void WithFear_PutsTheLevelOnACopy_AndStatsForReadsIt()
        {
            var enemy = ScriptableObject.CreateInstance<Assets.Scripts.Enemies.EnemySO>();
            _created.Add(enemy);
            enemy.BaseStats = new Assets.Scripts.UnitStats.StatBlock();
            enemy.BaseStats[Assets.Scripts.UnitStats.StatType.MaxHealth] = 20;
            enemy.BaseStats[Assets.Scripts.UnitStats.StatType.Strength] = 4;
            enemy.XpReward = 10;
            var authored = new Assets.Scripts.Enemies.LevelEnemyTuning { Difficulty = 2f };
            var fear = RevisitOps.Resolve(MakeRules(), true, null);

            var tuned = Assets.Scripts.Enemies.LevelEnemyTuning.WithFear(authored, fear);

            Assert.IsNull(authored.Fear, "the authored tuning never carries a Fear level");
            Assert.AreEqual(60, Assets.Scripts.Enemies.LevelEnemyTuning.StatsFor(enemy, tuned)[Assets.Scripts.UnitStats.StatType.MaxHealth],
                "20 x Difficulty 2 = 40, x 1.5 = 60");
            Assert.AreEqual(40, Assets.Scripts.Enemies.LevelEnemyTuning.StatsFor(enemy, tuned, includeFear: false)[Assets.Scripts.UnitStats.StatType.MaxHealth]);
            Assert.AreEqual(12, Assets.Scripts.Enemies.LevelEnemyTuning.XpFor(enemy, tuned), "10 x 1.25 = 12.5 -> 12 (halves to even)");
            Assert.AreSame(authored, Assets.Scripts.Enemies.LevelEnemyTuning.WithFear(authored, RunFear.None),
                "no revisit, no copy");
        }

        [Test]
        public void ScaleHealing_ScalesHeroesOnly_AndZeroMeansZero()
        {
            var fear = new RunFear { HeroHealingMultiplier = 0f };
            var enemy = new MockCombatUnit("Foe", 5, 0, 20, 5, false);
            var hero = new MockCombatUnit("Hero", 5, 0, 20, 5, true);

            Assert.AreEqual(0, fear.ScaleHealing(hero, 12));
            Assert.AreEqual(12, fear.ScaleHealing(enemy, 12), "an enemy healer still heals");

            fear.HeroHealingMultiplier = 0.5f;
            Assert.AreEqual(5, fear.ScaleHealing(hero, 11), "rounded down");
        }

        [Test]
        public void ScaleEnemyStats_ScalesEveryStat_ThenTheConditionsOnTop()
        {
            var fear = new RunFear { EnemyStatMultiplier = 1.5f, EnemyHealthMultiplier = 1.5f };
            var stats = new Assets.Scripts.UnitStats.StatBlock();
            stats[Assets.Scripts.UnitStats.StatType.MaxHealth] = 20;
            stats[Assets.Scripts.UnitStats.StatType.Strength] = 4;
            stats[Assets.Scripts.UnitStats.StatType.Agility] = 6;
            stats[Assets.Scripts.UnitStats.StatType.Endurance] = 2;

            fear.ScaleEnemyStats(stats);

            Assert.AreEqual(45, stats[Assets.Scripts.UnitStats.StatType.MaxHealth], "20 x 1.5 = 30, then x 1.5 for the condition");
            Assert.AreEqual(6, stats[Assets.Scripts.UnitStats.StatType.Strength]);
            Assert.AreEqual(9, stats[Assets.Scripts.UnitStats.StatType.Agility]);
            Assert.AreEqual(3, stats[Assets.Scripts.UnitStats.StatType.Endurance]);
            Assert.AreEqual(0, stats[Assets.Scripts.UnitStats.StatType.Luck], "a stat the enemy does not have stays 0");
        }

        [Test]
        public void ScaleReward_AndEnemyHealth_NeverScaleToNothing()
        {
            var fear = new RunFear { RewardMultiplier = 1.25f, EnemyHealthMultiplier = 1.5f };

            Assert.AreEqual(5, fear.ScaleReward(4));
            Assert.AreEqual(0, fear.ScaleReward(0));
            Assert.AreEqual(30, fear.ScaleEnemyMaxHealth(20));
        }

        [Test]
        public void None_ChangesNothing()
        {
            var unit = new MockCombatUnit("Foe", 5, 0, 20, 5, false);

            Assert.AreEqual(10, RunFear.None.ScaleHealing(new MockCombatUnit("Hero", 5, 0, 20, 5, true), 10));
            Assert.AreEqual(7, RunFear.None.ScaleReward(7));
            Assert.AreEqual(0, RunFear.None.ExtraEnemiesPerRoom);
        }

        // --- The project's assets ---------------------------------------------------------

        [Test]
        public void RulesAsset_ExistsAndEveryConditionIsWellFormed()
        {
            var rules = RevisitRulesSO.Load();
            Assert.IsNotNull(rules, $"No rules at Assets/Resources/{RevisitRulesSO.ResourcePath}.asset - revisits would change nothing.");

            var keys = new List<string>();
            foreach (var modifier in rules.Modifiers)
            {
                Assert.IsNotNull(modifier, "a null condition in the list");
                Assert.IsFalse(string.IsNullOrEmpty(modifier.Key), $"'{modifier.DisplayName}' has no key");
                Assert.IsFalse(keys.Contains(modifier.Key), $"duplicate condition key '{modifier.Key}'");
                keys.Add(modifier.Key);
                Assert.GreaterOrEqual(modifier.MaxRank, 1, modifier.Key);
                Assert.Greater(modifier.FearPerRank, 0, $"'{modifier.Key}' adds no fear, so it pays nothing for being harder");
                Assert.IsNotEmpty(modifier.Effects, $"'{modifier.Key}' does nothing");
            }
        }

        [Test]
        public void RulesAsset_WithholdsVoidShard()
        {
            var rules = RevisitRulesSO.Load();
            Assert.IsTrue(rules.NewBestFearOnly.Exists(i => i != null && i.Key == "VoidShard"),
                "Void Shard must be withheld below a run's best fear, or every repeatable boss is an infinite tap.");
        }

        [Test]
        public void RevisitableRuns_AreEverythingButTheTutorialChallengeAndSecretRuns()
        {
            // Owner, 2026-10-06: "everything except tutorial, challenge and secret, for now".
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            Assert.IsNotNull(campaign);

            foreach (var node in campaign.Nodes)
            {
                if (node?.Run == null)
                {
                    continue;
                }
                bool excluded = node.Run.Challenge || node.Secret || IsTutorial(campaign, node);
                Assert.AreEqual(!excluded, RevisitOps.IsRevisitable(node.Run),
                    $"{node.Run.name}: revisitable should be {!excluded}");
            }
        }

        private static bool IsTutorial(CampaignSO campaign, CampaignNodeEntry node)
        {
            return node.Requires == null || node.Requires.Count == 0;
        }
    }
}
