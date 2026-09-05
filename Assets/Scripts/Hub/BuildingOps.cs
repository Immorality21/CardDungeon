using System.Collections.Generic;
using Assets.Scripts.Progression;
using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// Every hub-building rule, as pure static functions of the authored <see cref="HubSO"/> plus the
    /// saved <see cref="BuildingProgress"/> list. Nothing here touches disk, singletons or scenes —
    /// the same shape as <see cref="Dungeon.CampaignOps"/> and <c>SphereGridOps</c>, and what makes
    /// the town EditMode-testable with no scene.
    ///
    /// <para>Towns are treated as untrusted data: a null building, an empty key or a duplicate
    /// degrades rather than throwing, and the authoring validators at the bottom report the faults
    /// so a test can fail on them instead of the game rendering them.</para>
    /// </summary>
    public static class BuildingOps
    {
        /// <summary>
        /// <b>The phase switch.</b> While true every lot reads as placed at level 1 whatever the save
        /// says, so <see cref="BuildingSO.RequiredRunKeys"/> and <see cref="BuildingSO.PlacementCost"/>
        /// are authored and not yet consulted.
        ///
        /// <para><c>docs/plans/HUB.md</c> §7 splits this on purpose: phase 2/3 land the data model and
        /// the town renderer while the game plays exactly as it did, and phase 4 turns the gates on
        /// against a hub that already works — migration risk kept apart from design risk. Flipping
        /// this to false is most of phase 4, and it is one constant because every reader below goes
        /// through <see cref="StateOf"/> and <see cref="LevelOf"/>.</para>
        /// </summary>
        public const bool EverythingIsPlaced = true;

        /// <summary>How this lot reads right now. See <see cref="EverythingIsPlaced"/>.</summary>
        public static BuildingState StateOf(BuildingSO building, IEnumerable<BuildingProgress> saved)
        {
            return StateOf(building, saved, EverythingIsPlaced);
        }

        /// <summary>
        /// <see cref="StateOf(BuildingSO, IEnumerable{BuildingProgress})"/> with the phase switch
        /// passed in, so the gated behaviour phase 4 will turn on is testable *now* rather than
        /// arriving untested on the day the constant flips.
        /// </summary>
        public static BuildingState StateOf(
            BuildingSO building, IEnumerable<BuildingProgress> saved, bool everythingIsPlaced)
        {
            if (building == null)
            {
                return BuildingState.Absent;
            }
            if (LevelOf(building, saved, everythingIsPlaced) > 0)
            {
                return BuildingState.Built;
            }
            // Offered once the runs behind it are cleared; a bare lot until then. The unlock record
            // is not threaded in yet, so an unbuilt lot with no requirement reads as Available and
            // one with a requirement reads as Absent.
            return building.RequiredRunKeys == null || building.RequiredRunKeys.Count == 0
                ? BuildingState.Available
                : BuildingState.Absent;
        }

        /// <summary>
        /// The level this lot is built to: 0 when unbuilt, at least 1 for anything placed, clamped to
        /// <see cref="BuildingSO.MaxLevel"/>. A <see cref="BuildingSO.PlacedByDefault"/> lot reads as
        /// level 1 with nothing in the save, which is why owning a working campfire needs no save
        /// write on a fresh profile.
        /// </summary>
        public static int LevelOf(BuildingSO building, IEnumerable<BuildingProgress> saved)
        {
            return LevelOf(building, saved, EverythingIsPlaced);
        }

        /// <summary>
        /// <see cref="LevelOf(BuildingSO, IEnumerable{BuildingProgress})"/> with the phase switch
        /// passed in. A saved level always wins over the switch, so a save written after phase 4 keeps
        /// meaning what it says.
        /// </summary>
        public static int LevelOf(
            BuildingSO building, IEnumerable<BuildingProgress> saved, bool everythingIsPlaced)
        {
            if (building == null)
            {
                return 0;
            }

            int level = 0;
            if (saved != null)
            {
                foreach (var entry in saved)
                {
                    if (entry != null && entry.Key == building.SaveKey)
                    {
                        level = Mathf.Max(level, entry.Level);
                    }
                }
            }

            if (level <= 0 && (building.PlacedByDefault || everythingIsPlaced))
            {
                level = 1;
            }
            return Mathf.Clamp(level, 0, Mathf.Max(1, building.MaxLevel));
        }

        /// <summary>Whether the lot is standing at all.</summary>
        public static bool IsBuilt(BuildingSO building, IEnumerable<BuildingProgress> saved)
        {
            return StateOf(building, saved) == BuildingState.Built;
        }

        /// <summary>
        /// The town's buildings in paint order: <see cref="BuildingSO.DrawOrder"/> first, then
        /// <c>Position.y</c> (a painter's algorithm — lower on the screen is nearer the camera), then
        /// list order so the sort is total and a re-run never reshuffles equal lots.
        ///
        /// <para>This exists because <b>UI Toolkit has no z-index</b>: siblings paint in the order
        /// they were added, so "which building is in front" is decided here or not at all.</para>
        /// </summary>
        public static List<BuildingSO> InDrawOrder(HubSO hub)
        {
            var result = new List<BuildingSO>();
            if (hub == null || hub.Buildings == null)
            {
                return result;
            }

            var indexOf = new Dictionary<BuildingSO, int>();
            foreach (var building in hub.Buildings)
            {
                if (building == null || string.IsNullOrEmpty(building.SaveKey) || indexOf.ContainsKey(building))
                {
                    continue;
                }
                indexOf[building] = result.Count;
                result.Add(building);
            }

            result.Sort((a, b) =>
            {
                int byOrder = a.DrawOrder.CompareTo(b.DrawOrder);
                if (byOrder != 0)
                {
                    return byOrder;
                }
                int byY = a.Position.y.CompareTo(b.Position.y);
                return byY != 0 ? byY : indexOf[a].CompareTo(indexOf[b]);
            });
            return result;
        }

        /// <summary>The screen-space rect a lot occupies, in the hub's reference pixels. The
        /// clickable box — see <see cref="BuildingSO.HitSize"/> on why it is rectangular.</summary>
        public static Rect LotRect(BuildingSO building)
        {
            if (building == null)
            {
                return new Rect();
            }
            var size = new Vector2(Mathf.Max(1f, building.HitSize.x), Mathf.Max(1f, building.HitSize.y));
            return new Rect(building.Position, size);
        }

        /// <summary>
        /// The sprite for a lot's current state, or null to render the flat placeholder. The view
        /// calls this and does not otherwise know what a state means, which is what keeps the town
        /// renderer free of rules.
        /// </summary>
        public static Sprite SpriteFor(BuildingSO building, IEnumerable<BuildingProgress> saved)
        {
            if (building == null)
            {
                return null;
            }

            switch (StateOf(building, saved))
            {
                case BuildingState.Available:
                    return building.AvailableSprite;
                case BuildingState.Built:
                    var sprites = building.LevelSprites;
                    if (sprites == null || sprites.Length == 0)
                    {
                        return null;
                    }
                    int level = Mathf.Max(1, LevelOf(building, saved));
                    return sprites[Mathf.Clamp(level - 1, 0, sprites.Length - 1)];
                default:
                    return building.AbsentSprite;
            }
        }

        // --- authoring validators -------------------------------------------------
        // Reported rather than thrown: a half-authored town should render, and a test should be what
        // fails. Same contract as CampaignOps' validators.

        /// <summary>Save keys used by more than one building — a save would record the wrong lot.</summary>
        public static List<string> GetDuplicateKeys(HubSO hub)
        {
            var duplicates = new List<string>();
            var seen = new List<string>();
            if (hub == null || hub.Buildings == null)
            {
                return duplicates;
            }

            foreach (var building in hub.Buildings)
            {
                if (building == null || string.IsNullOrEmpty(building.SaveKey))
                {
                    continue;
                }
                if (seen.Contains(building.SaveKey) && !duplicates.Contains(building.SaveKey))
                {
                    duplicates.Add(building.SaveKey);
                }
                seen.Add(building.SaveKey);
            }
            return duplicates;
        }

        /// <summary>Services no lot opens — a screen the player cannot reach.</summary>
        public static List<HubService> GetServicesWithNoBuilding(HubSO hub)
        {
            var missing = new List<HubService>();
            foreach (HubService service in System.Enum.GetValues(typeof(HubService)))
            {
                if (hub == null || hub.Find(service) == null)
                {
                    missing.Add(service);
                }
            }
            return missing;
        }

        /// <summary>Services opened by more than one lot — two doors to one room.</summary>
        public static List<HubService> GetDuplicateServices(HubSO hub)
        {
            var duplicates = new List<HubService>();
            if (hub == null || hub.Buildings == null)
            {
                return duplicates;
            }

            var seen = new List<HubService>();
            foreach (var building in hub.Buildings)
            {
                if (building == null)
                {
                    continue;
                }
                if (seen.Contains(building.Service) && !duplicates.Contains(building.Service))
                {
                    duplicates.Add(building.Service);
                }
                seen.Add(building.Service);
            }
            return duplicates;
        }

        /// <summary>
        /// Pairs of lots whose hit rects intersect. UI Toolkit hit-testing is rectangular, so an
        /// overlap means one lot silently swallows the other's clicks — the failure looks like a dead
        /// building rather than like a layout mistake, which is exactly why it needs a test.
        /// </summary>
        public static List<string> GetOverlappingLots(HubSO hub)
        {
            var overlaps = new List<string>();
            var buildings = InDrawOrder(hub);
            for (int i = 0; i < buildings.Count; i++)
            {
                for (int j = i + 1; j < buildings.Count; j++)
                {
                    if (LotRect(buildings[i]).Overlaps(LotRect(buildings[j])))
                    {
                        overlaps.Add($"{buildings[i].SaveKey} overlaps {buildings[j].SaveKey}");
                    }
                }
            }
            return overlaps;
        }

        /// <summary>Lots that sit even partly outside the hub's reference rect — content off-screen
        /// however the town is letterboxed.</summary>
        public static List<string> GetLotsOutsideTheRect(HubSO hub)
        {
            var outside = new List<string>();
            if (hub == null)
            {
                return outside;
            }

            var bounds = new Rect(Vector2.zero, hub.ReferenceSize);
            foreach (var building in InDrawOrder(hub))
            {
                var rect = LotRect(building);
                if (rect.xMin < bounds.xMin || rect.yMin < bounds.yMin
                    || rect.xMax > bounds.xMax || rect.yMax > bounds.yMax)
                {
                    outside.Add($"{building.SaveKey} at {rect} is outside {bounds}");
                }
            }
            return outside;
        }
    }
}
