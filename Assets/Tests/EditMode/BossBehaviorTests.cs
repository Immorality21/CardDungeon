using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The Boss preset, driven through <see cref="EnemyActionPlanner"/>.
    ///
    /// <para>Every assertion here predates behaviours becoming data, and is kept verbatim on
    /// purpose: the preset has to reproduce the old <c>BossBehavior</c> exactly. Enrage is the
    /// interesting case — it used to be an <c>if</c> inside the class that tightened the signature
    /// cadence from 3 to 2 and multiplied ordinary blows by 1.5, and it is now four authored entries
    /// whose health conditions make exactly one of each pair eligible.</para>
    /// </summary>
    public class BossBehaviorTests
    {
        // Cadence and enrage numbers the preset must keep. They used to be consts on BossBehavior.
        private const int SignatureInterval = 3;
        private const int EnragedSignatureInterval = 2;
        private const float SignatureMultiplier = 1.6f;
        private const float EnrageAttackMultiplier = 1.5f;

        private MockCombatUnit _self;   // 100 max HP so HP fraction is easy to set
        private MockCombatUnit _hero;

        [SetUp]
        public void SetUp()
        {
            _self = new MockCombatUnit("Boss", strength: 12, endurance: 4, health: 100, isHero: false);
            _hero = new MockCombatUnit("Knight", strength: 10, endurance: 5, health: 100);
        }

        private EnemyCombatContext Context(int turnCount, int chargingEntry = EnemyActionPlanner.NoCharge)
        {
            return new EnemyCombatContext
            {
                Heroes = new List<ICombatUnit> { _hero },
                Allies = new List<ICombatUnit>(),
                BuffTracker = new CombatBuffTracker(),
                ChargingEntryIndex = chargingEntry,
                SelfTurnCount = turnCount
            };
        }

        private EnemyDecision Plan(EnemyCombatContext context)
        {
            return EnemyActionPlanner.Plan(
                _self, context, EnemyBehaviorSO.BuiltInPreset(EnemyArchetype.Boss),
                new EnemyPlanRolls { Tier = 0f, Target = 0f, Magic = 0f, Fallback = 0f });
        }

        [Test]
        public void FirstTurn_IsAPlainAttack()
        {
            var decision = Plan(Context(turnCount: 0));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
            Assert.AreSame(_hero, decision.Target);
            Assert.AreEqual(1f, decision.Multiplier);
        }

        [Test]
        public void OffCadence_Attacks()
        {
            var decision = Plan(Context(turnCount: 1));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void OnCadence_ChargesSignature()
        {
            var decision = Plan(Context(turnCount: SignatureInterval));

            Assert.AreEqual(EnemyActionType.ChargeAoe, decision.Type);
        }

        [Test]
        public void Charging_DeliversAoeWithMultiplier()
        {
            var wind = Plan(Context(turnCount: SignatureInterval));
            var decision = Plan(Context(turnCount: SignatureInterval + 1, chargingEntry: wind.EntryIndex));

            Assert.AreEqual(EnemyActionType.AoeAttack, decision.Type);
            Assert.AreEqual(SignatureMultiplier, decision.Multiplier, 0.0001f);
        }

        [Test]
        public void Enraged_AttacksHarder()
        {
            // Health, not MaxHealth: the enrage check is Health/MaxHealth, so shrinking the bar
            // under a full one reads as 500% health rather than 20%.
            _self.Stats.Health = 20; // 20% of 100 → below the enrage threshold

            var decision = Plan(Context(turnCount: 1));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
            Assert.AreEqual(EnrageAttackMultiplier, decision.Multiplier, 0.0001f);
        }

        [Test]
        public void NotEnraged_AttacksAtBaseMultiplier()
        {
            // The mirror of the above: the two Attack entries are gated on opposite sides of the
            // threshold, so exactly one of them must ever be eligible.
            var decision = Plan(Context(turnCount: 1));

            Assert.AreEqual(1f, decision.Multiplier, 0.0001f);
        }

        [Test]
        public void Enraged_UsesTighterSignatureCadence()
        {
            _self.Stats.Health = 20; // enraged

            // EnragedSignatureInterval (2) triggers on a turn the normal interval (3) would not.
            var decision = Plan(Context(turnCount: EnragedSignatureInterval));

            Assert.AreEqual(EnemyActionType.ChargeAoe, decision.Type);
        }

        [Test]
        public void NotEnraged_IgnoresTheTighterCadence()
        {
            // Turn 2 is on the enraged cadence but not the normal one, so a healthy boss must swing.
            var decision = Plan(Context(turnCount: EnragedSignatureInterval));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void Charging_TakesPriorityOverEnrageCadence()
        {
            _self.Stats.Health = 20; // enraged

            var wind = Plan(Context(turnCount: EnragedSignatureInterval));
            var decision = Plan(Context(
                turnCount: EnragedSignatureInterval, chargingEntry: wind.EntryIndex));

            Assert.AreEqual(EnemyActionType.AoeAttack, decision.Type);
        }

        [Test]
        public void MidTelegraph_IsTheOnlyCertainIntent()
        {
            // The intent icon must never guess. Mid-charge it is certain; a healthy boss off-cadence
            // has only one ungated action available, so that is knowable too.
            var wind = Plan(Context(turnCount: SignatureInterval));
            var behavior = EnemyBehaviorSO.BuiltInPreset(EnemyArchetype.Boss);

            Assert.AreEqual(EnemyActionType.AoeAttack,
                EnemyActionPlanner.PredictCertain(
                    _self, Context(turnCount: SignatureInterval + 1, chargingEntry: wind.EntryIndex), behavior));

            Assert.AreEqual(EnemyActionType.Attack,
                EnemyActionPlanner.PredictCertain(_self, Context(turnCount: 1), behavior));
        }
    }
}
