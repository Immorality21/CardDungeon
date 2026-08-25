using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// A unit's health ceiling is its <b>effective</b> MaxHealth - base plus gear - everywhere, not
    /// its authored <c>Stats.MaxHealth</c>.
    ///
    /// <para>This used to be inconsistent and it made +MaxHealth gear very nearly inert: the potion
    /// path healed to the effective max, but <c>Party.HealAll()</c> filled only the base, and the HP
    /// bar rendered against the base too - so the bonus bought a sliver of bar the party could never
    /// be healed into and could not see. The analyzer carried it as a standing warning. These pin the
    /// clamps that are reachable without a MonoBehaviour.</para>
    /// </summary>
    public class EffectiveMaxHealthTests
    {
        /// <summary>A hero whose gear raises MaxHealth above the authored value, and who is wounded.</summary>
        private static MockCombatUnit GearedHero(int baseMax, int effectiveMax, int currentHealth)
        {
            var hero = new MockCombatUnit("Warrior", strength: 10, endurance: 0, health: baseMax);
            hero.EffectiveOverrides[StatType.MaxHealth] = effectiveMax;
            hero.Stats.Health = currentHealth;
            return hero;
        }

        [Test]
        public void Heal_ClampsToTheEffectiveMax_NotTheAuthoredOne()
        {
            var hero = GearedHero(baseMax: 16, effectiveMax: 22, currentHealth: 10);
            var result = new EffectResult();

            new HealEffectExecutor().Execute(
                new SpellEffect { EffectType = SpellEffectType.Heal, Power = 30 },
                hero,
                new List<ICombatUnit> { hero },
                new CombatBuffTracker(),
                result,
                flatPower: true);

            Assert.AreEqual(22, hero.Stats.Health, "A big heal should fill the bar the hero fights with.");
        }

        [Test]
        public void Heal_StillCannotOverfillTheBar()
        {
            var hero = GearedHero(baseMax: 16, effectiveMax: 22, currentHealth: 22);
            var result = new EffectResult();

            new HealEffectExecutor().Execute(
                new SpellEffect { EffectType = SpellEffectType.Heal, Power = 10 },
                hero,
                new List<ICombatUnit> { hero },
                new CombatBuffTracker(),
                result,
                flatPower: true);

            Assert.AreEqual(22, hero.Stats.Health);
        }

        [Test]
        public void Heal_WithNoGear_IsUnchanged()
        {
            // The overwhelming majority of units have no MaxHealth bonus at all; effective and
            // authored are the same number and none of this is observable.
            var hero = new MockCombatUnit("Plain", strength: 10, endurance: 0, health: 16);
            hero.Stats.Health = 4;
            var result = new EffectResult();

            new HealEffectExecutor().Execute(
                new SpellEffect { EffectType = SpellEffectType.Heal, Power = 30 },
                hero,
                new List<ICombatUnit> { hero },
                new CombatBuffTracker(),
                result,
                flatPower: true);

            Assert.AreEqual(16, hero.Stats.Health);
        }

        [Test]
        public void Absorbed_ElementalDamage_HealsUpToTheEffectiveMax()
        {
            // Resistance above 100% turns a hit into healing (DamageCalculator returns negative), and
            // that heal is capped by the same ceiling.
            var hero = GearedHero(baseMax: 16, effectiveMax: 22, currentHealth: 10);
            hero.Resistances = new List<Resistance>
            {
                new Resistance { DamageType = DamageType.Fire, Percent = 200f }
            };
            var result = new EffectResult();

            new DamageEffectExecutor().Execute(
                new SpellEffect
                {
                    EffectType = SpellEffectType.Damage,
                    Power = 30,
                    DamageType = DamageType.Fire
                },
                hero,
                new List<ICombatUnit> { hero },
                new CombatBuffTracker(),
                result,
                flatPower: true);

            Assert.AreEqual(22, hero.Stats.Health, "Absorption fills the same bar a heal does.");
        }

        [Test]
        public void Damage_IsUnaffectedByTheCeiling()
        {
            var hero = GearedHero(baseMax: 16, effectiveMax: 22, currentHealth: 22);
            var result = new EffectResult();

            new DamageEffectExecutor().Execute(
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 6 },
                hero,
                new List<ICombatUnit> { hero },
                new CombatBuffTracker(),
                result,
                flatPower: true);

            Assert.AreEqual(16, hero.Stats.Health);
        }
    }
}
