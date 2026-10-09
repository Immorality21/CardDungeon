using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The three hit-or-miss rules added 2026-10-09: every debuff lands on a chance
    /// (<see cref="AfflictionRules"/>), a blinded unit's physical hits can miss
    /// (<see cref="DefenseRules.RollMiss"/>), and an ability crits only when it can
    /// (<see cref="MagicSO.CanCrit"/>, or a crit upgrade on the caster's grid). Also the ability
    /// upgrades themselves (<see cref="SphereNodeKind.MagicUpgrade"/> through <see cref="AbilityGrant"/>):
    /// power, crit chance, duration, apply chance and charges, folded in at the cast.
    /// </summary>
    public class CritMissAfflictionTests
    {
        private EffectResolver _resolver;
        private CombatBuffTracker _buffTracker;
        private MockCombatUnit _caster;
        private MockCombatUnit _target;

        /// <summary>A caster whose grid has bought upgrades for some abilities.</summary>
        private class UpgradedCaster : MockCombatUnit, IAbilityUpgrades
        {
            public readonly Dictionary<string, AbilityGrant> Grants = new Dictionary<string, AbilityGrant>();

            public UpgradedCaster()
                : base("Upgraded", strength: 10, endurance: 0, health: 50)
            {
            }

            public UpgradedCaster With(string key, AbilityUpgradeType type, int amount)
            {
                AbilityGrant grant;
                if (!Grants.TryGetValue(key, out grant))
                {
                    grant = new AbilityGrant { Key = key };
                    Grants[key] = grant;
                }
                grant.Add(type, amount);
                return this;
            }

            public AbilityGrant UpgradesFor(string abilityKey)
            {
                AbilityGrant grant;
                return Grants.TryGetValue(abilityKey, out grant) ? grant : null;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _resolver = new EffectResolver();
            _buffTracker = new CombatBuffTracker();
            _caster = new MockCombatUnit("Caster", strength: 10, endurance: 0, health: 50);
            _target = new MockCombatUnit("Target", strength: 5, endurance: 0, health: 200, isHero: false);
        }

        [TearDown]
        public void TearDown()
        {
            DefenseRules.Roll = () => Random.Range(0f, 1f);
            CritRules.Roll = () => Random.Range(0f, 1f);
            AfflictionRules.Roll = () => Random.Range(0f, 1f);
        }

        // --- fixtures --------------------------------------------------------

        private static SpellEffect Hit(StatType scaling = StatType.Strength, int power = 5)
        {
            return new SpellEffect { EffectType = SpellEffectType.Damage, Power = power, ScalingStat = scaling };
        }

        private static SpellEffect Debuff(BuffType type, int chance, int power = 0)
        {
            return new SpellEffect
            {
                EffectType = SpellEffectType.Debuff,
                BuffType = type,
                Power = power,
                Duration = 3,
                ApplyChance = chance
            };
        }

        private static MagicSO Ability(string key, bool canCrit, params SpellEffect[] effects)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = key;
            magic.CanCrit = canCrit;
            magic.Effects = new List<SpellEffect>(effects);
            magic.Tags = new List<MagicTag>();
            return magic;
        }

        private EffectResult Cast(MagicSO magic, ICombatUnit caster = null)
        {
            return _resolver.Execute(new SpellcastAction
            {
                Magic = magic,
                Caster = caster ?? _caster,
                Targets = new List<ICombatUnit> { _target }
            }, _buffTracker);
        }

        private int Lost()
        {
            return 200 - _target.Stats.Health;
        }

        // --- AfflictionRules -------------------------------------------------

        [Test]
        public void Lands_Certain_IgnoresTheRoll()
        {
            AfflictionRules.Roll = () => 0.999f;
            Assert.IsTrue(AfflictionRules.Lands(AfflictionRules.Certain));
        }

        [TestCase(0.49f, true)]
        [TestCase(0.51f, false)]
        public void Lands_HalfChance_FollowsTheRoll(float roll, bool lands)
        {
            AfflictionRules.Roll = () => roll;
            Assert.AreEqual(lands, AfflictionRules.Lands(50));
        }

        [Test]
        public void SpellEffect_DefaultsToCertain_SoOlderAssetsAreUnchanged()
        {
            Assert.AreEqual(AfflictionRules.Certain, new SpellEffect().ApplyChance);
        }

        [Test]
        public void Debuff_FailedRoll_AppliesNothing_AndSaysUnaffected()
        {
            AfflictionRules.Roll = () => 0.9f;

            var result = Cast(Ability("hush", false, Debuff(BuffType.Silenced, 60)));

            Assert.IsFalse(_buffTracker.HasStatusEffect(_target, BuffType.Silenced));
            Assert.IsTrue(result.Entries.Any(e => e.Text == Assets.Scripts.Cards.Effects.DebuffEffectExecutor.UnaffectedText));
        }

        [Test]
        public void Debuff_PassedRoll_Applies()
        {
            AfflictionRules.Roll = () => 0.1f;

            Cast(Ability("hush", false, Debuff(BuffType.Silenced, 60)));

            Assert.IsTrue(_buffTracker.HasStatusEffect(_target, BuffType.Silenced));
        }

        [Test]
        public void Debuff_FailedRiderOnAHit_PassesQuietly()
        {
            AfflictionRules.Roll = () => 0.9f;

            var result = Cast(Ability("slash", false, Hit(), Debuff(BuffType.Bleeding, 50, power: 1)));

            Assert.Greater(Lost(), 0, "The hit still lands.");
            Assert.IsFalse(_buffTracker.HasStatusEffect(_target, BuffType.Bleeding));
            Assert.IsFalse(result.Entries.Any(e => e.Text == Assets.Scripts.Cards.Effects.DebuffEffectExecutor.UnaffectedText),
                "The damage number is the news; a failed rider says nothing.");
        }

        [Test]
        public void Debuff_StatCut_RollsToo()
        {
            AfflictionRules.Roll = () => 0.9f;

            Cast(Ability("sunder", false, Debuff(BuffType.Endurance, 50, power: 3)));

            Assert.AreEqual(0, _buffTracker.GetBuffAmount(_target, StatType.Endurance));
        }

        [Test]
        public void Clone_CarriesApplyChance()
        {
            var effect = Debuff(BuffType.Poisoned, 35, power: 2);
            Assert.AreEqual(35, effect.Clone().ApplyChance);
        }

        // --- Blind -----------------------------------------------------------

        [Test]
        public void Blinded_IsACurableStatus()
        {
            Assert.IsTrue(StatusImmunity.IsStatus(BuffType.Blinded));
            Assert.IsTrue(BuffHandlerRegistry.IsCurable(BuffType.Blinded));
            Assert.IsInstanceOf<BlindedBuffHandler>(BuffHandlerRegistry.Get(BuffType.Blinded));
        }

        [Test]
        public void MissChance_OnlyWhenBlinded_AndStacksWithDodge()
        {
            Assert.AreEqual(0f, DefenseRules.MissChanceFor(false));
            Assert.AreEqual(DefenseRules.BlindMissChance, DefenseRules.MissChanceFor(true));

            _target.EffectiveOverrides[StatType.Luck] = 20;
            float dodge = DefenseRules.DodgeChanceFor(_target);
            Assert.AreEqual((1f - DefenseRules.BlindMissChance) * (1f - dodge),
                DefenseRules.HitChance(true, _target), 1e-5f);
            Assert.AreEqual(1f - dodge, DefenseRules.HitChance(false, _target), 1e-5f);
        }

        [Test]
        public void BlindedCaster_PhysicalAbility_CanMissTheWholeCast()
        {
            _buffTracker.ApplyStatusEffect(_caster, BuffType.Blinded, 3);
            DefenseRules.Roll = () => 0f;

            var result = Cast(Ability("slash", false, Hit(), Debuff(BuffType.Bleeding, 100, power: 1)));

            Assert.AreEqual(0, Lost());
            Assert.IsFalse(_buffTracker.HasStatusEffect(_target, BuffType.Bleeding), "A miss lands no rider either.");
            Assert.IsTrue(result.Entries.Any(e => e.Text == "Miss"));
        }

        [Test]
        public void BlindedCaster_Magic_NeverMisses()
        {
            _buffTracker.ApplyStatusEffect(_caster, BuffType.Blinded, 3);
            _caster.EffectiveOverrides[StatType.Intelligence] = 10;
            DefenseRules.Roll = () => 0f;

            Cast(Ability("bolt", false, Hit(StatType.Intelligence)));

            Assert.Greater(Lost(), 0);
        }

        [Test]
        public void UnblindedCaster_NeverMisses()
        {
            DefenseRules.Roll = () => 0f;

            Cast(Ability("slash", false, Hit()));

            Assert.Greater(Lost(), 0);
        }

        // --- Crits on abilities ----------------------------------------------

        [Test]
        public void Ability_ThatCannotCrit_NeverDoes()
        {
            CritRules.Roll = () => 0f;

            var result = Cast(Ability("bash", false, Hit()));

            Assert.IsFalse(result.Entries.Any(e => e.Crit));
            Assert.AreEqual(15, Lost(), "STR 10 + 5, no defense, no crit.");
        }

        [Test]
        public void Ability_ThatCanCrit_CritsOnALowRoll()
        {
            CritRules.Roll = () => 0f;

            var result = Cast(Ability("slash", true, Hit()));

            Assert.IsTrue(result.Entries.Any(e => e.Crit));
            Assert.AreEqual(CritRules.Apply(15), Lost());
        }

        [Test]
        public void Ability_ThatCanCrit_HighRoll_IsAPlainHit()
        {
            CritRules.Roll = () => 0.99f;

            Cast(Ability("slash", true, Hit()));

            Assert.AreEqual(15, Lost());
        }

        [Test]
        public void GridUnlock_LetsAnUnauthoredAbilityCrit()
        {
            CritRules.Roll = () => 0f;

            Cast(Ability("volley", false, Hit()), new UpgradedCaster().With("volley", AbilityUpgradeType.CritChance, 15));

            Assert.AreEqual(CritRules.Apply(15), Lost());
        }

        [Test]
        public void CanCrit_NeedsDamage()
        {
            var buffOnly = Ability("cry", true, new SpellEffect { EffectType = SpellEffectType.Buff, BuffType = BuffType.Strength, Power = 2 });
            Assert.IsFalse(EffectResolver.CanCrit(buffOnly, _caster));
        }

        [Test]
        public void CritRules_Apply_AlwaysAddsAtLeastOne_AndLeavesAbsorbsAlone()
        {
            Assert.AreEqual(2, CritRules.Apply(1));
            Assert.AreEqual(16, CritRules.Apply(10));
            Assert.AreEqual(0, CritRules.Apply(0));
            Assert.AreEqual(-5, CritRules.Apply(-5));
        }

        // --- Ability upgrades (MagicUpgrade nodes) ---------------------------

        private static SphereGridNode Upgrade(string key, string ability, AbilityUpgradeType type, int amount)
        {
            return new SphereGridNode
            {
                Key = key, Kind = SphereNodeKind.MagicUpgrade, GrantedMagicKey = ability,
                UpgradeType = type, UpgradeAmount = amount
            };
        }

        /// <summary>start - volley (2 charges) - keen (+15% crit), sharp (+2 power), more (+1 charge), awaken.</summary>
        private static SphereGridSO UpgradeGrid()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "start";
            grid.Nodes = new List<SphereGridNode>
            {
                new SphereGridNode { Key = "start", Neighbors = new List<string> { "volley", "keen", "sharp", "more", "awaken" } },
                new SphereGridNode { Key = "volley", Kind = SphereNodeKind.MagicKnown, GrantedMagicKey = "Volley", GrantedCharges = 2 },
                Upgrade("keen", "Volley", AbilityUpgradeType.CritChance, 15),
                Upgrade("sharp", "Volley", AbilityUpgradeType.Power, 2),
                Upgrade("more", "Volley", AbilityUpgradeType.Charges, 1),
                new SphereGridNode
                {
                    Key = "awaken", Kind = SphereNodeKind.MagicAwaken,
                    GrantedMagicKey = "Volley", AwakenedMagicKey = "VolleyAwakened"
                }
            };
            return grid;
        }

        [Test]
        public void AbilityUpgradesForNodes_SumsEveryNodeForAnAbility()
        {
            var grid = UpgradeGrid();

            Assert.IsNull(SphereGridOps.AbilityUpgradesFor(grid, new List<string> { "start", "volley" }, "Volley"));

            var grant = SphereGridOps.AbilityUpgradesFor(grid, new List<string> { "start", "volley", "keen", "sharp" }, "Volley");
            Assert.IsTrue(grant.CanCrit);
            Assert.AreEqual(15, grant.CritChanceBonus);
            Assert.AreEqual(2, grant.PowerBonus);
        }

        [Test]
        public void AbilityUpgrades_CarryToTheAwakening()
        {
            var grid = UpgradeGrid();

            var grant = SphereGridOps.AbilityUpgradesFor(grid, new List<string> { "start", "volley", "sharp", "awaken" }, "VolleyAwakened");

            Assert.IsNotNull(grant, "Buying the awakening must not take the upgrades away.");
            Assert.AreEqual(2, grant.PowerBonus);
        }

        [Test]
        public void ChargeUpgrades_AreInTheKnownAllowance()
        {
            var grid = UpgradeGrid();

            var known = SphereGridOps.KnownMagicForNodes(grid, new List<string> { "start", "volley", "more" });

            Assert.AreEqual(3, known.First(k => k.Key == "Volley").Value);
        }

        [Test]
        public void PowerUpgrade_HitsHarder()
        {
            Cast(Ability("bash", false, Hit()), new UpgradedCaster().With("bash", AbilityUpgradeType.Power, 3));

            Assert.AreEqual(18, Lost(), "STR 10 + 5 + 3.");
        }

        [Test]
        public void CritUpgrade_RaisesTheChance_OnTopOfLuck()
        {
            var caster = new UpgradedCaster().With("bash", AbilityUpgradeType.CritChance, 20);
            var magic = Ability("bash", false, Hit());

            Assert.AreEqual(CritRules.ChanceFor(caster) + 0.20f, EffectResolver.CritChanceWith(magic, caster), 1e-5f);
            Assert.AreEqual(0f, EffectResolver.CritChanceWith(magic, _caster), "No upgrade, not authored: it cannot crit.");

            // A roll the base chance misses but the upgraded one does not.
            CritRules.Roll = () => CritRules.ChanceFor(caster) + 0.1f;
            Cast(magic, caster);
            Assert.AreEqual(CritRules.Apply(15), Lost());
        }

        [Test]
        public void DurationUpgrade_LengthensTimedEffects()
        {
            var caster = new UpgradedCaster().With("dart", AbilityUpgradeType.Duration, 2);

            Cast(Ability("dart", false, Debuff(BuffType.Poisoned, 100, power: 1)), caster);

            // 3 authored + 2: still up after four of the target's turns, gone after the fifth.
            for (int turn = 0; turn < 4; turn++)
            {
                _buffTracker.TickBuffs(_target);
            }
            Assert.IsTrue(_buffTracker.HasStatusEffect(_target, BuffType.Poisoned), "Outlasts its authored 3 turns.");
            _buffTracker.TickBuffs(_target);
            Assert.IsFalse(_buffTracker.HasStatusEffect(_target, BuffType.Poisoned), "And lasts exactly 5.");
        }

        [Test]
        public void ApplyChanceUpgrade_MakesADebuffLand_AndNeverPassesCertain()
        {
            AfflictionRules.Roll = () => 0.55f;
            var caster = new UpgradedCaster().With("hush", AbilityUpgradeType.ApplyChance, 10);

            Cast(Ability("hush", false, Debuff(BuffType.Silenced, 50)), caster);

            Assert.IsTrue(_buffTracker.HasStatusEffect(_target, BuffType.Silenced), "50% + 10% beats a 0.55 roll.");
            var capped = EffectResolver.ApplyUpgrades(Debuff(BuffType.Silenced, 95), caster.UpgradesFor("hush"));
            Assert.AreEqual(AfflictionRules.Certain, capped.ApplyChance);
        }

        [Test]
        public void Upgrades_NeverChangeTheSharedAsset()
        {
            var magic = Ability("dart", false, Debuff(BuffType.Poisoned, 50, power: 1));
            var caster = new UpgradedCaster()
                .With("dart", AbilityUpgradeType.Duration, 2)
                .With("dart", AbilityUpgradeType.ApplyChance, 25);

            Cast(magic, caster);

            Assert.AreEqual(3, magic.Effects[0].Duration);
            Assert.AreEqual(50, magic.Effects[0].ApplyChance);
        }
    }
}
