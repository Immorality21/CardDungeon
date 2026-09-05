using System.Collections.Generic;
using Assets.Scripts.Progression;
using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// Every hub-building rule, as pure static functions of the authored <see cref="HubSO"/> plus a
    /// <see cref="HubProgress"/>. Nothing here touches disk, singletons or scenes — the same shape as
    /// <see cref="Dungeon.CampaignOps"/> and <c>SphereGridOps</c>, and what makes the town
    /// EditMode-testable with no scene.
    ///
    /// <para>Money is deliberately <b>not</b> here. Whether the player can afford a lot depends on the
    /// inventory and the purse, which are singletons; <c>HubManager.CanAffordPlacement</c> asks them.
    /// Keeping the two apart is what lets the balance model reason about a hub state it has no wallet
    /// for — the same split <c>SphereGridOps.CanActivate</c> makes about material costs.</para>
    ///
    /// <para>Towns are treated as untrusted data: a null building, an empty key or a duplicate
    /// degrades rather than throwing, and the authoring validators at the bottom report the faults so
    /// a test can fail on them instead of the game rendering them.</para>
    /// </summary>
    public static class BuildingOps
    {
        /// <summary>
        /// How this lot reads right now.
        ///
        /// <para>Three states, and <see cref="BuildingState.Available"/> is the load-bearing one: a
        /// bare lot the player *could* build on is the affordance that makes a material worth wanting.
        /// Without it an unbuilt hub is indistinguishable from empty ground and nothing on screen ever
        /// explains why the player is carrying ember iron.</para>
        /// </summary>
        public static BuildingState StateOf(BuildingSO building, HubProgress progress)
        {
            if (building == null)
            {
                return BuildingState.Absent;
            }
            if (LevelOf(building, progress) > 0)
            {
                return BuildingState.Built;
            }
            return IsOffered(building, progress) ? BuildingState.Available : BuildingState.Absent;
        }

        /// <summary>
        /// Whether the campaign has opened this lot for building yet — every key in
        /// <see cref="BuildingSO.RequiredRunKeys"/> cleared. This is the pacing dial: one system
        /// arriving at a time, each introduced when the player has a reason to want it.
        /// </summary>
        public static bool IsOffered(BuildingSO building, HubProgress progress)
        {
            if (building == null)
            {
                return false;
            }
            if (building.RequiredRunKeys == null || building.RequiredRunKeys.Count == 0)
            {
                return true;
            }

            progress = progress ?? HubProgress.Fresh;
            foreach (var runKey in building.RequiredRunKeys)
            {
                if (!string.IsNullOrEmpty(runKey) && !progress.HasCleared(runKey))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The level this lot is built to: 0 when unbuilt, at least 1 for anything placed, clamped to
        /// <see cref="BuildingSO.MaxLevel"/>. A <see cref="BuildingSO.PlacedByDefault"/> lot reads as
        /// level 1 with nothing in the save, which is why owning a working campfire needs no save
        /// write on a fresh profile.
        /// </summary>
        public static int LevelOf(BuildingSO building, HubProgress progress)
        {
            if (building == null)
            {
                return 0;
            }

            int level = 0;
            var saved = (progress ?? HubProgress.Fresh).Buildings;
            foreach (var entry in saved)
            {
                if (entry != null && entry.Key == building.SaveKey)
                {
                    level = Mathf.Max(level, entry.Level);
                }
            }

            if (level <= 0 && building.PlacedByDefault)
            {
                level = 1;
            }
            return Mathf.Clamp(level, 0, Mathf.Max(1, building.MaxLevel));
        }

        /// <summary>Whether the lot is standing at all.</summary>
        public static bool IsBuilt(BuildingSO building, HubProgress progress)
        {
            return LevelOf(building, progress) > 0;
        }

        /// <summary>Whether this lot is waiting to be placed — offered, and not yet standing.</summary>
        public static bool CanPlace(BuildingSO building, HubProgress progress)
        {
            return StateOf(building, progress) == BuildingState.Available;
        }

        /// <summary>Whether this lot is standing and has a level left to buy.</summary>
        public static bool CanUpgrade(BuildingSO building, HubProgress progress)
        {
            if (building == null)
            {
                return false;
            }
            int level = LevelOf(building, progress);
            return level > 0 && level < building.MaxLevel;
        }

        /// <summary>The level a build or upgrade would take this lot to, or 0 when neither applies.</summary>
        public static int NextLevel(BuildingSO building, HubProgress progress)
        {
            if (CanPlace(building, progress))
            {
                return 1;
            }
            return CanUpgrade(building, progress) ? LevelOf(building, progress) + 1 : 0;
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

        /// <summary>
        /// The box a lot can be clicked in, in the hub's reference pixels. Rectangular, and the thing
        /// <c>HubContentTests</c> refuses to let two lots share — see <see cref="BuildingSO.HitSize"/>.
        /// </summary>
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
        /// Where the sprite paints, in the hub's reference pixels: <see cref="BuildingSO.DrawOffset"/>
        /// from the lot's corner at <see cref="BuildingSO.DrawSize"/>, falling back to the hit box when
        /// the draw size is unauthored. <b>Draw rects may overlap freely</b> — that is what makes a
        /// town look painted rather than tiled, and it is why this is a different rectangle from
        /// <see cref="LotRect"/>.
        /// </summary>
        public static Rect DrawRect(BuildingSO building)
        {
            if (building == null)
            {
                return new Rect();
            }

            var hit = LotRect(building);
            float width = building.DrawSize.x > 0f ? building.DrawSize.x : hit.width;
            float height = building.DrawSize.y > 0f ? building.DrawSize.y : hit.height;
            return new Rect(building.Position + building.DrawOffset, new Vector2(width, height));
        }

        /// <summary>
        /// The sprite for a lot's current state, or null to render the flat placeholder. The view
        /// calls this and does not otherwise know what a state means, which is what keeps the town
        /// renderer free of rules.
        /// </summary>
        public static Sprite SpriteFor(BuildingSO building, HubProgress progress)
        {
            if (building == null)
            {
                return null;
            }

            switch (StateOf(building, progress))
            {
                case BuildingState.Available:
                    return building.AvailableSprite;
                case BuildingState.Built:
                    var sprites = building.LevelSprites;
                    if (sprites == null || sprites.Length == 0)
                    {
                        return null;
                    }
                    int level = Mathf.Max(1, LevelOf(building, progress));
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
        /// Pairs of lots whose <b>hit</b> rects intersect. UI Toolkit hit-testing is rectangular, so an
        /// overlap means one lot silently swallows the other's clicks — the failure looks like a dead
        /// building rather than like a layout mistake, which is exactly why it needs a test. Draw rects
        /// are not checked and are meant to overlap.
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

        /// <summary>Lots whose hit box sits even partly outside the hub's reference rect — a lot the
        /// player cannot fully click, however the town is letterboxed.</summary>
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

        /// <summary>
        /// Lots that can never be placed on a fresh save because nothing offers them and nothing
        /// places them — a building the player would watch forever. Reported per building key.
        /// </summary>
        public static List<string> GetUnreachableLots(HubSO hub, ICollection<string> everyRunKey)
        {
            var stuck = new List<string>();
            foreach (var building in InDrawOrder(hub))
            {
                if (building.PlacedByDefault || building.RequiredRunKeys == null)
                {
                    continue;
                }
                foreach (var required in building.RequiredRunKeys)
                {
                    if (string.IsNullOrEmpty(required))
                    {
                        continue;
                    }
                    if (everyRunKey == null || !everyRunKey.Contains(required))
                    {
                        stuck.Add($"{building.SaveKey} requires run '{required}', which no run has");
                    }
                }
            }
            return stuck;
        }

        /// <summary>Lots authored as upgradable but with no price on the upgrade — free content that
        /// almost certainly meant to cost something.</summary>
        public static List<string> GetFreeUpgrades(HubSO hub)
        {
            var free = new List<string>();
            foreach (var building in InDrawOrder(hub))
            {
                if (building.IsUpgradable && building.GoldPerUpgrade <= 0)
                {
                    free.Add(building.SaveKey);
                }
            }
            return free;
        }
    }
}
