using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// What the party expects to be hit *with*. This is the number that makes a resistance
    /// priceable: <see cref="GearLoadout"/> used to rank items on their stat line alone, so the Ruby
    /// Amulet's 25% Fire was bought for its Strength and its ward counted for nothing.
    /// (<c>docs/BALANCING.md</c> §5q.)
    /// </summary>
    public class IncomingDamageMixTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

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

        private SimUnit Enemy(DamageType element, int attackPower)
        {
            return new SimUnit
            {
                DisplayName = element + "-" + attackPower,
                IsHero = false,
                AttackDamageType = element,
                EffectiveAttackPower = attackPower
            };
        }

        // --- shares ----------------------------------------------------------------------

        [Test]
        public void ShareOf_OneElement_IsEverything()
        {
            var mix = IncomingDamageMix.FromUnits(new List<SimUnit> { Enemy(DamageType.Fire, 10) });

            Assert.AreEqual(1f, mix.ShareOf(DamageType.Fire), 0.001f);
            Assert.AreEqual(0f, mix.ShareOf(DamageType.Ice), 0.001f);
        }

        /// <summary>
        /// Weighted by attack power, not headcount: the party is choosing armour against the damage
        /// it will actually take, and a Dragon standing beside a Floating Eye is most of that damage.
        /// </summary>
        [Test]
        public void ShareOf_WeighsEnemiesByAttackPowerRatherThanCount()
        {
            var mix = IncomingDamageMix.FromUnits(new List<SimUnit>
            {
                Enemy(DamageType.Fire, 30),
                Enemy(DamageType.Ice, 10)
            });

            Assert.AreEqual(0.75f, mix.ShareOf(DamageType.Fire), 0.001f);
            Assert.AreEqual(0.25f, mix.ShareOf(DamageType.Ice), 0.001f);
        }

        [Test]
        public void FromRooms_SumsEveryRoomOnTheFloor()
        {
            var mix = IncomingDamageMix.FromRooms(new List<IList<SimUnit>>
            {
                new List<SimUnit> { Enemy(DamageType.Fire, 10) },
                new List<SimUnit> { Enemy(DamageType.Fire, 10), Enemy(DamageType.Shadow, 20) }
            });

            Assert.AreEqual(0.5f, mix.ShareOf(DamageType.Fire), 0.001f);
            Assert.AreEqual(0.5f, mix.ShareOf(DamageType.Shadow), 0.001f);
        }

        /// <summary>Heroes are not the threat. Counting them would dilute every enemy's share.</summary>
        [Test]
        public void FromUnits_SkipsHeroes()
        {
            var hero = Enemy(DamageType.Normal, 100);
            hero.IsHero = true;

            var mix = IncomingDamageMix.FromUnits(new List<SimUnit> { hero, Enemy(DamageType.Fire, 10) });

            Assert.AreEqual(1f, mix.ShareOf(DamageType.Fire), 0.001f);
            Assert.AreEqual(0f, mix.ShareOf(DamageType.Normal), 0.001f);
        }

        /// <summary>
        /// Nothing known means resistance prices at nothing, not at a guess. An invented default
        /// would be a second balance lever nobody set.
        /// </summary>
        [Test]
        public void EmptyMix_SharesNothingRatherThanSpreadingEvenly()
        {
            var mix = IncomingDamageMix.FromUnits(new List<SimUnit>());

            Assert.IsTrue(mix.IsEmpty);
            foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
            {
                Assert.AreEqual(0f, mix.ShareOf(type), 0.001f);
            }
        }

        [Test]
        public void FromRooms_NullIsEmptyRatherThanThrowing()
        {
            Assert.IsTrue(IncomingDamageMix.FromRooms(null).IsEmpty);
            Assert.IsTrue(IncomingDamageMix.FromUnits(null).IsEmpty);
            Assert.IsTrue(IncomingDamageMix.FromEnemies(null).IsEmpty);
        }

        // --- casts -----------------------------------------------------------------------

        private MagicSO Spell(string key, DamageType element, MagicTargetType target = MagicTargetType.SingleEnemy)
        {
            var magic = Make<MagicSO>();
            magic.Key = key;
            magic.DisplayName = key;
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, DamageType = element, Power = 10 }
            };
            return magic;
        }

        /// <summary>
        /// An enemy that casts is hitting the party with two elements, and armour has to be chosen
        /// against both. The split is the behaviour's cast share, so a Hex Weaver's spells count for
        /// as much of the mix as it spends turns casting them.
        /// </summary>
        [Test]
        public void AnEnemyThatCasts_SplitsBetweenItsSwingAndItsSpell()
        {
            var fire = Spell("Fire", DamageType.Fire);
            var definition = Make<EnemySO>();
            definition.Spells = new List<EnemySpellEntry>
            {
                new EnemySpellEntry { Magic = fire, CastWeight = 1f }
            };

            var behavior = Make<Assets.Scripts.Enemies.Behaviors.EnemyBehaviorSO>();
            behavior.Actions = new List<Assets.Scripts.Enemies.Behaviors.EnemyActionEntry>
            {
                new Assets.Scripts.Enemies.Behaviors.EnemyActionEntry
                {
                    Kind = Assets.Scripts.Enemies.Behaviors.EnemyActionKind.CastMagic, Weight = 1f
                },
                new Assets.Scripts.Enemies.Behaviors.EnemyActionEntry
                {
                    Kind = Assets.Scripts.Enemies.Behaviors.EnemyActionKind.Attack, Weight = 1f
                }
            };

            var unit = Enemy(DamageType.Ice, 10);
            unit.Definition = definition;
            unit.Behavior = behavior;

            var mix = IncomingDamageMix.FromUnits(new List<SimUnit> { unit });

            Assert.Greater(mix.ShareOf(DamageType.Fire), 0f,
                "A spell the enemy actually casts is damage the party takes.");
            Assert.Greater(mix.ShareOf(DamageType.Ice), 0f,
                "It still swings on the turns it does not cast.");
            Assert.AreEqual(
                1f,
                mix.ShareOf(DamageType.Fire) + mix.ShareOf(DamageType.Ice),
                0.001f,
                "The two together are the whole of what this enemy deals.");
        }

        /// <summary>
        /// A heal aimed at the enemy's own side is not incoming damage, however much it prolongs the
        /// fight — the same line <c>EnemyMagicModel</c> draws when it prices a cast.
        /// </summary>
        [Test]
        public void ASupportCast_DoesNotCountAsDamageTheHeroesTake()
        {
            var cure = Make<MagicSO>();
            cure.Key = "Cure";
            cure.TargetType = MagicTargetType.SingleAlly;
            cure.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Heal, Power = 10 }
            };

            var definition = Make<EnemySO>();
            definition.Spells = new List<EnemySpellEntry>
            {
                new EnemySpellEntry { Magic = cure, CastWeight = 1f }
            };

            var behavior = Make<Assets.Scripts.Enemies.Behaviors.EnemyBehaviorSO>();
            behavior.Actions = new List<Assets.Scripts.Enemies.Behaviors.EnemyActionEntry>
            {
                new Assets.Scripts.Enemies.Behaviors.EnemyActionEntry
                {
                    Kind = Assets.Scripts.Enemies.Behaviors.EnemyActionKind.CastMagic, Weight = 1f
                },
                new Assets.Scripts.Enemies.Behaviors.EnemyActionEntry
                {
                    Kind = Assets.Scripts.Enemies.Behaviors.EnemyActionKind.Attack, Weight = 1f
                }
            };

            var unit = Enemy(DamageType.Ice, 10);
            unit.Definition = definition;
            unit.Behavior = behavior;

            var mix = IncomingDamageMix.FromUnits(new List<SimUnit> { unit });

            Assert.AreEqual(1f, mix.ShareOf(DamageType.Ice), 0.001f,
                "With nothing damaging to cast, every turn's weight belongs to the swing.");
        }
    }
}
