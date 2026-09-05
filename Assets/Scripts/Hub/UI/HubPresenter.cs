using System.Collections.Generic;

namespace Assets.Scripts.Hub.UI
{
    /// <summary>
    /// The decision layer between <see cref="BuildingOps"/> and <see cref="HubView"/>: turns a lot's
    /// state into the classes and text the town paints. Pure and scene-free, so the EditMode tests
    /// drive it directly — the same split <c>SphereGridPresenter</c> makes, and the reason the town
    /// renderer holds no rules of its own.
    /// </summary>
    public static class HubPresenter
    {
        /// <summary>The hub-* USS class for a lot's state (see CardDungeon.uss).</summary>
        public static string StateClass(BuildingState state)
        {
            switch (state)
            {
                case BuildingState.Built:
                    return "hub-lot--built";
                case BuildingState.Available:
                    return "hub-lot--available";
                default:
                    return "hub-lot--absent";
            }
        }

        /// <summary>Every state class, so the view can clear the old one without knowing them.</summary>
        public static readonly string[] StateClasses =
        {
            "hub-lot--built", "hub-lot--available", "hub-lot--absent"
        };

        /// <summary>
        /// The single character drawn on a lot that has no art. Placeholder art's whole job is to be
        /// *distinguishable* — with flat rectangles the glyph plus the label is all that tells the
        /// Forge from the Bestiary. A lot with a real sprite hides it.
        /// </summary>
        public static string Glyph(BuildingSO building)
        {
            if (building == null)
            {
                return "?";
            }

            switch (building.Service)
            {
                case HubService.Party:
                    return "🔥";
                case HubService.Merchant:
                    return "⚖";
                case HubService.Forge:
                    return "✦";
                case HubService.Inventory:
                    return "▣";
                case HubService.Bestiary:
                    return "☘";
                case HubService.SphereGrid:
                    return "◈";
                default:
                    return "?";
            }
        }

        /// <summary>
        /// The line under a lot's name: what it is, or what it is waiting for. An
        /// <see cref="BuildingState.Available"/> lot has to say what it would cost, or the bare
        /// foundation is a mystery rather than an invitation.
        /// </summary>
        public static string DescribeState(BuildingSO building, HubProgress progress)
        {
            if (building == null)
            {
                return "";
            }

            switch (BuildingOps.StateOf(building, progress))
            {
                case BuildingState.Built:
                    int level = BuildingOps.LevelOf(building, progress);
                    if (BuildingOps.CanUpgrade(building, progress))
                    {
                        return $"Level {level} · upgrade {building.GoldPerUpgrade}g";
                    }
                    return building.MaxLevel > 1 ? $"Level {level}" : "";
                case BuildingState.Available:
                    string price = DescribePlacementCost(building);
                    return string.IsNullOrEmpty(price) ? "Ready to build" : "Needs " + price;
                default:
                    return DescribeLock(building);
            }
        }

        /// <summary>Why an Absent lot is absent — which run has to fall first.</summary>
        public static string DescribeLock(BuildingSO building)
        {
            if (building == null || building.RequiredRunKeys == null || building.RequiredRunKeys.Count == 0)
            {
                return "Not yet";
            }
            return "Locked";
        }

        /// <summary>A lot's placement price as one line ("2 Ember Iron · 1 Void Shard"), or empty
        /// when it asks for nothing.</summary>
        public static string DescribePlacementCost(BuildingSO building)
        {
            if (building == null || building.PlacementCost == null)
            {
                return "";
            }

            var parts = new List<string>();
            foreach (var line in building.PlacementCost)
            {
                if (line == null || !line.IsValid)
                {
                    continue;
                }
                string material = string.IsNullOrEmpty(line.Material.DisplayName)
                    ? line.Material.Key
                    : line.Material.DisplayName;
                parts.Add($"{(line.Amount < 1 ? 1 : line.Amount)} {material}");
            }
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// What the lot's action button should say, given what the player can do with it right now.
        /// Empty means there is no action to offer.
        /// </summary>
        public static string ActionLabel(BuildingSO building, HubProgress progress)
        {
            if (BuildingOps.CanPlace(building, progress))
            {
                string price = DescribePlacementCost(building);
                return string.IsNullOrEmpty(price) ? "Build" : "Build — " + price;
            }
            if (BuildingOps.CanUpgrade(building, progress))
            {
                return $"Upgrade — {building.GoldPerUpgrade} gold";
            }
            return "";
        }

        /// <summary>Whether clicking this lot should open its service. An unbuilt lot is scenery.</summary>
        public static bool IsOpenable(BuildingSO building, HubProgress progress)
        {
            return BuildingOps.IsBuilt(building, progress);
        }

        /// <summary>
        /// Whether a click should stop at the lot panel rather than going straight into the service.
        /// A lot that is finished — built and at its ceiling — opens immediately, because making the
        /// player pass through a panel on every merchant visit is a tax on the common case. Anything
        /// with a decision attached shows the panel first.
        /// </summary>
        public static bool NeedsPanel(BuildingSO building, HubProgress progress)
        {
            return !BuildingOps.IsBuilt(building, progress) || BuildingOps.CanUpgrade(building, progress);
        }

        /// <summary>
        /// The town as <see cref="HubView"/> input: one LotInfo per keyed building, already in paint
        /// order. Shared by the hub screen and (later) any authoring window, so the two cannot render
        /// different towns from one asset.
        /// </summary>
        public static void BuildViewModel(HubSO hub, HubProgress progress, List<HubView.LotInfo> lots)
        {
            lots.Clear();
            if (hub == null)
            {
                return;
            }

            foreach (var building in BuildingOps.InDrawOrder(hub))
            {
                lots.Add(new HubView.LotInfo
                {
                    Key = building.SaveKey,
                    HitRect = BuildingOps.LotRect(building),
                    DrawRect = BuildingOps.DrawRect(building),
                    Label = building.Label,
                    Glyph = Glyph(building),
                    Sprite = BuildingOps.SpriteFor(building, progress),
                    Tooltip = building.Blurb
                });
            }
        }
    }
}
