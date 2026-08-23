using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Heroes.Editor
{
    /// <summary>
    /// Generates the four starter sphere grids and assigns them to their heroes. Code-generated
    /// rather than hand-typed YAML because ~17 nodes × (key, kind, cost, position, gains,
    /// neighbours) per hero is exactly the kind of data that gets mistyped silently — and this
    /// file doubles as executable documentation of the tuning. Idempotent: re-running overwrites
    /// each grid's contents in place, so asset GUIDs (and every reference to them) stay stable.
    /// Refinement afterwards happens in Tools ▸ Heroes ▸ Sphere Grid Editor.
    ///
    /// <para>Tuning intent: a 5-node spine re-grants the old level-2 gains for ~105 XP (the old
    /// level 2 cost 100), so the tutorial's XP buys the spine plus a first branch pick. The two
    /// old +5-Agility steps — flagged by the analyzer as +100% of base — are split +2/+2/+1 across
    /// spine and branches, so no single node moves an output stat by half its base. Costs escalate
    /// 15→80 outward; a full grid is ~650-750 XP, about two runs of headroom.</para>
    /// </summary>
    public static class SphereGridSeeder
    {
        private const string GridFolder = "Assets/ScriptableObjects/Heroes/Grids";

        // Spine runs upward from the start; branches fan right (A) and left (B).
        private const float SpineStep = 90f;

        [MenuItem("Tools/Heroes/Generate Starter Sphere Grids")]
        public static void Generate()
        {
            EnsureFolder();

            SeedWarrior();
            SeedTank();
            SeedScout();
            SeedAcolyte();

            AssetDatabase.SaveAssets();
            Debug.Log("Sphere grids generated/updated under " + GridFolder + ".");
        }

        // --- per-hero grids ----------------------------------------------------

        private static void SeedWarrior()
        {
            // Base STR10 END5 AGI5 INT3 SPR3 LCK5 HP13. Old L2: STR+3 END+1 HP+7 AGI+5 @100.
            var b = new GridBuilder("warrior");
            b.Spine(
                Stat(15, ("MaxHealth", 3)),
                Stat(20, ("Strength", 2)),
                Stat(25, ("Agility", 2)),
                Stat(20, ("MaxHealth", 4)),
                Stat(25, ("Strength", 1), ("Endurance", 1)));
            // Berserk: damage and crit, ending in reach (a slot) — the aggressive spend.
            b.Branch("berserk", 2, +1,
                Stat(35, ("Agility", 2)),
                Stat(45, ("Strength", 2)),
                Stat(45, ("Luck", 2)),
                Stat(60, ("Strength", 3)),
                Resist(60, DamageType.Fire, 15f),
                Slot(80));
            // Bulwark: toughness — the Warrior borrowing the Tank's job.
            b.Branch("bulwark", 4, -1,
                Stat(35, ("Endurance", 2)),
                Stat(40, ("MaxHealth", 5)),
                Stat(40, ("Agility", 1)),
                Stat(50, ("Endurance", 2)),
                Stat(55, ("MaxHealth", 6)),
                Resist(60, DamageType.Shadow, 15f));
            b.WriteAndAssign("Warrior");
        }

        private static void SeedTank()
        {
            // Base STR5 END15 AGI5 INT2 SPR6 LCK3 HP17. Old L2: STR+1 END+3 HP+9 AGI+5 @100.
            var b = new GridBuilder("tank");
            b.Spine(
                Stat(15, ("MaxHealth", 4)),
                Stat(20, ("Endurance", 2)),
                Stat(25, ("Agility", 2)),
                Stat(20, ("MaxHealth", 5)),
                Stat(25, ("Strength", 1), ("Endurance", 1)));
            // Immovable: the wall — HP and elemental soak.
            b.Branch("immovable", 4, -1,
                Stat(40, ("Endurance", 3)),
                Stat(45, ("MaxHealth", 7)),
                Resist(55, DamageType.Ice, 15f),
                Stat(60, ("MaxHealth", 8)),
                Resist(65, DamageType.Fire, 15f));
            // Vanguard: speed and a caster's reach — the Tank that acts, not just absorbs.
            b.Branch("vanguard", 2, +1,
                Stat(35, ("Agility", 2)),
                Stat(40, ("Agility", 1)),
                Stat(45, ("Spirit", 2)),
                Stat(55, ("Strength", 2)),
                Slot(80));
            b.WriteAndAssign("Tank");
        }

        private static void SeedScout()
        {
            // Base STR8 END3 AGI9 INT4 SPR2 LCK12 HP10. Old L2: STR+2 END+1 HP+5 AGI+2 @100.
            var b = new GridBuilder("scout");
            b.Spine(
                Stat(15, ("MaxHealth", 2)),
                Stat(20, ("Agility", 1)),
                Stat(20, ("Strength", 1)),
                Stat(20, ("MaxHealth", 3)),
                Stat(30, ("Strength", 1), ("Endurance", 1), ("Agility", 1)));
            // Skirmisher: speed and luck — first strike, more crits, extra reach.
            b.Branch("skirmisher", 2, +1,
                Stat(40, ("Agility", 2)),
                Stat(40, ("Luck", 3)),
                Stat(50, ("Agility", 2)),
                Slot(70),
                Resist(55, DamageType.Lightning, 15f));
            // Survivor: the fragile chassis toughened up.
            b.Branch("survivor", 4, -1,
                Stat(35, ("MaxHealth", 3)),
                Stat(35, ("Endurance", 1)),
                Stat(45, ("MaxHealth", 4)),
                Stat(45, ("Luck", 2)),
                Resist(55, DamageType.Shadow, 15f));
            b.WriteAndAssign("Scout");
        }

        private static void SeedAcolyte()
        {
            // Base STR4 END8 AGI6 INT10 SPR12 LCK4 HP19. Old L2: STR+1 END+2 HP+10 AGI+1 INT+1 SPR+1 @100.
            var b = new GridBuilder("acolyte");
            b.Spine(
                Stat(15, ("MaxHealth", 4)),
                Stat(20, ("Intelligence", 1)),
                Stat(20, ("Spirit", 1)),
                Stat(25, ("MaxHealth", 6)),
                Stat(25, ("Strength", 1), ("Endurance", 2), ("Agility", 1)));
            // Arcanist: offensive casting. The caster is the class whose grid grows the Draw kit,
            // which is why the Acolyte gets two slot nodes where everyone else gets one.
            b.Branch("arcanist", 2, +1,
                Stat(40, ("Intelligence", 2)),
                Slot(60),
                Stat(55, ("Intelligence", 3)),
                Resist(55, DamageType.Shadow, 15f));
            // Devout: the restorative kit, deeper.
            b.Branch("devout", 4, -1,
                Stat(40, ("Spirit", 2)),
                Stat(45, ("MaxHealth", 7)),
                Stat(55, ("Spirit", 3)),
                Resist(55, DamageType.Holy, 15f),
                Slot(80));
            b.WriteAndAssign("Acolyte");
        }

        // --- node shorthand -------------------------------------------------------

        private struct NodeSpec
        {
            public SphereNodeKind Kind;
            public int Cost;
            public (string Stat, int Amount)[] Gains;
            public DamageType ResistType;
            public float ResistPercent;
        }

        private static NodeSpec Stat(int cost, params (string, int)[] gains)
        {
            return new NodeSpec { Kind = SphereNodeKind.Stat, Cost = cost, Gains = gains };
        }

        private static NodeSpec Resist(int cost, DamageType type, float percent)
        {
            return new NodeSpec
            {
                Kind = SphereNodeKind.Resistance,
                Cost = cost,
                ResistType = type,
                ResistPercent = percent
            };
        }

        private static NodeSpec Slot(int cost)
        {
            return new NodeSpec { Kind = SphereNodeKind.MagicSlot, Cost = cost };
        }

        // --- builder -------------------------------------------------------------------

        private class GridBuilder
        {
            private readonly string _prefix;
            private readonly List<SphereGridNode> _nodes = new List<SphereGridNode>();
            private readonly List<string> _spineKeys = new List<string>();

            public GridBuilder(string prefix)
            {
                _prefix = prefix;
            }

            /// <summary>The trunk: first node is the start, each links to the previous.</summary>
            public void Spine(params NodeSpec[] specs)
            {
                for (int i = 0; i < specs.Length; i++)
                {
                    string key = i == 0 ? _prefix + "-start" : _prefix + "-spine-" + i;
                    var node = Make(key, specs[i], new Vector2(0f, -SpineStep * i));
                    if (i > 0)
                    {
                        node.Neighbors.Add(_spineKeys[i - 1]);
                    }
                    _nodes.Add(node);
                    _spineKeys.Add(key);
                }
            }

            /// <summary>A side chain hanging off spine node <paramref name="spineIndex"/>,
            /// fanning to <paramref name="side"/> (+1 right / -1 left).</summary>
            public void Branch(string name, int spineIndex, int side, params NodeSpec[] specs)
            {
                var anchor = new Vector2(0f, -SpineStep * spineIndex);
                string previous = _spineKeys[spineIndex];
                for (int i = 0; i < specs.Length; i++)
                {
                    string key = _prefix + "-" + name + "-" + (i + 1);
                    var position = anchor + new Vector2(side * (110f + 95f * i), -55f * (i + 1));
                    var node = Make(key, specs[i], position);
                    node.Neighbors.Add(previous);
                    _nodes.Add(node);
                    previous = key;
                }
            }

            private static SphereGridNode Make(string key, NodeSpec spec, Vector2 position)
            {
                var node = new SphereGridNode
                {
                    Key = key,
                    Kind = spec.Kind,
                    XpCost = spec.Cost,
                    Position = position,
                    ResistType = spec.ResistType,
                    ResistPercent = spec.ResistPercent
                };
                if (spec.Gains != null)
                {
                    foreach (var (stat, amount) in spec.Gains)
                    {
                        node.Gains.Add(ParseStat(stat), amount);
                    }
                }
                return node;
            }

            private static StatType ParseStat(string name)
            {
                return (StatType)System.Enum.Parse(typeof(StatType), name);
            }

            /// <summary>Writes the grid asset (creating or overwriting in place, so the GUID is
            /// stable) and assigns it to the hero whose <c>SaveKey</c> matches.</summary>
            public void WriteAndAssign(string heroKey)
            {
                string path = $"{GridFolder}/{heroKey}Grid.asset";
                var grid = AssetDatabase.LoadAssetAtPath<SphereGridSO>(path);
                bool fresh = grid == null;
                if (fresh)
                {
                    grid = ScriptableObject.CreateInstance<SphereGridSO>();
                }

                Undo.RecordObject(grid, "Generate Sphere Grid");
                grid.StartNodeKey = _prefix + "-start";
                grid.Nodes = _nodes;

                if (fresh)
                {
                    AssetDatabase.CreateAsset(grid, path);
                }
                EditorUtility.SetDirty(grid);

                var hero = FindHero(heroKey);
                if (hero == null)
                {
                    Debug.LogWarning($"SphereGridSeeder: no HeroSO with SaveKey '{heroKey}' — grid written but unassigned.");
                    return;
                }
                if (hero.SphereGrid != grid)
                {
                    Undo.RecordObject(hero, "Assign Sphere Grid");
                    hero.SphereGrid = grid;
                    EditorUtility.SetDirty(hero);
                }
            }

            private static HeroSO FindHero(string saveKey)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:HeroSO"))
                {
                    var hero = AssetDatabase.LoadAssetAtPath<HeroSO>(AssetDatabase.GUIDToAssetPath(guid));
                    if (hero != null && hero.SaveKey == saveKey)
                    {
                        return hero;
                    }
                }
                return null;
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(GridFolder))
            {
                AssetDatabase.CreateFolder("Assets/ScriptableObjects/Heroes", "Grids");
            }
        }
    }
}
