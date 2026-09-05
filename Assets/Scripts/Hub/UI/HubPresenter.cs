using System.Collections.Generic;
using Assets.Scripts.Progression;

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
        /// The single character drawn on a lot. Placeholder art's whole job is to be *distinguishable*
        /// — with flat rectangles the glyph plus the label is all that tells the Forge from the
        /// Bestiary, so this exists until real sprites do.
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
        public static string DescribeState(BuildingSO building, IEnumerable<BuildingProgress> saved)
        {
            if (building == null)
            {
                return "";
            }

            switch (BuildingOps.StateOf(building, saved))
            {
                case BuildingState.Built:
                    int level = BuildingOps.LevelOf(building, saved);
                    return building.MaxLevel > 1 ? $"Level {level} of {building.MaxLevel}" : "";
                case BuildingState.Available:
                    string price = DescribePlacementCost(building);
                    return string.IsNullOrEmpty(price) ? "Ready to build" : "Needs " + price;
                default:
                    return "Not yet";
            }
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

        /// <summary>Whether clicking this lot should open its service. An unbuilt lot is scenery.</summary>
        public static bool IsOpenable(BuildingSO building, IEnumerable<BuildingProgress> saved)
        {
            return BuildingOps.IsBuilt(building, saved);
        }

        /// <summary>
        /// The town as <see cref="HubView"/> input: one LotInfo per keyed building, already in paint
        /// order. Shared by the hub screen and (later) any authoring window, so the two cannot render
        /// different towns from one asset.
        /// </summary>
        public static void BuildViewModel(
            HubSO hub, IEnumerable<BuildingProgress> saved, List<HubView.LotInfo> lots)
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
                    Rect = BuildingOps.LotRect(building),
                    Label = building.Label,
                    Glyph = Glyph(building),
                    Sprite = BuildingOps.SpriteFor(building, saved),
                    Tooltip = building.Blurb
                });
            }
        }
    }
}
