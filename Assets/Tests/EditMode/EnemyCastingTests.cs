using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Enemies cast from the same list the player draws from. These pin the four rules that make
    /// that safe: the roll gates it, charges are never spent, a charging enemy delivers its charge
    /// instead, and spell power rides the level's Difficulty exactly as Strength does.
    /// </summary>
    public class EnemyCastingTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _created.Clear();
        }

        private MagicSO Magic(string key, int power, MagicTargetType target = MagicTargetType.SingleEnemy,
            SpellEffectType type = SpellEffectType.Damage, StatType scaling = StatType.None)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            _created.Add(magic);
            magic.Key = key;
            magic.DisplayName = key;
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>
            {
                new SpellEffect
                {
                    EffectType = type,
                    Power = power,
                    ScalingStat = scaling,
                    DamageType = DamageType.Normal
                }
            };
            return magic;
        }

        private EnemySO Enemy(float castChance, params DrawableMagicEntry[] entries)
        {
            var enemy = ScriptableObject.CreateInstance<EnemySO>();
            _created.Add(enemy);
            enemy.DisplayName = "Caster";
            enemy.BaseStats[StatType.Strength] = 6;
            enemy.BaseStats[StatType.MaxHealth] = 30;
            enemy.BaseStats[StatType.Endurance] = 2;
            enemy.BaseStats[StatType.Agility] = 5;
            enemy.DrawableMagics = new List<DrawableMagicEntry>(entries);
            enemy.Behavior = Caster(castChance);
            return enemy;
        }

        /// <summary>
        /// Cast frequency is an authored action, not a field: a gated CastMagic entry above a plain
        /// swing. This is what EnemySO.MagicCastChance became.
        /// </summary>
        private EnemyBehaviorSO Caster(float castChance)
        {
            var behavior = ScriptableObject.CreateInstance<EnemyBehaviorSO>();
            _created.Add(behavior);
            behavior.Actions = new List<EnemyActionEntry>();
            if (castChance > 0f)
            {
                // ChanceGate 0 means "no gate", not "never" - a 0% caster is a behaviour with no cast
                // action at all, which is also how the presets express it.
                behavior.Actions.Add(EnemyBehaviorSO.CastFromDrawList(castChance));
            }
            behavior.Actions.Add(new EnemyActionEntry { Kind = EnemyActionKind.Attack });
            return behavior;
        }

        private DrawableMagicEntry Entry(MagicSO magic, int charges = 3, float weight = 1f)
        {
            return new DrawableMagicEntry { Magic = magic, Charges = charges, CastWeight = weight };
        }

        // ------------------------------------------------------------------ the cast roll

        [Test]
        public void ShouldCast_RollUnderChance_Casts()
        {
            var magics = new List<DrawableMagicEntry> { Entry(Magic("Fireball", 8)) };
            Assert.IsTrue(EnemyMagicPlan.ShouldCast(0.30f, magics, false, 0.29f));
        }

        [Test]
        public void ShouldCast_RollAtOrOverChance_DoesNotCast()
        {
            var magics = new List<DrawableMagicEntry> { Entry(Magic("Fireball", 8)) };
            Assert.IsFalse(EnemyMagicPlan.ShouldCast(0.30f, magics, false, 0.30f));
        }

        [Test]
        public void ShouldCast_ZeroChance_NeverCasts()
        {
            var magics = new List<DrawableMagicEntry> { Entry(Magic("Fireball", 8)) };
            Assert.IsFalse(EnemyMagicPlan.ShouldCast(0f, magics, false, 0f));
        }

        [Test]
        public void ShouldCast_WhileCharging_DeliversTheChargeInstead()
        {
            // The charge has already been telegraphed to the player; swallowing it would make the
            // telegraph a lie, so a charging enemy never casts however high its chance is.
            var magics = new List<DrawableMagicEntry> { Entry(Magic("Fireball", 8)) };
            Assert.IsFalse(EnemyMagicPlan.ShouldCast(1f, magics, true, 0f));
        }

        [Test]
        public void ShouldCast_NoMagicToCast_DoesNotCast()
        {
            Assert.IsFalse(EnemyMagicPlan.ShouldCast(1f, null, false, 0f));
            Assert.IsFalse(EnemyMagicPlan.ShouldCast(1f, new List<DrawableMagicEntry>(), false, 0f));
        }

        [Test]
        public void ShouldCast_EntryWithNoEffects_IsNotCastable()
        {
            var empty = ScriptableObject.CreateInstance<MagicSO>();
            _created.Add(empty);
            empty.Effects = new List<SpellEffect>();

            var magics = new List<DrawableMagicEntry> { Entry(empty) };
            Assert.IsFalse(EnemyMagicPlan.ShouldCast(1f, magics, false, 0f));
        }

        // ------------------------------------------------------------------ which magic

        [Test]
        public void Select_WeightsBiasTheChoice()
        {
            var rare = Magic("Rare", 5);
            var common = Magic("Common", 5);
            var magics = new List<DrawableMagicEntry> { Entry(rare, weight: 1f), Entry(common, weight: 9f) };

            // Total weight 10: the first tenth picks the rare entry, the rest the common one.
            Assert.AreEqual(rare, EnemyMagicPlan.Select(magics, 0.05f));
            Assert.AreEqual(common, EnemyMagicPlan.Select(magics, 0.5f));
            Assert.AreEqual(common, EnemyMagicPlan.Select(magics, 0.99f));
        }

        [Test]
        public void Select_AllWeightsZero_IsUniform()
        {
            // CastWeight was added to a type already serialized on every enemy asset, so existing
            // entries deserialize to 0 rather than to the C# initializer. Uniform is the behaviour
            // those assets have to get.
            var first = Magic("First", 5);
            var second = Magic("Second", 5);
            var magics = new List<DrawableMagicEntry> { Entry(first, weight: 0f), Entry(second, weight: 0f) };

            Assert.AreEqual(first, EnemyMagicPlan.Select(magics, 0.1f));
            Assert.AreEqual(second, EnemyMagicPlan.Select(magics, 0.9f));
        }

        [Test]
        public void Select_RollAtOne_StillReturnsAMagic()
        {
            var magics = new List<DrawableMagicEntry> { Entry(Magic("A", 5)), Entry(Magic("B", 5)) };
            Assert.IsNotNull(EnemyMagicPlan.Select(magics, 1f));
        }

        // ------------------------------------------------------------------ targeting mirrors

        [Test]
        public void ResolveTargets_SingleEnemy_PicksAHero()
        {
            var self = Unit("Enemy", hero: false);
            var hero = Unit("Hero", hero: true);

            var targets = EnemyMagicPlan.ResolveTargets(
                Magic("Fireball", 8, MagicTargetType.SingleEnemy),
                self, new List<ICombatUnit> { hero }, new List<ICombatUnit>(), 0f);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(hero, targets[0]);
        }

        [Test]
        public void ResolveTargets_AllEnemies_HitsEveryLivingHero()
        {
            var self = Unit("Enemy", hero: false);
            var alive = Unit("Alive", hero: true);
            var dead = Unit("Dead", hero: true);
            dead.Stats.Health = 0;

            var targets = EnemyMagicPlan.ResolveTargets(
                Magic("Storm", 8, MagicTargetType.AllEnemies),
                self, new List<ICombatUnit> { alive, dead }, new List<ICombatUnit>(), 0f);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(alive, targets[0]);
        }

        [Test]
        public void ResolveTargets_SingleAlly_PicksTheMostWoundedOfSelfAndAllies()
        {
            // TargetType is authored from the player's point of view, so "ally" is the monster side.
            var self = Unit("Enemy", hero: false);
            var hurtAlly = Unit("Hurt", hero: false);
            hurtAlly.Stats.Health = 2;

            var targets = EnemyMagicPlan.ResolveTargets(
                Magic("Heal", 8, MagicTargetType.SingleAlly, SpellEffectType.Heal),
                self, new List<ICombatUnit> { Unit("Hero", hero: true) },
                new List<ICombatUnit> { hurtAlly }, 0f);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(hurtAlly, targets[0]);
        }

        [Test]
        public void ResolveTargets_Self_TargetsOnlyItself()
        {
            var self = Unit("Enemy", hero: false);

            var targets = EnemyMagicPlan.ResolveTargets(
                Magic("ShieldUp", 3, MagicTargetType.Self, SpellEffectType.Buff),
                self, new List<ICombatUnit> { Unit("Hero", hero: true) }, new List<ICombatUnit>(), 0f);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(self, targets[0]);
        }

        [Test]
        public void ResolveTargets_AllAllies_IncludesSelf()
        {
            var self = Unit("Enemy", hero: false);
            var ally = Unit("Ally", hero: false);

            var targets = EnemyMagicPlan.ResolveTargets(
                Magic("WarCry", 3, MagicTargetType.AllAllies, SpellEffectType.Buff),
                self, new List<ICombatUnit>(), new List<ICombatUnit> { ally }, 0f);

            Assert.AreEqual(2, targets.Count);
            Assert.Contains(self, targets);
            Assert.Contains(ally, targets);
        }

        // ------------------------------------------------------------------ level power scaling

        [Test]
        public void MagicPowerScale_IsTheLevelDifficulty()
        {
            var enemy = Enemy(0.25f, Entry(Magic("Fireball", 8)));
            var tuning = new LevelEnemyTuning { Difficulty = 2.5f };

            Assert.AreEqual(2.5f, LevelEnemyTuning.MagicPowerScaleFor(enemy, tuning), 0.0001f);
        }

        [Test]
        public void MagicPowerScale_NoTuning_IsOne()
        {
            var enemy = Enemy(0.25f, Entry(Magic("Fireball", 8)));
            Assert.AreEqual(1f, LevelEnemyTuning.MagicPowerScaleFor(enemy, null), 0.0001f);
        }

        [Test]
        public void MagicPowerScale_EnemyWithAbsoluteOverride_DoesNotScale()
        {
            // An Overrides row means "this level's dial does not apply to this enemy" - it is how
            // bosses are kept off the trash dial - so its spells stay on their authored power for the
            // same reason its Strength does.
            var enemy = Enemy(0.25f, Entry(Magic("Fireball", 8)));
            var tuning = new LevelEnemyTuning
            {
                Difficulty = 3f,
                Overrides = new List<EnemyStatOverride> { new EnemyStatOverride { Enemy = enemy } }
            };

            Assert.AreEqual(1f, LevelEnemyTuning.MagicPowerScaleFor(enemy, tuning), 0.0001f);
        }

        [Test]
        public void ScalePower_RoundsAndNeverReachesZero()
        {
            Assert.AreEqual(20, EnemyMagicPlan.ScalePower(8, 2.5f));
            Assert.AreEqual(8, EnemyMagicPlan.ScalePower(8, 1f));
            Assert.AreEqual(1, EnemyMagicPlan.ScalePower(1, 0.1f), "A scaled-down spell must not become free.");
            Assert.AreEqual(0, EnemyMagicPlan.ScalePower(0, 3f), "Nothing scaled is still nothing.");
        }

        [Test]
        public void Resolver_PowerScale_MultipliesDamageButNotBuffs()
        {
            var resolver = new EffectResolver();
            var tracker = new CombatBuffTracker();

            var damage = Magic("Bolt", 10);
            var target = Unit("Hero", hero: true);
            target.Stats[StatType.Endurance] = 0;
            int before = target.Stats.Health;

            resolver.Execute(
                new SpellcastAction { Magic = damage, Caster = Unit("Enemy", hero: false), Targets = new List<ICombatUnit> { target } },
                tracker, null, null, 0, 0, null, 2f);

            Assert.AreEqual(20, before - target.Stats.Health, "Power 10 at scale 2 should land 20 against 0 Endurance.");

            // A buff's Power is a stat delta, not a damage number, so the level scale leaves it alone -
            // the same rule the magic-upgrade bonus already follows.
            var buff = Magic("ShieldUp", 4, MagicTargetType.Self, SpellEffectType.Buff);
            buff.Effects[0].BuffType = BuffType.Endurance;
            var caster = Unit("Enemy", hero: false);

            resolver.Execute(
                new SpellcastAction { Magic = buff, Caster = caster, Targets = new List<ICombatUnit> { caster } },
                tracker, null, null, 0, 0, null, 3f);

            Assert.AreEqual(4, tracker.GetBuffAmount(caster, StatType.Endurance));
        }

        // ------------------------------------------------------------------ charges are never spent

        [Test]
        public void Casting_DoesNotTouchTheDrawCharges()
        {
            // Charges are the player's Draw grant. An enemy casting from the same list is free, the
            // way the FF games this system is modelled on treat it - so nothing in the cast path may
            // read or write them.
            var entry = Entry(Magic("Fireball", 8), charges: 3);
            var magics = new List<DrawableMagicEntry> { entry };

            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(EnemyMagicPlan.ShouldCast(1f, magics, false, 0f));
                Assert.IsNotNull(EnemyMagicPlan.Select(magics, 0.5f));
            }

            Assert.AreEqual(3, entry.Charges);
        }

        // ------------------------------------------------------------------ the balance model

        [Test]
        public void Model_PricesTheCastAtTheLevelsSpellScale()
        {
            var enemy = Enemy(0.4f, Entry(Magic("Fireball", 10)));
            var tuning = new LevelEnemyTuning { Difficulty = 2f };
            var caster = SimUnit.FromEnemy(enemy, tuning);
            var heroes = new List<SimUnit> { Hero(endurance: 0) };

            var profile = EnemyMagicModel.Profile(enemy, tuning, caster, heroes);

            Assert.AreEqual(0.4f, profile.CastChance, 0.0001f);
            Assert.AreEqual(1, profile.CastableCount);
            Assert.AreEqual(20f, profile.ExpectedDamage, 0.51f, "Power 10 at Difficulty 2 against 0 Endurance.");
        }

        [Test]
        public void Model_ZeroCastChance_PricesNothing()
        {
            var enemy = Enemy(0f, Entry(Magic("Fireball", 10)));
            var caster = SimUnit.FromEnemy(enemy, null);
            var profile = EnemyMagicModel.Profile(enemy, null, caster, new List<SimUnit> { Hero() });

            Assert.IsFalse(profile.Casts, "A 0% caster must read exactly as it did before casting existed.");
        }

        [Test]
        public void Model_PartyWideCast_CountsEveryHero()
        {
            var enemy = Enemy(1f, Entry(Magic("Storm", 10, MagicTargetType.AllEnemies)));
            var caster = SimUnit.FromEnemy(enemy, null);
            var heroes = new List<SimUnit> { Hero(endurance: 0), Hero(endurance: 0), Hero(endurance: 0) };

            var profile = EnemyMagicModel.Profile(enemy, null, caster, heroes);

            Assert.AreEqual(30f, profile.ExpectedDamage, 1f, "10 power on each of three heroes.");
        }

        [Test]
        public void Model_HealMagic_IsHealingNotDamage()
        {
            var enemy = Enemy(1f, Entry(Magic("Heal", 12, MagicTargetType.SingleAlly, SpellEffectType.Heal)));
            var caster = SimUnit.FromEnemy(enemy, null);

            var profile = EnemyMagicModel.Profile(enemy, null, caster, new List<SimUnit> { Hero() });

            Assert.AreEqual(0f, profile.ExpectedDamage, 0.0001f);
            Assert.AreEqual(12f, profile.ExpectedHealing, 0.51f);
        }

        [Test]
        public void Model_CastChanceBlendsWithTheAttackRatherThanAddingToIt()
        {
            // Casting is an alternative to attacking, not an addition. A 100% caster's damage per
            // turn is its spell, not its spell plus its swing.
            var enemy = Enemy(1f, Entry(Magic("Fireball", 30)));
            var tuning = new LevelEnemyTuning { Difficulty = 1f };
            var caster = SimUnit.FromEnemy(enemy, tuning);
            var heroes = new List<SimUnit> { Hero(endurance: 0) };

            float withCasting = BalanceMath.DamagePerTick(caster, heroes, heroes.Count);

            enemy.Behavior = Caster(0f);
            var attackOnly = SimUnit.FromEnemy(enemy, tuning);
            float withoutCasting = BalanceMath.DamagePerTick(attackOnly, heroes, heroes.Count);

            Assert.Greater(withCasting, withoutCasting,
                "A 30-power spell should beat a Strength-6 swing.");

            float perTurn = withCasting / BalanceMath.TurnsPerTick(caster);
            Assert.AreEqual(30f, perTurn, 1.5f, "At 100% cast chance the swing contributes nothing.");
        }

        // ------------------------------------------------------------------ helpers

        private MockCombatUnit Unit(string name, bool hero)
        {
            var unit = new MockCombatUnit(name, 5, 2, 40, 5) { IsHero = hero };
            return unit;
        }

        private SimUnit Hero(int endurance = 2)
        {
            var stats = StatBlock.Defaults();
            stats[StatType.Strength] = 8;
            stats[StatType.Endurance] = endurance;
            stats[StatType.MaxHealth] = 40;
            stats[StatType.Agility] = 5;

            return new SimUnit
            {
                DisplayName = "Hero",
                IsHero = true,
                Stats = new Stats(stats),
                Effective = stats.Clone(),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = stats[StatType.Strength]
            };
        }
    }
}
