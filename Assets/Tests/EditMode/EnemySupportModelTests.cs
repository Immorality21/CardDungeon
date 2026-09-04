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
    /// Healing, buffs and debuffs in the closed-form model.
    ///
    /// <para>All three used to price as **nothing**: a Healer read as harmless because its healing
    /// never entered the danger index, and a Shield Up or a hero debuff read as a wasted turn because
    /// a closed form had nowhere to put a stat delta. They now go through one channel — the rate at
    /// which the attacking side can actually clear the target side — and these pin that channel,
    /// including the case the old model could not express at all: a healer that cannot be killed.</para>
    /// </summary>
    public class EnemySupportModelTests
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

        // ------------------------------------------------------------------ builders

        private EnemyBehaviorSO Behavior(params EnemyActionEntry[] actions)
        {
            var behavior = ScriptableObject.CreateInstance<EnemyBehaviorSO>();
            _created.Add(behavior);
            behavior.Actions = new List<EnemyActionEntry>(actions);
            return behavior;
        }

        private EnemySO Enemy(EnemyBehaviorSO behavior, int health = 60, int strength = 6)
        {
            var enemy = ScriptableObject.CreateInstance<EnemySO>();
            _created.Add(enemy);
            enemy.DisplayName = "Target";
            enemy.BaseStats[StatType.Strength] = strength;
            enemy.BaseStats[StatType.Endurance] = 2;
            enemy.BaseStats[StatType.MaxHealth] = health;
            enemy.BaseStats[StatType.Agility] = 5;
            enemy.Behavior = behavior;
            return enemy;
        }

        private static SimUnit Hero(int strength = 10, int agility = 5)
        {
            var stats = StatBlock.Defaults();
            stats[StatType.Strength] = strength;
            stats[StatType.Endurance] = 2;
            stats[StatType.MaxHealth] = 40;
            stats[StatType.Agility] = agility;

            return new SimUnit
            {
                DisplayName = "Hero",
                IsHero = true,
                Stats = new Stats(stats),
                Effective = stats.Clone(),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = strength
            };
        }

        private static EnemyActionEntry Swing()
        {
            return new EnemyActionEntry { Kind = EnemyActionKind.Attack };
        }

        // ------------------------------------------------------------------ healing

        [Test]
        public void Healing_ShortensNothingButLengthensTheFight()
        {
            var plain = Enemy(Behavior(Swing()));
            var healer = Enemy(Behavior(
                new EnemyActionEntry { Kind = EnemyActionKind.Heal, Priority = 10, Power = 6 },
                Swing()));

            var party = new List<SimUnit> { Hero(), Hero() };

            float plainTicks = BalanceMath.TicksToClear(party, new List<SimUnit> { SimUnit.FromEnemy(plain, null) });
            float healTicks = BalanceMath.TicksToClear(party, new List<SimUnit> { SimUnit.FromEnemy(healer, null) });

            Assert.Greater(healTicks, plainTicks,
                "An enemy that heals itself takes longer to clear than an identical one that does not.");
        }

        [Test]
        public void Healing_FasterThanThePartyCanDealIt_IsUnwinnable()
        {
            // The outcome the old model could not express: healing was priced at zero, so a healer
            // that out-heals the party still read as a finite, winnable fight.
            var healer = Enemy(Behavior(
                new EnemyActionEntry { Kind = EnemyActionKind.Heal, Priority = 10, Power = 500 }));

            var party = new List<SimUnit> { Hero(strength: 3) };
            var targets = new List<SimUnit> { SimUnit.FromEnemy(healer, null) };

            Assert.IsTrue(float.IsInfinity(BalanceMath.TicksToClear(party, targets)),
                "The party cannot out-damage the healing, so it never clears it.");

            // And the trap worth knowing about: the danger index measures a *damage race*, so an enemy
            // that cannot kill the party either reads as 0.00 - the safest-looking number there is for
            // an unwinnable fight. That is why the analyzer has a separate critical for it rather than
            // relying on danger.
            Assert.AreEqual(0f, BalanceMath.DangerIndex(party, targets), 0.0001f);
        }

        [Test]
        public void Healing_IsZeroForHeroes()
        {
            // Heroes have no behaviour, so the same code path must contribute nothing rather than
            // needing a special case.
            Assert.AreEqual(0f, BalanceMath.SustainPerTick(Hero()), 0.0001f);
        }

        [Test]
        public void Healing_ScalesWithTurnRate()
        {
            var slow = Enemy(Behavior(new EnemyActionEntry { Kind = EnemyActionKind.Heal, Priority = 10, Power = 6 }));
            slow.BaseStats[StatType.Agility] = 5;
            var fast = Enemy(Behavior(new EnemyActionEntry { Kind = EnemyActionKind.Heal, Priority = 10, Power = 6 }));
            fast.BaseStats[StatType.Agility] = 10;

            float slowSustain = BalanceMath.SustainPerTick(SimUnit.FromEnemy(slow, null));
            float fastSustain = BalanceMath.SustainPerTick(SimUnit.FromEnemy(fast, null));

            Assert.AreEqual(2f, fastSustain / slowSustain, 0.01f,
                "Twice the Agility is twice the healing per tick, same as it is twice the damage.");
        }

        // ------------------------------------------------------------------ debuffs on the party

        [Test]
        public void HeroDebuff_CutsPartyOutput()
        {
            var plain = Enemy(Behavior(Swing()));
            var debuffer = Enemy(Behavior(
                new EnemyActionEntry
                {
                    Kind = EnemyActionKind.Debuff,
                    Priority = 10,
                    Power = 5,
                    Duration = 3,
                    TargetStat = StatType.Strength
                },
                Swing()));

            var party = new List<SimUnit> { Hero() };

            float none = BalanceMath.OutputSuppressionOf(party, SimUnit.FromEnemy(plain, null));
            float cut = BalanceMath.OutputSuppressionOf(party, SimUnit.FromEnemy(debuffer, null));

            Assert.AreEqual(1f, none, 0.0001f, "An enemy with no support must not suppress anything.");
            Assert.Less(cut, 1f, "A Strength debuff has to show up as reduced party output.");
        }

        [Test]
        public void HeroDebuff_OnAStatThatDoesNotAffectDamage_IsNotCharged()
        {
            // Suppression is measured through the real damage maths rather than assumed per stat, so a
            // debuff on something that does not touch output must come out neutral instead of being
            // credited a flat penalty.
            var spiritDebuffer = Enemy(Behavior(
                new EnemyActionEntry
                {
                    Kind = EnemyActionKind.Debuff,
                    Priority = 10,
                    Power = 5,
                    Duration = 3,
                    TargetStat = StatType.Spirit
                },
                Swing()));

            var party = new List<SimUnit> { Hero() };

            Assert.AreEqual(1f,
                BalanceMath.OutputSuppressionOf(party, SimUnit.FromEnemy(spiritDebuffer, null)), 0.0001f);
        }

        [Test]
        public void AgilityDebuff_CutsOutputThroughTurnRate()
        {
            var slower = Enemy(Behavior(
                new EnemyActionEntry
                {
                    Kind = EnemyActionKind.Debuff,
                    Priority = 10,
                    Power = 3,
                    Duration = 4,
                    TargetStat = StatType.Agility
                },
                Swing()));

            var party = new List<SimUnit> { Hero(agility: 8) };

            Assert.Less(BalanceMath.OutputSuppressionOf(party, SimUnit.FromEnemy(slower, null)), 1f,
                "Fewer hero turns is less party damage per tick, so an Agility debuff is real suppression.");
        }

        [Test]
        public void Uptime_IsCappedAtAlwaysOn()
        {
            Assert.AreEqual(1f, EnemyBehaviorModel.Uptime(0.5f, 4), 0.0001f,
                "Re-applying something already up does not stack it.");
            Assert.AreEqual(0.6f, EnemyBehaviorModel.Uptime(0.2f, 3), 0.0001f);
        }

        // ------------------------------------------------------------------ the enemy's own buffs

        [Test]
        public void SelfBuffFromACast_ShowsUpAsSuppressionOrOffense()
        {
            var shieldUp = ScriptableObject.CreateInstance<MagicSO>();
            _created.Add(shieldUp);
            shieldUp.Key = "ShieldUp";
            shieldUp.TargetType = MagicTargetType.Self;
            shieldUp.Effects = new List<SpellEffect>
            {
                new SpellEffect
                {
                    EffectType = SpellEffectType.Buff,
                    BuffType = BuffType.Endurance,
                    Power = 6,
                    Duration = 4
                }
            };

            var behavior = Behavior(EnemyBehaviorSO.CastFromSpellList(0.5f), Swing());
            var enemy = Enemy(behavior);
            enemy.Spells = new List<EnemySpellEntry>
            {
                new EnemySpellEntry { Magic = shieldUp, CastWeight = 1f }
            };

            var unit = SimUnit.FromEnemy(enemy, null);
            var shifts = BalanceMath.StatShiftsOf(unit);

            Assert.IsNotEmpty(shifts, "A Buff effect inside a cast has to reach the model at all.");
            bool foundEndurance = false;
            foreach (var shift in shifts)
            {
                if (shift.Stat == StatType.Endurance && !shift.OnHeroSide)
                {
                    foundEndurance = true;
                }
            }
            Assert.IsTrue(foundEndurance, "Shield Up on itself is an Endurance buff on the enemy side.");

            var party = new List<SimUnit> { Hero() };
            Assert.Less(BalanceMath.OutputSuppressionOf(party, unit), 1f,
                "More Endurance means the party clears it more slowly.");
        }

        [Test]
        public void ResistanceAndStatusBuffs_AreSkippedRatherThanGuessedAt()
        {
            // BuffType carries resistances and status effects as well as stats. Those have no StatType
            // to map to, and inventing one would put a made-up number into the danger index.
            var cloak = ScriptableObject.CreateInstance<MagicSO>();
            _created.Add(cloak);
            cloak.Key = "FireCloak";
            cloak.TargetType = MagicTargetType.Self;
            cloak.Effects = new List<SpellEffect>
            {
                new SpellEffect
                {
                    EffectType = SpellEffectType.Buff,
                    BuffType = BuffType.FireResistance,
                    Power = 40,
                    Duration = 3
                }
            };

            var shifts = new List<StatShift>();
            EnemyMagicModel.CollectStatShifts(cloak, 1f, shifts);

            Assert.IsEmpty(shifts);
        }

        // ------------------------------------------------------------------ no double counting

        [Test]
        public void AnEnemyWithNoSupport_ReadsExactlyAsBefore()
        {
            // The regression guard for the whole channel: a plain attacker must be untouched by any of
            // it, or every number in the project moves for no reason.
            var plain = Enemy(Behavior(Swing()));
            var unit = SimUnit.FromEnemy(plain, null);
            var party = new List<SimUnit> { Hero(), Hero() };
            var targets = new List<SimUnit> { unit };

            Assert.AreEqual(0f, BalanceMath.SustainPerTick(unit), 0.0001f);
            Assert.AreEqual(1f, BalanceMath.OutputSuppression(party, targets), 0.0001f);
            Assert.AreEqual(
                BalanceMath.GroupDamagePerTick(party, targets),
                BalanceMath.NetClearRate(BalanceMath.GroupDamagePerTick(party, targets), party, targets),
                0.0001f);
        }
    }
}
