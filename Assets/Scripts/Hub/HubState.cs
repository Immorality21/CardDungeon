using Assets.Scripts.Progression;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The one impure lookup: resolves the authored town and the current save from the singletons, so
    /// a screen can ask what level its own building is at.
    ///
    /// <para>It exists so <see cref="BuildingOps"/> does not have to. Every rule there is a pure
    /// function of a <see cref="HubProgress"/>, which is what lets the balance model and the EditMode
    /// tests reason about hub states no save ever held; this is the small, obvious place where the
    /// real save gets fetched instead. Screens outside the hub (the merchant, and whatever phase 6
    /// adds next) call this rather than reaching for <c>MetaProgressManager</c> themselves.</para>
    /// </summary>
    public static class HubState
    {
        /// <summary>The player's town as the rules see it, or a fresh one with no managers around.</summary>
        public static HubProgress Progress()
        {
            if (!MetaProgressManager.HasInstance)
            {
                return HubProgress.Fresh;
            }
            return new HubProgress(
                MetaProgressManager.Instance.GetBuildings(),
                MetaProgressManager.Instance.GetCompletedRunKeys());
        }

        /// <summary>The authored town, or null when the Resources asset is missing.</summary>
        public static HubSO Town()
        {
            return UnityEngine.Resources.Load<HubSO>(HubSO.ResourcePath);
        }

        /// <summary>
        /// What level the building behind <paramref name="service"/> is at: 0 when it is not built,
        /// 1 when it has just been placed, higher once upgraded.
        ///
        /// <para><b>Nothing calls this yet, deliberately.</b> The build/upgrade machinery is
        /// finished and tested, but <i>what a level grants</i> is an open design decision — see
        /// <c>docs/plans/HUB.md</c> §7 phase 6. This is the seam a screen uses when that is
        /// settled: the merchant would size its stock off it, the forge its discount, and so on.
        /// Until then every lot is authored <c>MaxLevel 1</c>, so no level is on sale that buys
        /// nothing.</para>
        ///
        /// <para>Returns 1 when there is no hub asset at all, so a scene without one degrades to
        /// the old fixed behaviour rather than to a screen offering nothing.</para>
        /// </summary>
        public static int LevelOf(HubService service)
        {
            var hub = Town();
            if (hub == null)
            {
                return 1;
            }
            return BuildingOps.LevelOf(hub.Find(service), Progress());
        }
    }
}
