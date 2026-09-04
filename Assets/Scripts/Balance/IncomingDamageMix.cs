using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// What the party expects to be hit *with*, as a share of incoming damage per
    /// <see cref="DamageType"/>. The defensive mirror of the danger index: a resistance is worth
    /// exactly as much as the element it answers actually turns up.
    ///
    /// <para><b>Why this exists.</b> <see cref="GearLoadout"/> used to rank items on their stat line
    /// alone, so the Ruby Amulet (25% Fire) and the Leather Cap (10% Lightning) were bought for their
    /// bonuses and their resistance counted for nothing — even though <c>PartyBaseline</c> hands that
    /// resistance to the simulator, where it demonstrably changes the fight. Pricing it needs one
    /// number the stat line cannot supply: how much of the incoming damage is that element. That is
    /// this. See <c>docs/BALANCING.md</c> §5q.</para>
    ///
    /// <para><b>What it approximates.</b> Weight per enemy is its attack power, split between its
    /// basic attack and its casts by the behaviour's cast share. Spell *magnitude* is not modelled —
    /// a cast counts as a turn's worth of that enemy's damage, whatever the spell does — so the mix
    /// answers "which elements arrive, roughly how often", not "how hard". That is the resolution the
    /// ranking needs: it is choosing between items, not reporting a damage figure.</para>
    /// </summary>
    public class IncomingDamageMix
    {
        private readonly Dictionary<DamageType, float> _weight = new Dictionary<DamageType, float>();
        private float _total;

        /// <summary>Nothing was accumulated, so every share is zero and resistance prices at nothing.</summary>
        public bool IsEmpty => _total <= 0f;

        /// <summary>Total weight accumulated, in "attack power per turn" units. For tests and reporting.</summary>
        public float TotalWeight => _total;

        /// <summary>
        /// Fraction of expected incoming damage carrying <paramref name="type"/>, in 0..1. An empty
        /// mix returns 0 for everything, which makes resistance worth nothing — the honest answer
        /// when nothing is known about what the party will face.
        /// </summary>
        public float ShareOf(DamageType type)
        {
            if (_total <= 0f || !_weight.TryGetValue(type, out float weight))
            {
                return 0f;
            }
            return weight / _total;
        }

        public void Add(DamageType type, float weight)
        {
            if (weight <= 0f)
            {
                return;
            }
            _weight.TryGetValue(type, out float existing);
            _weight[type] = existing + weight;
            _total += weight;
        }

        /// <summary>The mix a whole floor presents, given its rooms in the simulator's own form.</summary>
        public static IncomingDamageMix FromRooms(IEnumerable<IList<SimUnit>> rooms)
        {
            var mix = new IncomingDamageMix();
            if (rooms == null)
            {
                return mix;
            }

            foreach (var room in rooms)
            {
                mix.Accumulate(room);
            }
            return mix;
        }

        /// <summary>The mix one set of units presents. Heroes are skipped: they are not the threat.</summary>
        public static IncomingDamageMix FromUnits(IEnumerable<SimUnit> units)
        {
            var mix = new IncomingDamageMix();
            mix.Accumulate(units);
            return mix;
        }

        /// <summary>
        /// The campaign-wide mix, from enemy definitions rather than tuned units. Used where the
        /// model builds one party for the whole game rather than for a floor, so per-level tuning is
        /// not available and the template's own numbers are the best weight there is.
        /// </summary>
        public static IncomingDamageMix FromEnemies(IEnumerable<EnemySO> enemies)
        {
            var mix = new IncomingDamageMix();
            if (enemies == null)
            {
                return mix;
            }

            foreach (var enemy in enemies)
            {
                var unit = SimUnit.FromEnemy(enemy);
                if (unit != null)
                {
                    mix.AddUnit(unit);
                }
            }
            return mix;
        }

        private void Accumulate(IEnumerable<SimUnit> units)
        {
            if (units == null)
            {
                return;
            }

            foreach (var unit in units)
            {
                AddUnit(unit);
            }
        }

        private void AddUnit(SimUnit unit)
        {
            if (unit == null || unit.IsHero)
            {
                return;
            }

            // Attack power is the per-turn damage proxy, so a Dragon weighs more in the mix than a
            // Floating Eye standing next to it - which is the point: the party is choosing armour
            // against the damage it will actually take, not against a headcount.
            float weight = Mathf.Max(1f, unit.EffectiveAttackPower);
            float castShare = unit.Behavior != null
                ? Mathf.Clamp01(EnemyMagicModel.CastShareOf(unit.Behavior))
                : 0f;

            Add(unit.AttackDamageType, weight * (1f - castShare));

            if (castShare <= 0f)
            {
                return;
            }

            var casts = DamageTypesOfCasts(unit.Definition);
            if (casts.Count == 0)
            {
                // A caster with nothing damaging to cast still spends those turns doing something.
                // Attributing them to its swing beats dropping the weight and quietly shrinking the
                // enemy in the mix.
                Add(unit.AttackDamageType, weight * castShare);
                return;
            }

            foreach (var cast in casts)
            {
                Add(cast.Key, weight * castShare * cast.Value);
            }
        }

        /// <summary>
        /// Damage types this enemy's repertoire casts, as shares of its casting turns. Weighted by
        /// <c>EnemySpellEntry.CastWeight</c> the same way <c>EnemyMagicModel.Profile</c> weights
        /// it — all-zero weights (what assets authored before <c>CastWeight</c> deserialize to) mean
        /// a uniform pick, exactly as <c>EnemyMagicPlan.Select</c> reads them.
        /// </summary>
        private static Dictionary<DamageType, float> DamageTypesOfCasts(EnemySO enemy)
        {
            var byType = new Dictionary<DamageType, float>();
            if (enemy == null || enemy.Spells == null)
            {
                return byType;
            }

            var magics = new List<MagicSO>();
            var weights = new List<float>();
            float totalWeight = 0f;

            foreach (var entry in enemy.Spells)
            {
                if (entry == null || entry.Magic == null || entry.Magic.Effects == null)
                {
                    continue;
                }
                magics.Add(entry.Magic);
                float weight = Mathf.Max(0f, entry.CastWeight);
                weights.Add(weight);
                totalWeight += weight;
            }

            if (magics.Count == 0)
            {
                return byType;
            }

            bool uniform = totalWeight <= 0f;
            float assigned = 0f;

            for (int i = 0; i < magics.Count; i++)
            {
                float share = uniform ? 1f / magics.Count : weights[i] / totalWeight;
                if (share <= 0f)
                {
                    continue;
                }

                var types = DamagingTypesOf(magics[i]);
                if (types.Count == 0)
                {
                    // A heal or a pure debuff is a turn the party is not being damaged in. It is
                    // deliberately not redistributed: pretending it lands damage would inflate every
                    // element a support enemy happens to stand beside.
                    continue;
                }

                float perType = share / types.Count;
                foreach (var type in types)
                {
                    byType.TryGetValue(type, out float existing);
                    byType[type] = existing + perType;
                    assigned += perType;
                }
            }

            if (assigned <= 0f)
            {
                byType.Clear();
                return byType;
            }

            // Renormalise to the casting turns that actually deal damage, so the caller can multiply
            // by the full cast share without the healing turns silently shrinking the enemy.
            var normalised = new Dictionary<DamageType, float>(byType.Count);
            foreach (var kvp in byType)
            {
                normalised[kvp.Key] = kvp.Value / assigned;
            }
            return normalised;
        }

        private static List<DamageType> DamagingTypesOf(MagicSO magic)
        {
            var types = new List<DamageType>();

            // Only a spell aimed at the *other* side lands on the party. An enemy healer's Cure
            // targets its own side and is not incoming damage, however much it prolongs the fight -
            // the same distinction EnemyMagicModel.HitsHeroSide draws when it prices a cast.
            if (magic.TargetType != MagicTargetType.SingleEnemy
                && magic.TargetType != MagicTargetType.AllEnemies)
            {
                return types;
            }

            foreach (var effect in magic.Effects)
            {
                if (effect == null || effect.EffectType != SpellEffectType.Damage || effect.UnlockLevel > 0)
                {
                    continue;
                }
                if (!types.Contains(effect.DamageType))
                {
                    types.Add(effect.DamageType);
                }
            }
            return types;
        }
    }
}
