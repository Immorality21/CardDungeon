using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// The pure rules of the Ultra gauge and the transform (docs/plans/COMBAT_DEPTH.md §13). Covered
    /// by <c>UltraTests</c>.
    ///
    /// <para><b>The gauge fills on health lost, in this fight.</b> Losing <see cref="FillShare"/> of
    /// the hero's max health fills it from empty - a blow, a burn or a blood price all count, which is
    /// what makes the Warlock's own health costs feed his Demon Form. It starts every fight empty and
    /// is spent whole on use. Per fight rather than per run because a gauge carried between rooms would
    /// be charged in an easy room and spent in the next, which is not a comeback.</para>
    /// </summary>
    public static class UltraOps
    {
        /// <summary>A full gauge.</summary>
        public const int Max = 100;

        /// <summary>The share of a hero's max health they must lose in one fight to fill the gauge
        /// from empty. First-draft number.</summary>
        public const float FillShare = 0.6f;

        /// <summary>Gauge points for losing <paramref name="healthLost"/> of a <paramref name="maxHealth"/>
        /// bar. Rounded up, so any real loss moves it.</summary>
        public static int GainFor(int healthLost, int maxHealth)
        {
            if (healthLost <= 0 || maxHealth <= 0)
            {
                return 0;
            }
            return Mathf.CeilToInt(healthLost * Max / (maxHealth * FillShare));
        }

        /// <summary>The gauge after a gain, clamped to <see cref="Max"/>.</summary>
        public static int Add(int gauge, int gain)
        {
            return Mathf.Clamp(gauge + Mathf.Max(0, gain), 0, Max);
        }

        public static bool IsFull(int gauge)
        {
            return gauge >= Max;
        }

        /// <summary>
        /// Health after a max-health change that keeps the same share of the bar filled - half health
        /// before is half health after, both into the form and back out of it. A living unit never
        /// rounds down to 0.
        /// </summary>
        public static int KeepShare(int health, int oldMax, int newMax)
        {
            if (health <= 0 || oldMax <= 0 || newMax <= 0)
            {
                return Mathf.Max(0, health);
            }
            return Mathf.Clamp(Mathf.RoundToInt((float)health / oldMax * newMax), 1, newMax);
        }

        /// <summary>
        /// Every ability an Ultra grants, by key. Like a summon's, they are <see cref="MagicSO"/>
        /// assets reached through the Ultra and never through a grid, the Forge or a loadout, so every
        /// "can a hero learn this?" check must leave them out.
        /// </summary>
        public static HashSet<string> AbilityKeys(IEnumerable<UltraSO> ultras)
        {
            var keys = new HashSet<string>();
            if (ultras == null)
            {
                return keys;
            }
            foreach (var ultra in ultras)
            {
                if (ultra == null)
                {
                    continue;
                }
                if (ultra.Abilities != null)
                {
                    foreach (var ability in ultra.Abilities)
                    {
                        if (ability != null && !string.IsNullOrEmpty(ability.Key))
                        {
                            keys.Add(ability.Key);
                        }
                    }
                }
                // A mech's own Attack and Signature are reached through the Ultra too.
                if (ultra.Mech != null)
                {
                    foreach (var ability in new[] { ultra.Mech.AttackAbility, ultra.Mech.Signature })
                    {
                        if (ability != null && !string.IsNullOrEmpty(ability.Key))
                        {
                            keys.Add(ability.Key);
                        }
                    }
                }
                if (ultra.StatAbilities != null)
                {
                    foreach (var entry in ultra.StatAbilities)
                    {
                        if (entry != null && entry.Ability != null && !string.IsNullOrEmpty(entry.Ability.Key))
                        {
                            keys.Add(entry.Ability.Key);
                        }
                    }
                }
            }
            return keys;
        }

        /// <summary>Whether using <paramref name="ultra"/> asks for a target first (a Sacrifice does).</summary>
        public static bool NeedsTarget(UltraSO ultra)
        {
            return ultra != null && ultra.Kind == UltraKind.Sacrifice;
        }

        /// <summary>
        /// A Sacrifice horror's Attack: the <see cref="UltraSO.StatAbilities"/> entry for the sacrificed
        /// hero's highest stat among those listed (ties to the first listed). Null when nothing is listed.
        /// </summary>
        public static MagicSO PickStatAbility(UltraSO ultra, System.Func<Assets.Scripts.UnitStats.StatType, int> stat)
        {
            if (ultra == null || ultra.StatAbilities == null || stat == null)
            {
                return null;
            }
            UltraStatAbility best = null;
            int bestValue = int.MinValue;
            foreach (var entry in ultra.StatAbilities)
            {
                if (entry == null || entry.Ability == null)
                {
                    continue;
                }
                int value = stat(entry.Stat);
                if (value > bestValue)
                {
                    best = entry;
                    bestValue = value;
                }
            }
            return best != null ? best.Ability : null;
        }

        private static readonly Dictionary<string, MagicSO> CastableCache = new Dictionary<string, MagicSO>();

        /// <summary>
        /// The throwaway <see cref="MagicSO"/> a Strike resolves through - the same engine an ability
        /// uses, with no tags (no combo) and an <c>ultra:</c> key (no Forge bonus). Cached per Ultra,
        /// since the live fight and the balance model both cast it many times; never saved.
        /// </summary>
        public static MagicSO CastableFor(UltraSO ultra)
        {
            if (ultra == null)
            {
                return null;
            }
            string key = "ultra:" + ultra.Key;
            if (!CastableCache.TryGetValue(key, out var castable) || castable == null)
            {
                castable = ScriptableObject.CreateInstance<MagicSO>();
                castable.hideFlags = HideFlags.DontSave;
                castable.name = key;
                castable.Key = key;
                castable.DisplayName = ultra.Label;
                castable.Description = ultra.Description;
                castable.TargetType = ultra.TargetType;
                castable.Effects = new List<SpellEffect>(ultra.Effects ?? new List<SpellEffect>());
                castable.Tags = new List<MagicTag>();
                CastableCache[key] = castable;
            }
            return castable;
        }

        /// <summary>"Transforms for 3 turns · +50% health · Shadow attack · Chaos Bolt", or for a
        /// Strike its effects: "All enemies: 12 Fire damage · Burning 3/turn". With a
        /// <paramref name="caster"/> the numbers include their stats, as the ability picker's do.</summary>
        public static string Describe(UltraSO ultra, Combat.ICombatUnit caster = null)
        {
            if (ultra == null)
            {
                return "";
            }
            if (ultra.Kind == UltraKind.Sacrifice)
            {
                string creature = ultra.Creature != null ? ultra.Creature.Label : "a horror";
                return $"An ally falls for the rest of the floor; {creature} rises in their place, built off their stats, "
                       + "for the rest of the fight. Its attack follows their highest stat";
            }
            if (ultra.Kind == UltraKind.Mount)
            {
                var mount = new List<string> { "Rides a mech that takes the hits" };
                if (ultra.Mech != null && ultra.Mech.AttackAbility != null)
                {
                    mount.Add(ultra.Mech.AttackAbility.DisplayName);
                }
                if (ultra.Mech != null && ultra.Mech.Signature != null)
                {
                    mount.Add(ultra.Mech.Signature.DisplayName);
                }
                return string.Join(" · ", mount);
            }
            if (ultra.Kind == UltraKind.Strike)
            {
                var lines = AbilityDescriber.EffectLines(CastableFor(ultra), caster, null);
                return AbilityDescriber.TargetLabel(ultra.TargetType) + ": " + string.Join(" · ", lines);
            }
            var parts = new List<string> { ultra.Turns == 1 ? "Transforms for 1 turn" : $"Transforms for {ultra.Turns} turns" };
            if (ultra.MaxHealthPercent > 0)
            {
                parts.Add($"+{ultra.MaxHealthPercent}% health");
            }
            if (ultra.AttackDamageType != Combat.DamageType.Normal)
            {
                parts.Add($"{ultra.AttackDamageType} attack");
            }
            if (ultra.Abilities != null)
            {
                foreach (var ability in ultra.Abilities)
                {
                    if (ability != null)
                    {
                        parts.Add(ability.DisplayName);
                    }
                }
            }
            return string.Join(" · ", parts);
        }
    }
}
