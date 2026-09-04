using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    /// <summary>What activating a node grants. One enum rather than node subclasses, because Unity's
    /// plain serialization cannot hold a heterogeneous list without [SerializeReference] — the same
    /// flat-payload idiom as <c>RoomEventOutcome</c>. Unused payload fields on a node are inert.</summary>
    public enum SphereNodeKind
    {
        Stat = 0,       // grants Gains (a StatBlock)
        Resistance = 1, // grants +1 resistance: +ResistPercent to ResistType
        MagicSlot = 2,  // grants +1 slot, i.e. one more known spell the hero can carry at a time
        MagicKnown = 3  // teaches GrantedMagicKey permanently - the only source of magic in the game
    }

    /// <summary>
    /// One node of a hero's sphere grid: a stable key, an authored XP price, a payload picked by
    /// <see cref="Kind"/>, and the undirected edges to its neighbours. Positions exist only for the
    /// graph UI/editor — nothing in the game rules reads them.
    /// </summary>
    [Serializable]
    public class SphereGridNode
    {
        [Tooltip("Stable save identifier. Written into Party.json when activated; treat as " +
                 "write-once, like HeroSO.Key — renaming it orphans every save that bought it.")]
        public string Key;

        [Tooltip("Optional player-facing name. Empty falls back to a description of the payload.")]
        public string DisplayName;

        public SphereNodeKind Kind = SphereNodeKind.Stat;

        [Tooltip("XP spent from the hero's bank to activate this node.")]
        public int XpCost = 25;

        [Tooltip("Authored 2D layout position, consumed only by the grid UI and editor window.")]
        public Vector2 Position;

        [Tooltip("Kind == Stat: what activating this node grants.")]
        public StatBlock Gains = new StatBlock();

        [Tooltip("Kind == Resistance: the damage type this node grants resistance to.")]
        public DamageType ResistType = DamageType.Fire;

        [Tooltip("Kind == Resistance: percent granted. Sums with innate, gear and other nodes.")]
        public float ResistPercent = 10f;

        [Tooltip("Kind == MagicKnown: MagicSO.Key of the magic this hero permanently learns. This " +
                 "is the ONLY way a hero ever gets a spell - Draw was removed on 2026-09-04. " +
                 "Learning is not carrying: a known spell still has to fit one of the hero's slots " +
                 "(see MagicSlot), which is what makes a branch a kit rather than a collection. A " +
                 "key with no catalog entry is skipped rather than failing.")]
        public string GrantedMagicKey;

        [Tooltip("Kind == MagicKnown: charges this spell starts a run with when carried. It refills " +
                 "at a refuge and nowhere else, so it is a run allowance, not a per-fight one - " +
                 "which makes it the node's real power dial, more so than XpCost.")]
        [Range(1, 9)]
        public int GrantedCharges = 2;

        [Tooltip("Keys of neighbouring nodes. Edges are undirected: listing B on A is enough.")]
        public List<string> Neighbors = new List<string>();
    }

    /// <summary>
    /// A hero's progression grid. Per-hero XP banked from kills is spent here at the hub —
    /// activation starts at <see cref="StartNodeKey"/> and grows along edges. All rules live in
    /// <see cref="SphereGridOps"/>; this asset is pure data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSphereGrid", menuName = "SO/Sphere Grid")]
    public class SphereGridSO : ScriptableObject
    {
        [Tooltip("Key of the entry node. Empty falls back to the first node in the list.")]
        public string StartNodeKey;

        public List<SphereGridNode> Nodes = new List<SphereGridNode>();
    }
}
