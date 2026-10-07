using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using Assets.Scripts.Enemies;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Authored reactions (<see cref="TriggeredEffect"/>) fired by <see cref="TriggerRegistry"/> off a
    /// fight's event stream - in isolation, and inside the balance simulator's encounter loop, which
    /// builds the same registry the live fight does.
    /// </summary>
    public class TriggerTests
    {
        /// <summary>A mock unit that carries triggers, the way a hero carries its gear's.</summary>
        private class Bearer : MockCombatUnit, ITriggerSource
        {
            public readonly List<CarriedTrigger> Carried = new List<CarriedTrigger>();

            public Bearer(string name, int strength, int health, bool isHero = true)
                : base(name, strength, 0, health, isHero: isHero)
            {
            }

            public Bearer With(TriggeredEffect trigger, string from = "test item")
            {
                Carried.Add(new CarriedTrigger(trigger, from));
                return this;
            }

            public IEnumerable<CarriedTrigger> GetTriggers()
            {
                return Carried;
            }
        }

        private CombatEvents _events;
        private CombatBuffTracker _tracker;
        private EffectResolver _resolver;
        private List<ICombatUnit> _units;
        private List<ReactionResolved> _reactions;

        [SetUp]
        public void SetUp()
        {
            _events = new CombatEvents();
            _tracker = new CombatBuffTracker { Events = _events };
            _resolver = new EffectResolver { Events = _events };
            _units = new List<ICombatUnit>();
            _reactions = new List<ReactionResolved>();
            new TriggerRegistry(_events, _resolver, _tracker, () => _units, () => 0f);
            _events.Subscribe<ReactionResolved>(_reactions.Add, TriggerRegistry.Order + 1);
        }

        private static SpellEffect Flat(SpellEffectType type, int power, BuffType buff = BuffType.Strength, int duration = 99)
        {
            return new SpellEffect
            {
                EffectType = type,
                Power = power,
                PowerMode = PowerMode.Flat,
                ScalingStat = StatType.None,
                BuffType = buff,
                Duration = duration
            };
        }

        private static TriggeredEffect When(TriggerKind kind, TriggerTarget target, params SpellEffect[] effects)
        {
            return new TriggeredEffect { When = kind, Target = target, Effects = effects.ToList() };
        }

        private void Hit(ICombatUnit attacker, ICombatUnit target, int amount)
        {
            HealthOps.Damage(target, amount, new HealthSource(attacker, HealthCause.Attack), _events);
        }

        [Test]
        public void OnKill_BuffsTheBearer()
        {
            var sword = When(TriggerKind.OnKill, TriggerTarget.Bearer, Flat(SpellEffectType.Buff, 2));
            var hero = new Bearer("Warrior", 10, 30).With(sword, "Soulreaver");
            var rat = new Bearer("Rat", 1, 5, isHero: false);
            _units.AddRange(new ICombatUnit[] { hero, rat });

            Hit(hero, rat, 10);

            Assert.AreEqual(2, _tracker.GetBuffAmount(hero, StatType.Strength));
            Assert.AreEqual(1, _reactions.Count);
            Assert.AreEqual("Soulreaver", _reactions[0].Name, "falls back to what it came from");
        }

        [Test]
        public void OnDefeated_ResolvesFromTheFallenBearer_AndItsKillsAreCredited()
        {
            var bomb = When(TriggerKind.OnDefeated, TriggerTarget.AllEnemies, Flat(SpellEffectType.Damage, 50));
            var imp = new Bearer("Fire Imp", 1, 5, isHero: false).With(bomb);
            var hero = new Bearer("Warrior", 10, 30);
            var deaths = new List<UnitDefeated>();
            _events.Subscribe<UnitDefeated>(deaths.Add);
            _units.AddRange(new ICombatUnit[] { hero, imp });

            Hit(hero, imp, 10);

            Assert.IsFalse(hero.IsAlive, "the explosion took the hero with it");
            Assert.AreEqual(2, deaths.Count);
            var heroDeath = deaths.Single(d => d.Victim == hero);
            Assert.AreSame(imp, heroDeath.Killer);
            Assert.IsTrue(heroDeath.ByReaction);
        }

        [Test]
        public void AReactionsHits_DoNotSetOffMoreHitReactions()
        {
            // Two units that answer every hit they deal with another hit: without the reaction mark
            // they would trade blows until the stream's chain cap.
            var thorns = When(TriggerKind.OnDealDamage, TriggerTarget.Other, Flat(SpellEffectType.Damage, 1));
            var a = new Bearer("A", 1, 1000).With(thorns);
            var b = new Bearer("B", 1, 1000, isHero: false).With(thorns);
            _units.AddRange(new ICombatUnit[] { a, b });

            Hit(a, b, 5);

            Assert.AreEqual(1, _reactions.Count, "one reaction to the one real hit");
            Assert.AreEqual(1000 - 5 - 1, b.Stats.Health);
            Assert.AreEqual(1000, a.Stats.Health);
        }

        [Test]
        public void OnTakeDamage_AnswersTheAttacker()
        {
            var spikes = When(TriggerKind.OnTakeDamage, TriggerTarget.Other, Flat(SpellEffectType.Damage, 3));
            var knight = new Bearer("Knight", 1, 100).With(spikes);
            var rat = new Bearer("Rat", 1, 20, isHero: false);
            _units.AddRange(new ICombatUnit[] { knight, rat });

            Hit(rat, knight, 4);

            Assert.AreEqual(17, rat.Stats.Health);
        }

        [Test]
        public void OncePerCombat_FiresOnce()
        {
            var once = When(TriggerKind.OnTakeDamage, TriggerTarget.Bearer, Flat(SpellEffectType.Heal, 5));
            once.Limit = TriggerLimit.OncePerCombat;
            var hero = new Bearer("Paladin", 1, 100).With(once);
            var rat = new Bearer("Rat", 1, 20, isHero: false);
            _units.AddRange(new ICombatUnit[] { hero, rat });

            Hit(rat, hero, 10);
            Hit(rat, hero, 10);

            Assert.AreEqual(1, _reactions.Count);
        }

        [Test]
        public void OncePerTurn_ResetsWhenATurnStarts()
        {
            var perTurn = When(TriggerKind.OnTakeDamage, TriggerTarget.Bearer, Flat(SpellEffectType.Heal, 1));
            perTurn.Limit = TriggerLimit.OncePerTurn;
            var hero = new Bearer("Paladin", 1, 100).With(perTurn);
            var rat = new Bearer("Rat", 1, 20, isHero: false);
            _units.AddRange(new ICombatUnit[] { hero, rat });

            _events.Publish(new TurnStarted { Unit = rat });
            Hit(rat, hero, 10);
            Hit(rat, hero, 10);
            _events.Publish(new TurnStarted { Unit = rat });
            Hit(rat, hero, 10);

            Assert.AreEqual(2, _reactions.Count);
        }

        [Test]
        public void Chance_IsRolled()
        {
            var never = When(TriggerKind.OnKill, TriggerTarget.Bearer, Flat(SpellEffectType.Buff, 2));
            never.Chance = 0f;   // the roll in SetUp is always 0, which only a chance above 0 passes
            var hero = new Bearer("Warrior", 10, 30).With(never);
            var rat = new Bearer("Rat", 1, 5, isHero: false);
            _units.AddRange(new ICombatUnit[] { hero, rat });

            Hit(hero, rat, 10);

            Assert.IsEmpty(_reactions);
        }

        [Test]
        public void FilterDamageType_IgnoresOtherElements()
        {
            var fireOnly = When(TriggerKind.OnDealDamage, TriggerTarget.Bearer, Flat(SpellEffectType.Heal, 1));
            fireOnly.FilterDamageType = true;
            fireOnly.DamageType = DamageType.Fire;
            var mage = new Bearer("Mage", 1, 100).With(fireOnly);
            var rat = new Bearer("Rat", 1, 100, isHero: false);
            _units.AddRange(new ICombatUnit[] { mage, rat });

            HealthOps.Damage(rat, 5, new HealthSource(mage, HealthCause.Ability, DamageType.Ice), _events);
            Assert.IsEmpty(_reactions);

            HealthOps.Damage(rat, 5, new HealthSource(mage, HealthCause.Ability, DamageType.Fire), _events);
            Assert.AreEqual(1, _reactions.Count);
        }

        [Test]
        public void OnAllyDefeated_ReachesTheSurvivingSide()
        {
            var vengeance = When(TriggerKind.OnAllyDefeated, TriggerTarget.Bearer, Flat(SpellEffectType.Buff, 3));
            var survivor = new Bearer("Ranger", 1, 30).With(vengeance);
            var fallen = new Bearer("Cleric", 1, 5);
            var orc = new Bearer("Orc", 10, 30, isHero: false).With(vengeance);   // other side: unmoved
            _units.AddRange(new ICombatUnit[] { survivor, fallen, orc });

            Hit(orc, fallen, 10);

            Assert.AreEqual(3, _tracker.GetBuffAmount(survivor, StatType.Strength));
            Assert.AreEqual(0, _tracker.GetBuffAmount(orc, StatType.Strength));
        }

        [Test]
        public void AKillingBlowsDeath_IsHeardBeforeAnyDeathItsReactionsCause()
        {
            // Hitting A also hits B (OnDealDamage), and both blows are lethal.
            var cleave = When(TriggerKind.OnDealDamage, TriggerTarget.RandomEnemy, Flat(SpellEffectType.Damage, 50));
            var hero = new Bearer("Warrior", 10, 100).With(cleave);
            var a = new Bearer("A", 1, 5, isHero: false);
            var b = new Bearer("B", 1, 5, isHero: false);
            _units.AddRange(new ICombatUnit[] { hero, a, b });
            var order = new List<string>();
            _events.Subscribe<UnitDefeated>(d => order.Add(d.Victim.DisplayName));

            Hit(hero, a, 10);   // the roll in SetUp is 0, so RandomEnemy picks the first living enemy: B

            CollectionAssert.AreEqual(new[] { "A", "B" }, order);
        }

        [Test]
        public void OnKill_DoesNotPayForAFriendlyKill()
        {
            var sword = When(TriggerKind.OnKill, TriggerTarget.Bearer, Flat(SpellEffectType.Buff, 2));
            var cultist = new Bearer("Cultist", 1, 30).With(sword);
            var ranger = new Bearer("Ranger", 1, 30);
            _units.AddRange(new ICombatUnit[] { cultist, ranger });

            HealthOps.Set(ranger, 0, new HealthSource(cultist, HealthCause.Sacrifice), _events);

            Assert.IsEmpty(_reactions, "giving up an ally is not a kill");
        }

        [Test]
        public void APoisonAReactionApplies_IsCreditedToTheBearer_NotToWhoseTurnItIs()
        {
            // "When hit, poison the attacker" fires on the attacker's own turn.
            var venom = When(TriggerKind.OnTakeDamage, TriggerTarget.Other,
                new SpellEffect
                {
                    EffectType = SpellEffectType.Debuff, Power = 50, PowerMode = PowerMode.Flat,
                    ScalingStat = StatType.None, BuffType = BuffType.Poisoned, Duration = 3
                });
            var hero = new Bearer("Rogue", 1, 100).With(venom);
            var rat = new Bearer("Rat", 1, 20, isHero: false);
            _units.AddRange(new ICombatUnit[] { hero, rat });
            UnitDefeated death = null;
            _events.Subscribe<UnitDefeated>(d => death = d);

            _tracker.BeginTurn(rat);
            Hit(rat, hero, 5);
            _tracker.TickBuffs(rat);   // the rat's turn ends: landed on its own turn, it ticks from the next
            _tracker.BeginTurn(rat);
            _tracker.ResolveOverTime(rat, Assets.Scripts.Cards.Buffs.TickTiming.StartOfTurn);

            Assert.IsNotNull(death, "the poison killed the rat");
            Assert.AreSame(hero, death.Killer);
        }

        [Test]
        public void TheResolver_RaisesAbilityUsed_SoBothLoopsDo()
        {
            var caster = new Bearer("Mage", 1, 30);
            var rat = new Bearer("Rat", 1, 30, isHero: false);
            var used = new List<AbilityUsed>();
            _events.Subscribe<AbilityUsed>(used.Add);
            var bolt = UnityEngine.ScriptableObject.CreateInstance<MagicSO>();
            bolt.Key = "bolt";
            bolt.Effects = new List<SpellEffect> { Flat(SpellEffectType.Damage, 3) };
            bolt.Tags = new List<MagicTag>();

            _resolver.Execute(new SpellcastAction { Magic = bolt, Caster = caster, Targets = new List<ICombatUnit> { rat } }, _tracker);

            Assert.AreEqual(1, used.Count);
            Assert.AreSame(caster, used[0].Caster);
            Assert.AreSame(bolt, used[0].Ability);
            UnityEngine.Object.DestroyImmediate(bolt);
        }

        // ------------------------------------------------------------------ in the balance model

        [Test]
        public void Simulator_FiresAnEnemysDeathReaction()
        {
            SimUnit Make(string name, bool isHero, int str, int hp)
            {
                return new SimUnit
                {
                    DisplayName = name,
                    HeroKey = isHero ? name : null,
                    IsHero = isHero,
                    Archetype = EnemyArchetype.Aggressor,
                    Stats = TestStats.Make(str, 0, hp),
                    Effective = TestStats.Block(str, 0, hp),
                    AttackStat = StatType.Strength,
                    EffectiveAttackPower = str,
                    Resistances = new List<Resistance>()
                };
            }

            var hero = Make("Warrior", true, 40, 100);
            var party = new PartyBaseline { SourceLabel = "test" };
            party.Heroes.Add(new HeroBaseline { Effective = TestStats.Block(40, 0, 100), Unit = hero });
            var settings = new SimSettings { Trials = 20, Seed = 7, MaxTurns = 200, Policy = SimPolicy.AttackOnly, Combos = new List<MagicComboSO>() };

            var plain = Make("Imp", false, 1, 10);
            var bomb = Make("Imp", false, 1, 10);
            bomb.Triggers.Add(new CarriedTrigger(
                When(TriggerKind.OnDefeated, TriggerTarget.AllEnemies, Flat(SpellEffectType.Damage, 30)), "Imp"));

            var calm = EncounterSimulator.Run(party, new List<SimUnit> { plain }, settings);
            var loud = EncounterSimulator.Run(party, new List<SimUnit> { bomb }, settings);

            Assert.Greater(calm.AverageEndHealthFraction - loud.AverageEndHealthFraction, 0.25f,
                "the death blast reached the party in the model, as it would in play");
        }
    }
}
