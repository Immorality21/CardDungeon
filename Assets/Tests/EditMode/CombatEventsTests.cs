using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The per-fight event stream (<see cref="CombatEvents"/>) and the health funnel that raises it
    /// (<see cref="HealthOps"/>): ordering, re-entrancy, kill attribution, and the guard that keeps
    /// every health write going through the funnel.
    /// </summary>
    public class CombatEventsTests
    {
        private class Ping
        {
            public int Value;
        }

        private class Pong
        {
            public int Value;
        }

        // ------------------------------------------------------------------ the bus

        [Test]
        public void Publish_CallsSubscribersInOrderThenSubscriptionOrder()
        {
            var events = new CombatEvents();
            var calls = new List<string>();
            events.Subscribe<Ping>(_ => calls.Add("late"), order: 10);
            events.Subscribe<Ping>(_ => calls.Add("first"));
            events.Subscribe<Ping>(_ => calls.Add("second"));
            events.Subscribe<Ping>(_ => calls.Add("early"), order: -5);

            events.Publish(new Ping());

            CollectionAssert.AreEqual(new[] { "early", "first", "second", "late" }, calls);
        }

        [Test]
        public void Publish_RoutesByExactType()
        {
            var events = new CombatEvents();
            int pings = 0;
            int pongs = 0;
            events.Subscribe<Ping>(_ => pings++);
            events.Subscribe<Pong>(_ => pongs++);

            events.Publish(new Pong());

            Assert.AreEqual(0, pings);
            Assert.AreEqual(1, pongs);
        }

        [Test]
        public void Publish_FromInsideAHandler_IsQueuedUntilTheCurrentEventReachesEveryone()
        {
            var events = new CombatEvents();
            var calls = new List<string>();
            events.Subscribe<Ping>(_ =>
            {
                calls.Add("A saw ping");
                events.Publish(new Pong());
                calls.Add("A done");
            });
            events.Subscribe<Ping>(_ => calls.Add("B saw ping"));
            events.Subscribe<Pong>(_ => calls.Add("pong"));

            events.Publish(new Ping());

            // Breadth-first: B hears the ping before anyone hears the pong A raised.
            CollectionAssert.AreEqual(new[] { "A saw ping", "A done", "B saw ping", "pong" }, calls);
        }

        [Test]
        public void Publish_ARunawayChain_StopsAtMaxChainInsteadOfHanging()
        {
            var events = new CombatEvents();
            int delivered = 0;
            events.Subscribe<Ping>(p =>
            {
                delivered++;
                events.Publish(new Ping { Value = p.Value + 1 });
            });

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new Regex("feeding itself"));
            events.Publish(new Ping());

            Assert.AreEqual(CombatEvents.MaxChain, delivered);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var events = new CombatEvents();
            int calls = 0;
            System.Action<Ping> handler = _ => calls++;
            events.Subscribe(handler);
            events.Unsubscribe(handler);

            events.Publish(new Ping());

            Assert.AreEqual(0, calls);
        }

        // ------------------------------------------------------------------ the funnel

        [Test]
        public void Damage_ReportsOnlyWhatLanded_AndCreditsTheKillOnce()
        {
            var events = new CombatEvents();
            var killer = new MockCombatUnit("Warrior", 10, 0, 30);
            var target = new MockCombatUnit("Rat", 1, 0, 10, isHero: false);
            target.Stats.Health = 4;
            var deaths = new List<UnitDefeated>();
            events.Subscribe<UnitDefeated>(deaths.Add);

            var change = HealthOps.Damage(target, 9, new HealthSource(killer, HealthCause.Attack), events);

            Assert.AreEqual(4, change.Landed, "overkill is not damage dealt");
            Assert.IsTrue(change.Killed);
            Assert.AreEqual(1, deaths.Count);
            Assert.AreSame(killer, deaths[0].Killer);
            Assert.AreSame(target, deaths[0].Victim);
            Assert.AreEqual(HealthCause.Attack, deaths[0].Cause);

            // Hitting the corpse again is not a second death.
            HealthOps.Damage(target, 5, new HealthSource(killer, HealthCause.Attack), events);
            Assert.AreEqual(1, deaths.Count);
        }

        [Test]
        public void Damage_Negative_IsAbsorbedAndClampedAtMax()
        {
            var target = new MockCombatUnit("Salamander", 1, 0, 20, isHero: false);
            target.Stats.Health = 17;

            var change = HealthOps.Damage(target, -10, new HealthSource(null, HealthCause.Attack), null);

            Assert.AreEqual(20, target.Stats.Health);
            Assert.AreEqual(3, change.Healed);
            Assert.AreEqual(0, change.Landed);
        }

        [Test]
        public void Heal_NeverPassesTheEffectiveMaximum_NorCutsABarAlreadyOverIt()
        {
            var hero = new MockCombatUnit("Cleric", 1, 0, 20);
            hero.EffectiveOverrides[StatType.MaxHealth] = 25;   // gear
            hero.Stats.Health = 22;

            Assert.AreEqual(3, HealthOps.Heal(hero, 10, new HealthSource(hero, HealthCause.Ability), null).Healed);
            Assert.AreEqual(25, hero.Stats.Health);

            hero.Stats.Health = 30;   // over the bar (a form just ended, say)
            Assert.AreEqual(0, HealthOps.Heal(hero, 10, new HealthSource(hero, HealthCause.Ability), null).Healed);
            Assert.AreEqual(30, hero.Stats.Health);
        }

        [Test]
        public void Pay_KeepsTheOneHealthFloor_AndNeverKills()
        {
            var events = new CombatEvents();
            var warlock = new MockCombatUnit("Warlock", 1, 0, 20);
            warlock.Stats.Health = 5;
            int deaths = 0;
            events.Subscribe<UnitDefeated>(_ => deaths++);

            var change = HealthOps.Pay(warlock, 50, new HealthSource(warlock, HealthCause.Cost), events);

            Assert.AreEqual(1, warlock.Stats.Health);
            Assert.AreEqual(4, change.Landed);
            Assert.AreEqual(0, deaths);
        }

        [Test]
        public void NothingMoved_RaisesNothing()
        {
            var events = new CombatEvents();
            var hero = new MockCombatUnit("Paladin", 1, 0, 20);
            int changes = 0;
            events.Subscribe<HealthChange>(_ => changes++);

            HealthOps.Heal(hero, 5, new HealthSource(hero, HealthCause.Ability), events);   // already full

            Assert.AreEqual(0, changes);
        }

        // ------------------------------------------------------------------ attribution end to end

        [Test]
        public void AnAbilityKill_IsCreditedToItsCaster()
        {
            var events = new CombatEvents();
            var resolver = new EffectResolver { Events = events };
            var caster = new MockCombatUnit("Mage", 20, 0, 30);
            var victim = new MockCombatUnit("Imp", 1, 0, 5, isHero: false);
            UnitDefeated death = null;
            events.Subscribe<UnitDefeated>(d => death = d);

            var bolt = ScriptableObject.CreateInstance<MagicSO>();
            bolt.Key = "bolt";
            bolt.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 10, ScalingStat = StatType.None, DamageType = DamageType.Fire }
            };
            bolt.Tags = new List<MagicTag>();

            resolver.Execute(new SpellcastAction { Magic = bolt, Caster = caster, Targets = new List<ICombatUnit> { victim } },
                new CombatBuffTracker { Events = events });

            Assert.IsNotNull(death, "the kill raised UnitDefeated");
            Assert.AreSame(caster, death.Killer);
            Assert.AreEqual(HealthCause.Ability, death.Cause);
            Assert.AreEqual(DamageType.Fire, death.DamageType);
            Object.DestroyImmediate(bolt);
        }

        [Test]
        public void ADamageOverTimeKill_IsCreditedToWhoeverAppliedIt()
        {
            var events = new CombatEvents();
            var tracker = new CombatBuffTracker { Events = events };
            var poisoner = new MockCombatUnit("Rogue", 5, 0, 30);
            var victim = new MockCombatUnit("Rat", 1, 0, 3, isHero: false);
            UnitDefeated death = null;
            events.Subscribe<UnitDefeated>(d => death = d);

            // Applied during the Rogue's turn, so the Rogue is credited without being named.
            tracker.BeginTurn(poisoner);
            tracker.ApplyOverTime(victim, BuffType.Poisoned, 5, 3);
            tracker.BeginTurn(victim);
            tracker.ResolveOverTime(victim, TickTiming.StartOfTurn);

            Assert.IsNotNull(death);
            Assert.AreSame(poisoner, death.Killer);
            Assert.AreEqual(HealthCause.OverTime, death.Cause);
        }

        // ------------------------------------------------------------------ the guard

        /// <summary>
        /// Nothing but <see cref="HealthOps"/> may assign <c>Stats.Health</c>. A direct write skips
        /// the event a death or a heal raises - a kill nobody is credited with, a reaction that never
        /// fires - and the old per-site clamps were exactly how the thirty-odd writes drifted apart.
        /// Tests may still set up health directly; this scans the game code only.
        /// </summary>
        [Test]
        public void OnlyHealthOps_WritesHealth()
        {
            var scripts = Path.Combine(Application.dataPath, "Scripts");
            // Any assignment or step through a member access - x.Health =, +=, -=, *=, /=, %=, ++, --.
            // Member access only, so a save record's own Health field (HeroHealthSaveData, set in an
            // object initializer) is not mistaken for a unit's bar. Stats.cs is exempt: it owns the field.
            var write = new Regex(@"\.Health\s*(\+\+|--|[-+*/%]?=(?!=))|(\+\+|--)\s*[\w.]*\.Health\b");
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name == "HealthOps.cs" || name == "Stats.cs")
                {
                    continue;
                }
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("//") || line.StartsWith("///") || line.StartsWith("*"))
                    {
                        continue;
                    }
                    if (write.IsMatch(line))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {line}");
                    }
                }
            }
            Assert.IsEmpty(offenders,
                "Health is written outside HealthOps - route it through HealthOps.Damage/Heal/Pay/Set:\n"
                + string.Join("\n", offenders));
        }
    }
}
