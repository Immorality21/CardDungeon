using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The Paladin's kit from his vision (2026-10-10): Divine Intervention (Intercept + Retaliate),
    /// awakenings that chain (Consecrate -> Hallowed Ground -> Heavenly Fire), and the authored grid.
    /// </summary>
    public class PaladinKitTests
    {
        private static MagicSO Magic(MagicTargetType target, params SpellEffect[] effects)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = "test";
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>(effects);
            return magic;
        }

        [Test]
        public void Intercept_CoversTheAlly_AndBracesTheCaster()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var resolver = new EffectResolver { Events = events };
            var paladin = new MockCombatUnit("Paladin", 5, 0, 60);
            var ally = new MockCombatUnit("Cleric", 1, 0, 30);
            var intervention = Magic(MagicTargetType.SingleAlly, new SpellEffect { EffectType = SpellEffectType.Intercept, Power = 50 });

            resolver.Execute(new SpellcastAction { Magic = intervention, Caster = paladin, Targets = new List<ICombatUnit> { ally } }, tracker);
            Assert.AreSame(paladin, events.Guards.Redirect(ally), "single-target blows at the ally land on the Paladin");
            Assert.AreEqual(50f, tracker.GetResistanceBonus(paladin, DamageType.Normal));

            events.Publish(new TurnStarted { Unit = paladin });
            Assert.AreSame(ally, events.Guards.Redirect(ally), "the cover ends with the Paladin's next turn");
            Object.DestroyImmediate(intervention);
        }

        [Test]
        public void Retaliate_StrikesTheFirstAttackerOnce()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var resolver = new EffectResolver { Events = events };
            new RetaliationTracker(events, resolver, tracker);
            var paladin = new MockCombatUnit("Paladin", 5, 0, 60);
            var enemy = new MockCombatUnit("Ghoul", 3, 0, 40, isHero: false);
            var ready = Magic(MagicTargetType.Self, new SpellEffect
            {
                EffectType = SpellEffectType.Retaliate, Power = 6, PowerMode = PowerMode.Flat, DamageType = DamageType.Holy
            });
            resolver.Execute(new SpellcastAction { Magic = ready, Caster = paladin, Targets = new List<ICombatUnit> { paladin } }, tracker);

            HealthOps.Damage(paladin, 5, new HealthSource(enemy, HealthCause.Attack), events);
            Assert.AreEqual(34, enemy.Stats.Health, "struck back for 6");
            HealthOps.Damage(paladin, 5, new HealthSource(enemy, HealthCause.Attack), events);
            Assert.AreEqual(34, enemy.Stats.Health, "only once");
            Object.DestroyImmediate(ready);
        }

        [Test]
        public void Retaliate_EndsWithTheBearersNextTurn()
        {
            var events = new CombatEvents();
            var paladin = new MockCombatUnit("Paladin", 5, 0, 60);
            events.Retaliations.Arm(paladin, new SpellEffect());
            events.Publish(new TurnStarted { Unit = paladin });
            Assert.IsFalse(events.Retaliations.IsArmed(paladin));
        }

        [Test]
        public void Awakenings_Chain_ToTheLastOneOwned()
        {
            var paladin = AssetDatabase.LoadAssetAtPath<HeroSO>("Assets/ScriptableObjects/Heroes/Paladin.asset");
            var grid = paladin.SphereGrid;
            var first = SphereGridOps.AwakeningsForNodes(grid, new List<string> { "paladin-b-awaken" });
            Assert.AreEqual("HallowedGround", first["Consecrate"]);
            var both = SphereGridOps.AwakeningsForNodes(grid, new List<string> { "paladin-b-awaken", "paladin-b-awaken-2" });
            Assert.AreEqual("HeavenlyFire", both["Consecrate"], "owning both awakenings means the last of the chain");
        }

        [Test]
        public void Paladin_TeachesHisThreeUltras()
        {
            var paladin = AssetDatabase.LoadAssetAtPath<HeroSO>("Assets/ScriptableObjects/Heroes/Paladin.asset");
            var all = new List<string>();
            foreach (var node in paladin.SphereGrid.Nodes)
            {
                all.Add(node.Key);
            }
            var ultras = SphereGridOps.UltrasForNodes(paladin.SphereGrid, all);
            foreach (var key in new[] { "DivineIntervention", "JudgementDay", "LayOnHands" })
            {
                CollectionAssert.Contains(ultras, key);
                Assert.IsNotNull(UltraCatalogSO.Resolve(key), key + " should be in the Ultra catalog");
            }
        }
    }
}
