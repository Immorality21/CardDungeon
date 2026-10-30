using System.Collections.Generic;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Hub.UI
{
    /// <summary>
    /// The decision layer between <see cref="BuildingOps"/> and <see cref="HubView"/>: turns a lot's
    /// state into the classes and text the town paints. Pure and scene-free, so the EditMode tests
    /// drive it directly — the same split <c>SphereGridPresenter</c> makes, and the reason the town
    /// renderer holds no rules of its own.
    /// </summary>
    /// <summary>The mark a lot carries in the town - see <see cref="HubPresenter.BadgeFor"/>.</summary>
    public enum LotBadge
    {
        None,
        // On offer, but the bag cannot pay for it yet: a dimmed hammer.
        Build,
        Locked,
        // Something waits inside a built lot: XP to spend, an upgrade to afford, a new entry to read.
        New,
        // Can be built or raised right now: the hammer, lit (playtest 3 finding 16). Appended.
        Ready,
    }

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
                        return Loc.Format("hub.lot.level-upgrade", ("level", level), ("cost", BuildingOps.UpgradeCost(building, progress)));
                    }
                    return building.MaxLevel > 1 ? Loc.Format("hub.lot.level-max", ("level", level)) : "";
                case BuildingState.Available:
                    string price = DescribePlacementCost(building);
                    return string.IsNullOrEmpty(price) ? Loc.Get("hub.lot.ready-to-build") : Loc.Format("hub.lot.needs", ("price", price));
                default:
                    return DescribeLock(building);
            }
        }

        /// <summary>
        /// The one mark a lot carries in the town, in place of a line of text: a hammer when there is
        /// something to build or raise there, a padlock while it is not on offer yet, nothing when it is
        /// finished. The details - the price, what it grants, what unlocks it - live on the lot panel a
        /// click away (the owner's call, 2026-09-28: a town where every building printed its costs
        /// read like a spreadsheet).
        ///
        /// <para>A standing lot's next level only earns the hammer once <paramref name="gold"/> covers
        /// it: the campfire showed one with its upgrade 265 gold out of reach, which read as a job
        /// waiting (playtest 2 finding 10). Leave it out to ignore money.</para>
        ///
        /// <para>What can be paid for <i>now</i> is <see cref="LotBadge.Ready"/>, a lit hammer; an
        /// unbuilt lot the bag cannot cover yet keeps a dimmed one (<paramref name="canPayPlacement"/>),
        /// because the town gave no way to tell "on offer" from "you can buy this" (playtest 3
        /// finding 16).</para>
        /// </summary>
        public static LotBadge BadgeFor(BuildingSO building, HubProgress progress, int gold = int.MaxValue,
            bool canPayPlacement = true)
        {
            if (building == null)
            {
                return LotBadge.None;
            }

            switch (BuildingOps.StateOf(building, progress))
            {
                case BuildingState.Built:
                    return BuildingOps.CanUpgrade(building, progress) && gold >= BuildingOps.UpgradeCost(building, progress)
                        ? LotBadge.Ready
                        : LotBadge.None;
                case BuildingState.Available:
                    return canPayPlacement ? LotBadge.Ready : LotBadge.Build;
                default:
                    return LotBadge.Locked;
            }
        }

        /// <summary>The USS class that draws a badge, or null for none.</summary>
        public static string BadgeClass(LotBadge badge)
        {
            switch (badge)
            {
                case LotBadge.Build:
                    return "hub-lot__badge--build";
                case LotBadge.Locked:
                    return "hub-lot__badge--locked";
                case LotBadge.New:
                    return "hub-lot__badge--new";
                case LotBadge.Ready:
                    return "hub-lot__badge--ready";
                default:
                    return null;
            }
        }

        /// <summary>Why an Absent lot is absent — which run has to fall first.</summary>
        public static string DescribeLock(BuildingSO building)
        {
            if (building == null || building.RequiredRunKeys == null || building.RequiredRunKeys.Count == 0)
            {
                return Loc.Get("hub.lot.not-yet");
            }
            return Loc.Get("hub.lot.locked");
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
                parts.Add(Loc.Format("hub.lot.cost-part", ("amount", line.Amount < 1 ? 1 : line.Amount), ("material", material)));
            }
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// The promise beside the price: what building or upgrading this lot would actually grant,
        /// as the lot itself words it. Empty when there is nothing to buy.
        ///
        /// <para>A hub upgrade must always answer "what do I get" <i>before</i> the player pays -
        /// that is the whole of <c>docs/plans/HUB.md</c> §7 phase 6, and
        /// <c>BuildingOps.GetLevelsWithNoGrant</c> is the test that keeps it true.</para>
        /// </summary>
        public static string DescribeNextGrant(BuildingSO building, HubProgress progress)
        {
            string grant = BuildingOps.GrantForNext(building, progress);
            if (string.IsNullOrWhiteSpace(grant))
            {
                return "";
            }
            int next = BuildingOps.NextLevel(building, progress);
            return next <= 1 ? grant : Loc.Format("hub.lot.next-grant", ("level", next), ("grant", grant));
        }

        /// <summary>
        /// What the lot's action button should say, given what the player can do with it right now.
        /// Empty means there is no action to offer.
        /// </summary>
        public static string ActionLabel(BuildingSO building, HubProgress progress)
        {
            // The price is in the lot panel's cost table beside it (2026-09-30); the button only
            // says what it does. It used to repeat the status line word for word.
            if (BuildingOps.CanPlace(building, progress))
            {
                return Loc.Get("hub.lot.action.build");
            }
            if (BuildingOps.CanUpgrade(building, progress))
            {
                return Loc.Format("hub.lot.action.upgrade", ("level", BuildingOps.NextLevel(building, progress)));
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
                    Frames = BuildingOps.FramesFor(building, progress),
                    FramesPerSecond = building.IdleFps,
                    Lights = BuildingOps.LightsFor(building, progress),
                    Tooltip = building.Blurb
                });
            }
        }
    }
}
