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
        private class TraitUnit : MockCombatUnit, IHasTraits
        {
            public TraitUnit(string name, UnitTraits traits)
                : base(name, 1, 0, 50, isHero: false)
            {
                Traits = traits;
            }

            public UnitTraits Traits { get; }
            public bool IsBoss => false;
        }

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

        // ---------------------------------------------------------------- Divine Protection (2026-10-30)

        [Test]
        public void DivineProtection_NegatesTheNextBlow_ThenIsSpent()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Cleric", 1, 0, 50);
            events.Protections.Add(hero, 1, 3);

            var blow = HealthOps.Damage(hero, 12, new HealthSource(null, HealthCause.Attack), events);
            Assert.AreEqual(12, blow.Negated);
            Assert.AreEqual(0, blow.Landed);
            Assert.AreEqual(50, hero.Stats.Health);
            Assert.AreEqual(0, events.Protections.HitsOf(hero));

            HealthOps.Damage(hero, 12, new HealthSource(null, HealthCause.Attack), events);
            Assert.AreEqual(38, hero.Stats.Health, "one hit only");
        }

        [Test]
        public void DivineProtection_NegatesAnAbilityHitToo()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Cleric", 1, 0, 50);
            events.Protections.Add(hero, 1, 3);

            var cast = HealthOps.Damage(hero, 20, new HealthSource(null, HealthCause.Ability, DamageType.Fire), events);
            Assert.AreEqual(20, cast.Negated);
            Assert.AreEqual(50, hero.Stats.Health);
        }

        [Test]
        public void DivineProtection_IsNotSpentOnTicksOrPrices()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var hero = new MockCombatUnit("Cleric", 1, 0, 50);
            events.Protections.Add(hero, 1, 3);

            HealthOps.Damage(hero, 5, new HealthSource(hero, HealthCause.Cost), events);
            Assert.AreEqual(45, hero.Stats.Health);
            tracker.ApplyOverTime(hero, BuffType.Poisoned, 4, 3);
            tracker.ResolveOverTime(hero);
            Assert.AreEqual(41, hero.Stats.Health);
            Assert.AreEqual(1, events.Protections.HitsOf(hero), "still waiting for a real hit");
        }

        [Test]
        public void DivineProtection_ComesBeforeABarrier_AndLeavesItWhole()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Cleric", 1, 0, 50);
            events.Barriers.Add(hero, 8, 3);
            events.Protections.Add(hero, 1, 3);

            var hit = HealthOps.Damage(hero, 10, new HealthSource(null, HealthCause.Attack), events);
            Assert.AreEqual(0, hit.Absorbed);
            Assert.AreEqual(8, events.Barriers.PointsOf(hero));
        }

        [Test]
        public void DivineProtection_WearsOffAfterThreeOfTheBearersTurns()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Cleric", 1, 0, 50);
            events.Protections.Add(hero, 1, 3);

            for (int i = 0; i < 2; i++)
            {
                events.Publish(new TurnStarted { Unit = hero });
                events.Publish(new TurnEnded { Unit = hero });
            }
            Assert.AreEqual(1, events.Protections.HitsOf(hero));
            events.Publish(new TurnStarted { Unit = hero });
            events.Publish(new TurnEnded { Unit = hero });
            Assert.AreEqual(0, events.Protections.HitsOf(hero));

            HealthOps.Damage(hero, 10, new HealthSource(null, HealthCause.Attack), events);
            Assert.AreEqual(40, hero.Stats.Health);
        }

        [Test]
        public void DivineProtection_CastOnOneself_DoesNotLoseThatTurn()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Paladin", 1, 0, 50);
            events.Publish(new TurnStarted { Unit = hero });
            events.Protections.Add(hero, 1, 1);
            events.Publish(new TurnEnded { Unit = hero });
            Assert.AreEqual(1, events.Protections.HitsOf(hero));
        }

        [Test]
        public void DivineProtectionBuff_CastOnAnAlly_ProtectsThem()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var resolver = new EffectResolver { Events = events };
            var paladin = new MockCombatUnit("Paladin", 5, 0, 60);
            var ally = new MockCombatUnit("Cleric", 1, 0, 30);
            var protection = Magic(MagicTargetType.SingleAlly, new SpellEffect
            {
                EffectType = SpellEffectType.Buff, BuffType = BuffType.DivineProtection, Power = 1, Duration = 3
            });

            resolver.Execute(new SpellcastAction { Magic = protection, Caster = paladin, Targets = new List<ICombatUnit> { ally } }, tracker);
            Assert.AreEqual(1, events.Protections.HitsOf(ally));
            Assert.AreEqual(0, events.Protections.HitsOf(paladin));
            Object.DestroyImmediate(protection);
        }

        [Test]
        public void Paladin_TeachesDivineProtectionAndSmite_NotTheWarriorsAbilities()
        {
            var paladin = AssetDatabase.LoadAssetAtPath<HeroSO>("Assets/ScriptableObjects/Heroes/Paladin.asset");
            var taught = new List<string>();
            foreach (var node in paladin.SphereGrid.Nodes)
            {
                if (node.Kind == SphereNodeKind.MagicKnown)
                {
                    taught.Add(node.GrantedMagicKey);
                }
            }
            CollectionAssert.Contains(taught, "DivineProtection");
            CollectionAssert.Contains(taught, "Smite");
            CollectionAssert.DoesNotContain(taught, "ShieldUp");
            CollectionAssert.DoesNotContain(taught, "Sunder");

            var awakened = SphereGridOps.AwakeningsForNodes(paladin.SphereGrid, new List<string> { "paladin-a-awaken" });
            Assert.AreEqual("Aegis", awakened["DivineProtection"]);
        }

        [Test]
        public void Aegis_IsDivineProtectionOnTheWholeParty()
        {
            var aegis = AssetDatabase.LoadAssetAtPath<MagicSO>("Assets/ScriptableObjects/Cards/Aegis.asset");
            Assert.AreEqual(MagicTargetType.AllAllies, aegis.TargetType);
            Assert.AreEqual(BuffType.DivineProtection, aegis.Effects[0].BuffType);
        }

        [Test]
        public void Smite_HitsTheUndeadAndDemonsHarder()
        {
            var smite = AssetDatabase.LoadAssetAtPath<MagicSO>("Assets/ScriptableObjects/Cards/Smite.asset");
            var damage = smite.Effects[0];
            Assert.AreEqual(SpellEffectType.Damage, damage.EffectType);
            Assert.AreEqual(StatType.Strength, damage.ScalingStat);
            Assert.AreEqual(DamageType.Holy, damage.DamageType);
            Assert.Greater(damage.BonusFactor(new TraitUnit("Ghoul", UnitTraits.Undead), null), 1f);
            Assert.Greater(damage.BonusFactor(new TraitUnit("Imp", UnitTraits.Demon | UnitTraits.Flying), null), 1f);
            Assert.AreEqual(1f, damage.BonusFactor(new TraitUnit("Hound", UnitTraits.Beast), null));
        }
    }
}
