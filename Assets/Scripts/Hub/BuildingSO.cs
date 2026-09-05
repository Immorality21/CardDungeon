using System;
using System.Collections.Generic;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// One lot in the town: what it opens, where it sits, and what it will eventually cost to place.
    /// Pure content — whether it is actually built lives in the save
    /// (<c>MetaProgressSaveData.Buildings</c>), the same split <see cref="Dungeon.CampaignSO"/> uses,
    /// so one authored town reads differently per save.
    ///
    /// <para><b>The cost and unlock fields are authored now and read by nothing yet.</b> That is
    /// deliberate: <c>docs/plans/HUB.md</c> §7 phase 2 lands the data model with every building
    /// pre-placed so the game plays exactly as it did, and phase 4 turns the gates on against a town
    /// that already renders. Authoring the fields from the start is open question 7's recommendation
    /// — it means phase 4 is a pricing pass rather than a schema change.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Hub Building")]
    public class BuildingSO : ScriptableObject
    {
        [Tooltip("Stable save identifier, written into Meta.json when the lot is built. Treat as " +
                 "write-once, like HeroSO.Key - renaming it orphans every save that built it.")]
        public string Key;

        [Tooltip("Name shown on the lot and in its tooltip. Safe to rename at any time.")]
        public string DisplayName;

        [TextArea(2, 4)]
        [Tooltip("One line of flavour for the lot's tooltip. Optional.")]
        public string Blurb;

        [Tooltip("Which hub screen clicking this lot opens. Exactly one building per service - a " +
                 "service with no lot is a screen nobody can reach, which HubContentTests fails on.")]
        public HubService Service;

        // --- layout ---------------------------------------------------------------

        [Tooltip("Top-left corner of the lot, in the reference-rect pixels declared by HubSO. " +
                 "Positions are meaningless without that rect, which is why it lives on the hub " +
                 "asset rather than being implied by whatever resolution the art happened to be.")]
        public Vector2 Position;

        [Tooltip("Clickable size of the lot, in the same reference-rect pixels. UI Toolkit " +
                 "hit-testing is RECTANGULAR - an overlapping silhouette steals its neighbour's " +
                 "clicks, so lots must not overlap however the art is drawn. HubContentTests fails " +
                 "on an overlap.")]
        public Vector2 HitSize = new Vector2(150f, 110f);

        [Tooltip("Paint order. UI Toolkit has no z-index: siblings paint in the order they are " +
                 "added, so this is the only way a building in front stays in front. Ties break on " +
                 "Position.y, which is the usual painter's-algorithm answer for a town.")]
        public int DrawOrder;

        // --- state art ------------------------------------------------------------

        [Tooltip("Absent: a bare lot. Null renders the flat placeholder, which is the whole " +
                 "placeholder-art bargain - the town has to be playable before it is drawn.")]
        public Sprite AbsentSprite;

        [Tooltip("Available: a foundation or scaffold. This is the state that makes a material " +
                 "worth wanting, so it should read as an invitation rather than as a ruin.")]
        public Sprite AvailableSprite;

        [Tooltip("Built, one sprite per level: element 0 is level 1. A level past the end of the " +
                 "list falls back to the last entry.")]
        public Sprite[] LevelSprites = new Sprite[0];

        // --- progression (authored now, read from HUB.md phase 4 onward) -----------

        [Tooltip("Placed on a fresh save, free, and can never be un-built. The campfire is the one " +
                 "building this is true of - the hub has to be usable in minute one.")]
        public bool PlacedByDefault;

        [Min(1)]
        [Tooltip("How far this building can be upgraded. 1 means placement is the whole of it.")]
        public int MaxLevel = 1;

        [Tooltip("PHASE 4. Materials to place the lot. Materials gate WHETHER - they only come out " +
                 "of runs, so they make a building depend on where the player has been.")]
        public List<MaterialCost> PlacementCost = new List<MaterialCost>();

        [Tooltip("PHASE 6. Gold for each upgrade past level 1. Gold gates WHEN - it keeps its " +
                 "tuition role and gives the hub a sink that scales forever.")]
        public int GoldPerUpgrade;

        [Tooltip("PHASE 4. Run keys that must be cleared before the lot is even offered. Empty " +
                 "means it is offered from the start.")]
        public List<string> RequiredRunKeys = new List<string>();

        /// <summary>The save identifier: authored <see cref="Key"/>, falling back to the asset name
        /// so a building authored before the field existed still resolves.</summary>
        public string SaveKey => string.IsNullOrEmpty(Key) ? name : Key;

        /// <summary>The name to put on screen: authored <see cref="DisplayName"/>, falling back to
        /// the save key. Never mix the two up — one is data, the other is a label.</summary>
        public string Label => string.IsNullOrEmpty(DisplayName) ? SaveKey : DisplayName;
    }
}
