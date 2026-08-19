using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>Share of an enemy set that behaves one particular way.</summary>
    public class ArchetypeShare
    {
        public EnemyArchetype Archetype;
        public float Weight;
        public float Share;
    }

    /// <summary>Two enemies offering substantially the same Draw list — the Draw system's variety leaking away.</summary>
    public class DrawOverlap
    {
        public EnemySO A;
        public EnemySO B;
        public float Share;
        public List<string> SharedMagic = new List<string>();
    }

    /// <summary>
    /// The "one-dimensional" axis. Stat balance can be perfect and the game still be boring: if every
    /// enemy is an Aggressor with no resistances and the same Draw list, then every fight is the same
    /// fight and the elemental layer never comes into play. These are the metrics that catch that,
    /// none of which are visible anywhere in the inspector.
    /// </summary>
    public class VarietyReport
    {
        public string Scope = "Project";

        public List<ArchetypeShare> Archetypes = new List<ArchetypeShare>();
        public EnemyArchetype DominantArchetype;
        public float DominantArchetypeShare;

        /// <summary>Weighted share of the enemy set carrying at least one resistance.</summary>
        public float ResistanceCoverage;
        public float ResistedWeight;
        public float TotalWeight;

        /// <summary>Damage types some magic deals but no enemy in scope resists — decorative elements.</summary>
        public List<DamageType> InertDamageTypes = new List<DamageType>();

        /// <summary>Damage types no magic in the catalog uses at all.</summary>
        public List<DamageType> UnusedDamageTypes = new List<DamageType>();

        public List<DrawOverlap> DrawOverlaps = new List<DrawOverlap>();

        /// <summary>Enemy pairs handing out the same loot item.</summary>
        public List<string> DuplicateLootPairs = new List<string>();

        public int DistinctDrawableMagic;
        public int CatalogMagicCount;
        public int EnemiesWithoutDrawList;

        public float DrawCoverage => CatalogMagicCount > 0
            ? (float)DistinctDrawableMagic / CatalogMagicCount
            : 0f;

        /// <summary>
        /// Builds the variety report for a weighted enemy set. Weights let the same code judge the
        /// whole project (weight 1 each) and a single level (expected spawn counts), which is what
        /// makes "this level is 92% Aggressor" answerable.
        /// </summary>
        public static VarietyReport Build(
            IList<WeightedEnemy> members,
            IList<MagicSO> magicCatalog,
            BalanceRulesSO rules,
            string scope = "Project")
        {
            var report = new VarietyReport { Scope = scope };
            if (members == null || members.Count == 0)
            {
                return report;
            }

            var byArchetype = new Dictionary<EnemyArchetype, float>();
            var resistedTypes = new HashSet<DamageType>();
            var drawKeys = new HashSet<string>();
            var lootOwners = new Dictionary<string, List<string>>();

            foreach (var member in members)
            {
                if (member == null || member.Definition == null)
                {
                    continue;
                }

                var enemy = member.Definition;
                float weight = member.Weight;
                report.TotalWeight += weight;

                if (!byArchetype.ContainsKey(enemy.Archetype))
                {
                    byArchetype[enemy.Archetype] = 0f;
                }
                byArchetype[enemy.Archetype] += weight;

                bool hasResistance = false;
                if (enemy.Resistances != null)
                {
                    foreach (var resistance in enemy.Resistances)
                    {
                        if (resistance == null || Mathf.Approximately(resistance.Percent, 0f))
                        {
                            continue;
                        }
                        hasResistance = true;
                        resistedTypes.Add(resistance.DamageType);
                    }
                }
                if (hasResistance)
                {
                    report.ResistedWeight += weight;
                }

                if (enemy.DrawableMagics == null || enemy.DrawableMagics.Count == 0)
                {
                    report.EnemiesWithoutDrawList++;
                }
                else
                {
                    foreach (var draw in enemy.DrawableMagics)
                    {
                        if (draw != null && draw.Magic != null)
                        {
                            drawKeys.Add(MagicKey(draw.Magic));
                        }
                    }
                }

                if (enemy.LootItem != null)
                {
                    string lootKey = enemy.LootItem.name;
                    if (!lootOwners.ContainsKey(lootKey))
                    {
                        lootOwners[lootKey] = new List<string>();
                    }
                    lootOwners[lootKey].Add(string.IsNullOrEmpty(enemy.DisplayName) ? enemy.name : enemy.DisplayName);
                }
            }

            foreach (var kvp in byArchetype)
            {
                float share = report.TotalWeight > 0f ? kvp.Value / report.TotalWeight : 0f;
                report.Archetypes.Add(new ArchetypeShare
                {
                    Archetype = kvp.Key,
                    Weight = kvp.Value,
                    Share = share
                });

                if (share > report.DominantArchetypeShare)
                {
                    report.DominantArchetypeShare = share;
                    report.DominantArchetype = kvp.Key;
                }
            }
            report.Archetypes.Sort((a, b) => b.Share.CompareTo(a.Share));

            report.ResistanceCoverage = report.TotalWeight > 0f
                ? report.ResistedWeight / report.TotalWeight
                : 0f;

            report.DistinctDrawableMagic = drawKeys.Count;
            report.CatalogMagicCount = magicCatalog != null ? magicCatalog.Count : 0;

            CollectDamageTypeUsage(magicCatalog, resistedTypes, report);
            CollectDrawOverlaps(members, report, rules);

            foreach (var kvp in lootOwners)
            {
                if (kvp.Value.Count > 1)
                {
                    report.DuplicateLootPairs.Add($"{kvp.Key} ← {string.Join(", ", kvp.Value)}");
                }
            }

            return report;
        }

        /// <summary>
        /// Cross-references the magic catalog against the resistances actually present. A damage type
        /// that spells deal but nothing resists is a mechanic the player can never engage with.
        /// </summary>
        private static void CollectDamageTypeUsage(
            IList<MagicSO> magicCatalog,
            HashSet<DamageType> resistedTypes,
            VarietyReport report)
        {
            var usedByMagic = new HashSet<DamageType>();
            if (magicCatalog != null)
            {
                foreach (var magic in magicCatalog)
                {
                    if (magic == null || magic.Effects == null)
                    {
                        continue;
                    }
                    foreach (var effect in magic.Effects)
                    {
                        if (effect != null && effect.EffectType == SpellEffectType.Damage)
                        {
                            usedByMagic.Add(effect.DamageType);
                        }
                    }
                }
            }

            foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
            {
                if (type == DamageType.Normal)
                {
                    continue;
                }

                if (!usedByMagic.Contains(type))
                {
                    report.UnusedDamageTypes.Add(type);
                }
                else if (!resistedTypes.Contains(type))
                {
                    report.InertDamageTypes.Add(type);
                }
            }
        }

        private static void CollectDrawOverlaps(IList<WeightedEnemy> members, VarietyReport report, BalanceRulesSO rules)
        {
            float threshold = rules != null ? rules.MaxDrawTableOverlap : 0.6f;

            for (int i = 0; i < members.Count; i++)
            {
                for (int j = i + 1; j < members.Count; j++)
                {
                    var a = members[i] != null ? members[i].Definition : null;
                    var b = members[j] != null ? members[j].Definition : null;
                    if (a == null || b == null || a == b)
                    {
                        continue;
                    }

                    var keysA = DrawKeys(a);
                    var keysB = DrawKeys(b);
                    if (keysA.Count == 0 || keysB.Count == 0)
                    {
                        continue;
                    }

                    var shared = new List<string>();
                    foreach (var key in keysA)
                    {
                        if (keysB.Contains(key))
                        {
                            shared.Add(key);
                        }
                    }

                    if (shared.Count == 0)
                    {
                        continue;
                    }

                    // Jaccard-style share so "both offer 2 of 2" reads as total overlap.
                    var union = new HashSet<string>(keysA);
                    union.UnionWith(keysB);
                    float share = (float)shared.Count / union.Count;

                    if (share >= threshold)
                    {
                        report.DrawOverlaps.Add(new DrawOverlap
                        {
                            A = a,
                            B = b,
                            Share = share,
                            SharedMagic = shared
                        });
                    }
                }
            }

            report.DrawOverlaps.Sort((x, y) => y.Share.CompareTo(x.Share));
        }

        private static HashSet<string> DrawKeys(EnemySO enemy)
        {
            var keys = new HashSet<string>();
            if (enemy.DrawableMagics == null)
            {
                return keys;
            }
            foreach (var draw in enemy.DrawableMagics)
            {
                if (draw != null && draw.Magic != null)
                {
                    keys.Add(MagicKey(draw.Magic));
                }
            }
            return keys;
        }

        private static string MagicKey(MagicSO magic)
        {
            return string.IsNullOrEmpty(magic.Key) ? magic.name : magic.Key;
        }
    }
}
