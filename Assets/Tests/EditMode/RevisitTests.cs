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
    /// <see cref="RunHeat"/>, what that heat does to damage, healing and rewards, and the project's
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
            RunHeat.Current = RunHeat.None;
        }

        private RevisitRulesSO MakeRules()
        {
            var rules = ScriptableObject.CreateInstance<RevisitRulesSO>();
            _created.Add(rules);
            rules.BaseEnemyHealthPercent = 50;
            rules.BaseEnemyDamagePercent = 50;
            rules.BaseRewardPercent = 25;
            rules.RewardPercentPerHeat = 10;
            rules.Modifiers = new List<RunModifier>
            {
                Modifier("health", 3, 1, RunModifierEffectKind.EnemyHealthPercent, 25),
                Modifier("damage", 3, 1, RunModifierEffectKind.EnemyDamagePercent, 25),
                Modifier("swarm", 2, 2, RunModifierEffectKind.ExtraEnemiesPerRoom, 1),
                Modifier("no-heal", 2, 2, RunModifierEffectKind.HeroHealingPercent, -50),
            };
            return rules;
        }

        private static RunModifier Modifier(string key, int maxRank, int heat, RunModifierEffectKind kind, int amount)
        {
            return new RunModifier
            {
                Key = key,
                DisplayName = key,
                Description = "+{0}",
                MaxRank = maxRank,
                HeatPerRank = heat,
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
            var heat = RevisitOps.Resolve(MakeRules(), false, Pick(("health", 3)));

            Assert.AreSame(RunHeat.None, heat);
            Assert.AreEqual(1f, heat.EnemyHealthMultiplier);
            Assert.AreEqual(1f, heat.RewardMultiplier);
        }

        [Test]
        public void Resolve_RevisitWithNoConditions_AppliesTheBaseFiftyPercent()
        {
            var heat = RevisitOps.Resolve(MakeRules(), true, null);

            Assert.IsTrue(heat.IsRevisit);
            Assert.AreEqual(0, heat.Heat);
            Assert.AreEqual(1.5f, heat.EnemyHealthMultiplier, 1e-4f);
            Assert.AreEqual(1.5f, heat.EnemyDamageMultiplier, 1e-4f);
            Assert.AreEqual(1.25f, heat.RewardMultiplier, 1e-4f, "the base pays for itself");
            Assert.AreEqual(1f, heat.HeroHealingMultiplier, 1e-4f);
        }

        [Test]
        public void Resolve_ConditionsStackOnTheBase_AndAddHeat()
        {
            var heat = RevisitOps.Resolve(MakeRules(), true,
                Pick(("health", 2), ("damage", 1), ("swarm", 1), ("no-heal", 2)));

            Assert.AreEqual(2 + 1 + 2 + 4, heat.Heat);
            Assert.AreEqual(2.0f, heat.EnemyHealthMultiplier, 1e-4f, "50 + 2 x 25");
            Assert.AreEqual(1.75f, heat.EnemyDamageMultiplier, 1e-4f, "50 + 25");
            Assert.AreEqual(1, heat.ExtraEnemiesPerRoom);
            Assert.AreEqual(0f, heat.HeroHealingMultiplier, 1e-4f, "two ranks of -50% is no healing");
            Assert.AreEqual(1f + (25 + 9 * 10) / 100f, heat.RewardMultiplier, 1e-4f);
        }

        [Test]
        public void Resolve_UnknownKeysAndOverRanks_AreIgnoredAndClamped()
        {
            var heat = RevisitOps.Resolve(MakeRules(), true, Pick(("gone", 3), ("health", 9)));

            Assert.AreEqual(3, heat.Heat, "rank clamped to the condition's max of 3");
            Assert.AreEqual(2.25f, heat.EnemyHealthMultiplier, 1e-4f);
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
            rules.NewBestHeatOnly = new List<ItemSO> { item };
            return rules;
        }

        [Test]
        public void ApplyScarcity_FirstRevisit_PaysTheScarceMaterial()
        {
            var rules = RulesWithScarce("VoidShard");
            var heat = RevisitOps.Resolve(rules, true, null);

            RevisitOps.ApplyScarcity(heat, rules, -1);

            Assert.IsFalse(heat.Withholds("VoidShard"));
        }

        [Test]
        public void ApplyScarcity_AtOrBelowTheBestHeat_WithholdsIt()
        {
            var rules = RulesWithScarce("VoidShard");
            var heat = RevisitOps.Resolve(rules, true, Pick(("health", 2)));

            RevisitOps.ApplyScarcity(heat, rules, 2);

            Assert.IsTrue(heat.Withholds("VoidShard"));
            Assert.IsFalse(heat.Withholds("ScrapIron"), "only the listed materials are withheld");
        }

        [Test]
        public void ApplyScarcity_BeatingTheBestHeat_PaysIt()
        {
            var rules = RulesWithScarce("VoidShard");
            var heat = RevisitOps.Resolve(rules, true, Pick(("health", 3)));

            RevisitOps.ApplyScarcity(heat, rules, 2);

            Assert.IsFalse(heat.Withholds("VoidShard"));
        }

        [Test]
        public void ApplyScarcity_NeverTouchesTheSharedNone()
        {
            var rules = RulesWithScarce("VoidShard");

            RevisitOps.ApplyScarcity(RunHeat.None, rules, 5);

            Assert.IsFalse(RunHeat.None.Withholds("VoidShard"));
        }

        // --- What the heat does -----------------------------------------------------------

        [Test]
        public void ScaleOutgoingDamage_ScalesEnemiesOnly()
        {
            var heat = new RunHeat { EnemyDamageMultiplier = 1.5f };
            var enemy = new MockCombatUnit("Foe", 5, 0, 20, 5, false);
            var hero = new MockCombatUnit("Hero", 5, 0, 20, 5, true);

            Assert.AreEqual(15, heat.ScaleOutgoingDamage(enemy, 10));
            Assert.AreEqual(10, heat.ScaleOutgoingDamage(hero, 10));
            Assert.AreEqual(1, new RunHeat { EnemyDamageMultiplier = 0.1f }.ScaleOutgoingDamage(enemy, 1),
                "never scales a hit to nothing");
        }

        [Test]
        public void ScaleHealing_ScalesHeroesOnly_AndZeroMeansZero()
        {
            var heat = new RunHeat { HeroHealingMultiplier = 0f };
            var enemy = new MockCombatUnit("Foe", 5, 0, 20, 5, false);
            var hero = new MockCombatUnit("Hero", 5, 0, 20, 5, true);

            Assert.AreEqual(0, heat.ScaleHealing(hero, 12));
            Assert.AreEqual(12, heat.ScaleHealing(enemy, 12), "an enemy healer still heals");

            heat.HeroHealingMultiplier = 0.5f;
            Assert.AreEqual(5, heat.ScaleHealing(hero, 11), "rounded down");
        }

        [Test]
        public void ScaleReward_AndEnemyHealth_NeverScaleToNothing()
        {
            var heat = new RunHeat { RewardMultiplier = 1.25f, EnemyHealthMultiplier = 1.5f };

            Assert.AreEqual(5, heat.ScaleReward(4));
            Assert.AreEqual(0, heat.ScaleReward(0));
            Assert.AreEqual(30, heat.ScaleEnemyMaxHealth(20));
        }

        [Test]
        public void None_ChangesNothing()
        {
            var unit = new MockCombatUnit("Foe", 5, 0, 20, 5, false);

            Assert.AreEqual(10, RunHeat.None.ScaleOutgoingDamage(unit, 10));
            Assert.AreEqual(10, RunHeat.None.ScaleHealing(new MockCombatUnit("Hero", 5, 0, 20, 5, true), 10));
            Assert.AreEqual(7, RunHeat.None.ScaleReward(7));
            Assert.AreEqual(0, RunHeat.None.ExtraEnemiesPerRoom);
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
                Assert.Greater(modifier.HeatPerRank, 0, $"'{modifier.Key}' adds no heat, so it pays nothing for being harder");
                Assert.IsNotEmpty(modifier.Effects, $"'{modifier.Key}' does nothing");
            }
        }

        [Test]
        public void RulesAsset_WithholdsVoidShard()
        {
            var rules = RevisitRulesSO.Load();
            Assert.IsTrue(rules.NewBestHeatOnly.Exists(i => i != null && i.Key == "VoidShard"),
                "Void Shard must be withheld below a run's best heat, or every repeatable boss is an infinite tap.");
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
