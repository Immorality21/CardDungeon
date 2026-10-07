using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>One equipped magic in a simulated hero's slot bar.</summary>
    public class SimMagicSlot
    {
        public MagicSO Magic;
        public int Charges;
        public int MaxCharges;

        public bool CanCast => Magic != null && Charges > 0;

        public SimMagicSlot Clone()
        {
            return new SimMagicSlot
            {
                Magic = Magic,
                Charges = Charges,
                MaxCharges = MaxCharges
            };
        }
    }

    /// <summary>
    /// One summon a simulated hero carries (§4b), with its charges. <see cref="Castable"/> is the
    /// transient magic the effect engine resolves — built once per (summon, upgrades) and shared,
    /// because a frontier sweep builds thousands of parties and a ScriptableObject per party would
    /// be thousands of allocations for the same effects.
    /// </summary>
    public class SimSummonSlot
    {
        public SummonSO Summon;
        public Heroes.SummonGrant Grant;
        public MagicSO Castable;
        public int Charges;
        public int MaxCharges;

        /// <summary>A party-replacing summon (§4b): it has no castable, it builds a unit.</summary>
        public bool IsReplacement => Summon != null && Summon.Kind == SummonKind.ReplaceParty;

        /// <summary>A summon that fights beside the party: no castable either, it builds a unit that
        /// joins the hero side.</summary>
        public bool IsAlly => Summon != null && Summon.Kind == SummonKind.JoinParty;

        /// <summary>A special attack: one castable that lands and is over.</summary>
        public bool IsSpecialAttack => !IsReplacement && !IsAlly;

        public bool CanUse => Charges > 0 && (IsReplacement || IsAlly || Castable != null);

        /// <summary>The longest timed effect, so the policy can wait out a buff before re-summoning.</summary>
        public int LongestDuration
        {
            get
            {
                int longest = 0;
                if (Castable != null && Castable.Effects != null)
                {
                    foreach (var effect in Castable.Effects)
                    {
                        if (effect != null && effect.Duration > longest)
                        {
                            longest = effect.Duration;
                        }
                    }
                }
                return longest;
            }
        }

        public SimSummonSlot Clone()
        {
            return new SimSummonSlot
            {
                Summon = Summon,
                Grant = Grant,
                Castable = Castable,
                Charges = Charges,
                MaxCharges = MaxCharges
            };
        }

        private static readonly Dictionary<string, MagicSO> CastableCache = new Dictionary<string, MagicSO>();

        /// <summary>A shared castable for this summon at these upgrades.</summary>
        public static MagicSO CastableFor(SummonSO summon, Heroes.SummonGrant grant)
        {
            if (summon == null)
            {
                return null;
            }
            string key = summon.Key + "/" + (grant != null ? grant.PowerBonus : 0) + "/" + (grant != null ? grant.DurationBonus : 0);
            if (!CastableCache.TryGetValue(key, out var castable) || castable == null)
            {
                castable = SummonOps.BuildCastable(summon, grant);
                CastableCache[key] = castable;
            }
            return castable;
        }
    }

    /// <summary>
    /// A headless <see cref="ICombatUnit"/> for the balance model and simulator. Gear and level
    /// bonuses are folded into the effective stats at build time (they never change mid-fight), so
    /// no <see cref="Assets.Scripts.Items.InventoryManager"/> is needed; combat buffs still stack on
    /// top via <see cref="CombatBuffTracker"/> exactly as in the live combat loop.
    ///
    /// MaxHealth here includes gear MaxHealth bonuses, and so does the live game now: every health
    /// ceiling — <c>Party.HealAll()</c>, the heal and absorb clamps, the HP bar — reads
    /// <c>GetEffectiveStat(MaxHealth)</c>. It did not always, which is why this note exists: the
    /// model was right and the game was short by exactly the gear bonus.
    /// </summary>
    public class SimUnit : ICombatUnit, IStatusImmune, ITriggerSource
    {
        /// <summary>
        /// The reactions this unit carries - its gear's for a hero (set by <c>PartyBaseline</c>), its
        /// definition's for an enemy or a summon - fired by the encounter loop's
        /// <see cref="TriggerRegistry"/> exactly as the live fight fires them.
        /// </summary>
        public List<CarriedTrigger> Triggers = new List<CarriedTrigger>();

        public IEnumerable<CarriedTrigger> GetTriggers()
        {
            return Triggers;
        }

        /// <summary>Mirrors <c>Enemy.IsImmuneTo</c> off the same definition, so the model refuses what the game refuses.</summary>
        public bool IsImmuneTo(BuffType type)
        {
            return Definition != null && StatusImmunity.ListContains(Definition.StatusImmunities, type);
        }

        public string DisplayName { get; set; }
        public Sprite Icon => null;
        public Stats Stats { get; set; }
        public bool IsAlive => Stats != null && Stats.Health > 0;
        public bool IsHero { get; set; }
        public Transform Transform => null;
        public List<Resistance> Resistances { get; set; } = new List<Resistance>();

        /// <summary>
        /// Attack power is stored rather than derived: a hero's comes from its <c>AttackStat</c>,
        /// which the model resolves once in <c>PartyBaseline</c> instead of re-deriving per call.
        /// </summary>
        public int EffectiveAttackPower;

        /// <summary>Stats after level gains and gear, i.e. what the unit actually fights with.</summary>
        public StatBlock Effective = new StatBlock();

        /// <summary>Element this unit's physical attacks carry; Normal for heroes.</summary>
        public DamageType AttackDamageType { get; set; } = DamageType.Normal;

        /// <summary>
        /// Set by whoever built this unit: <c>PartyBaseline</c> resolves the hero's choice,
        /// <c>FromEnemy</c> uses Strength. Stored rather than derived so the model does not have to
        /// reach back into the definition on every read.
        /// </summary>
        public StatType AttackStat { get; set; }

        public int GetEffectiveAttackPower()
        {
            return EffectiveAttackPower;
        }

        public int GetEffectiveStat(StatType stat)
        {
            return Effective[stat];
        }

        // ---- Hero-side ----
        public string HeroKey = "";
        public List<SimMagicSlot> MagicSlots = new List<SimMagicSlot>();
        public List<SimSummonSlot> Summons = new List<SimSummonSlot>();

        /// <summary>The Ultras this hero's grid teaches (COMBAT_DEPTH §13), used by <see cref="SimUltras"/>.</summary>
        public List<UltraSO> Ultras = new List<UltraSO>();

        // ---- Enemy-side (mirrors the per-fight state CombatManager keeps on Enemy) ----
        public EnemySO Definition;
        public EnemyArchetype Archetype = EnemyArchetype.Aggressor;

        /// <summary>
        /// The level tuning this enemy was built under. Held because spell power scales off it
        /// (<see cref="LevelEnemyTuning.MagicPowerScaleFor(EnemySO)"/>) the same way the stat block
        /// does, so the model can price a cast without reaching back for the level.
        /// </summary>
        public LevelEnemyTuning Tuning;

        /// <summary>This enemy's authored repertoire — what <c>EnemyActionPlanner</c> reads.</summary>
        public EnemyBehaviorSO Behavior;
        /// <summary>Index of the telegraphed action in flight, or -1. Mirrors <c>Enemy.ChargingEntryIndex</c>.</summary>
        public int ChargingEntryIndex = -1;
        public ICombatUnit ChargeTarget;
        public bool IsCharging => ChargingEntryIndex >= 0;
        public int TurnsTaken;

        public bool IsBoss => Definition != null && Definition.IsBoss;

        /// <summary>A fresh copy at full health — one per simulated battle.</summary>
        public SimUnit Clone()
        {
            var clone = new SimUnit
            {
                DisplayName = DisplayName,
                IsHero = IsHero,
                Stats = Stats.Clone(),
                Resistances = new List<Resistance>(Resistances),
                AttackStat = AttackStat,
                EffectiveAttackPower = EffectiveAttackPower,
                Effective = Effective.Clone(),
                AttackDamageType = AttackDamageType,
                HeroKey = HeroKey,
                Definition = Definition,
                Archetype = Archetype,
                Tuning = Tuning,
                Behavior = Behavior,
                Triggers = new List<CarriedTrigger>(Triggers)
            };

            foreach (var slot in MagicSlots)
            {
                clone.MagicSlots.Add(slot.Clone());
            }
            foreach (var summon in Summons)
            {
                clone.Summons.Add(summon.Clone());
            }
            clone.Ultras.AddRange(Ultras);

            return clone;
        }

        /// <summary>
        /// The simulated stand-in for a party-replacing summon or an ally, built by the same
        /// <see cref="SummonOps.StatsFor"/> the live <c>SummonUnit</c> uses: the summoner's
        /// <see cref="Effective"/> stats (base + grid + gear - never a combat buff) scaled by the
        /// summon's percentages, at full health, with the summoner's resistances when the asset copies
        /// them. On the hero side, swinging off Strength.
        /// </summary>
        public static SimUnit FromSummon(SummonSO summon, Heroes.SummonGrant grant, SimUnit summoner)
        {
            var block = SummonOps.StatsFor(summon, grant,
                stat => summoner != null ? summoner.GetEffectiveStat(stat) : 0);
            return new SimUnit
            {
                DisplayName = summon != null ? summon.Label : "Summon",
                IsHero = true,
                Stats = new Stats(block),
                Effective = block.Clone(),
                Resistances = summon != null && summon.CopySummonerResistances && summoner != null
                    ? new List<Resistance>(summoner.Resistances)
                    : new List<Resistance>(),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = block[StatType.Strength],
                Triggers = new List<CarriedTrigger>(TriggerSources.From(summon != null ? summon.Triggers : null,
                    summon != null ? summon.Label : "Summon"))
            };
        }

        /// <summary>
        /// Builds a simulated enemy from its definition, under an optional level tuning.
        ///
        /// <para>The tuning is where an enemy's real numbers come from: an <c>EnemySO</c> is a
        /// template reused across the campaign, and the level it appears in owns its stats (see
        /// <see cref="LevelEnemyTuning"/>). Null means the template's own values - which is right for
        /// a project-wide authoring check, and wrong for anything measuring a fight.</para>
        /// </summary>
        public static SimUnit FromEnemy(EnemySO definition, LevelEnemyTuning tuning = null)
        {
            if (definition == null)
            {
                return null;
            }

            var stats = LevelEnemyTuning.StatsFor(definition, tuning);

            return new SimUnit
            {
                DisplayName = string.IsNullOrEmpty(definition.DisplayName) ? definition.name : definition.DisplayName,
                IsHero = false,
                Stats = new Stats(stats),
                Effective = stats.Clone(),
                Resistances = definition.Resistances != null
                    ? new List<Resistance>(definition.Resistances)
                    : new List<Resistance>(),
                // Enemies always swing off Strength; only heroes pick an attack stat.
                AttackStat = StatType.Strength,
                EffectiveAttackPower = stats[StatType.Strength],
                AttackDamageType = definition.AttackDamageType,
                Definition = definition,
                Archetype = definition.ArchetypeOf,
                Tuning = tuning,
                Behavior = definition.ResolvedBehavior,
                Triggers = new List<CarriedTrigger>(TriggerSources.From(definition.Triggers,
                    string.IsNullOrEmpty(definition.DisplayName) ? definition.name : definition.DisplayName))
            };
        }
    }
}
