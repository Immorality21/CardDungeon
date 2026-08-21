using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Items;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class EnemyBehaviorTests
    {
        private MockCombatUnit _self;
        private MockCombatUnit _hero;
        private CombatBuffTracker _buffTracker;

        [SetUp]
        public void SetUp()
        {
            _self = new MockCombatUnit("Enemy", attack: 5, defense: 2, health: 30, isHero: false);
            _hero = new MockCombatUnit("Knight", attack: 10, defense: 5, health: 100);
            _buffTracker = new CombatBuffTracker();
        }

        private EnemyCombatContext Context(
            List<ICombatUnit> heroes = null,
            List<ICombatUnit> allies = null,
            bool charging = false)
        {
            return new EnemyCombatContext
            {
                Heroes = heroes ?? new List<ICombatUnit> { _hero },
                Allies = allies ?? new List<ICombatUnit>(),
                BuffTracker = _buffTracker,
                SelfIsCharging = charging
            };
        }

        [Test]
        public void Aggressor_Attacks_ALivingHero()
        {
            var decision = new AggressorBehavior().Decide(_self, Context());

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
            Assert.AreSame(_hero, decision.Target);
        }

        [Test]
        public void Bruiser_NotCharging_Charges()
        {
            var decision = new BruiserBehavior().Decide(_self, Context(charging: false));

            Assert.AreEqual(EnemyActionType.ChargeHeavy, decision.Type);
            Assert.AreSame(_hero, decision.Target);
        }

        [Test]
        public void Bruiser_Charging_HeavyAttackWithMultiplier()
        {
            var decision = new BruiserBehavior().Decide(_self, Context(charging: true));

            Assert.AreEqual(EnemyActionType.HeavyAttack, decision.Type);
            Assert.Greater(decision.Multiplier, 1f);
        }

        [Test]
        public void Healer_HealsMostWoundedAlly()
        {
            var hurt = new MockCombatUnit("Goblin", attack: 4, defense: 1, health: 20, isHero: false);
            hurt.Stats.Health = 5; // wounded
            var healthy = new MockCombatUnit("Orc", attack: 6, defense: 2, health: 25, isHero: false);

            var decision = new HealerBehavior().Decide(
                _self, Context(allies: new List<ICombatUnit> { hurt, healthy }));

            Assert.AreEqual(EnemyActionType.Heal, decision.Type);
            Assert.AreSame(hurt, decision.Target);
            Assert.Greater(decision.Amount, 0);
        }

        [Test]
        public void Healer_NoWounded_Attacks()
        {
            var fullAlly = new MockCombatUnit("Orc", attack: 6, defense: 2, health: 25, isHero: false);

            var decision = new HealerBehavior().Decide(
                _self, Context(allies: new List<ICombatUnit> { fullAlly }));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void Healer_SelfWounded_NoAllies_HealsSelf()
        {
            _self.Stats.Health = 10; // wounded, MaxHealth 30

            var decision = new HealerBehavior().Decide(_self, Context());

            Assert.AreEqual(EnemyActionType.Heal, decision.Type);
            Assert.AreSame(_self, decision.Target);
        }

        [Test]
        public void Debuffer_WeakensUndebuffedHero()
        {
            var decision = new DebufferBehavior().Decide(_self, Context());

            Assert.AreEqual(EnemyActionType.Debuff, decision.Type);
            Assert.AreSame(_hero, decision.Target);
            Assert.AreEqual(StatType.Strength, decision.DebuffStat);
            Assert.Greater(decision.Amount, 0);
        }

        [Test]
        public void Debuffer_AllHeroesAlreadyWeakened_Attacks()
        {
            _buffTracker.ApplyBuff(_hero, StatType.Strength, -2, 3); // already weakened

            var decision = new DebufferBehavior().Decide(_self, Context());

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }
    }
}
