using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The town: every lot in it, the backdrop they sit on, and the coordinate space their positions
    /// are expressed in.
    ///
    /// <para>Loaded from Resources exactly like <see cref="Dungeon.CampaignSO"/> and
    /// <c>ItemCatalogSO</c>, so the hub resolves the whole town without scene wiring and without
    /// <c>AssetDatabase</c>, which does not exist in a build. Lives at
    /// <c>Assets/Resources/Hub.asset</c>.</para>
    ///
    /// <para>Progress is not stored here — it is <c>MetaProgressSaveData.Buildings</c>, so the asset
    /// stays pure content and one authored town reads differently per save. See
    /// <see cref="BuildingOps"/> for the rules that combine the two.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Hub")]
    public class HubSO : ScriptableObject
    {
        public const string ResourcePath = "Hub";

        public string DisplayName = "Hub";

        [Tooltip("The pixel rect every BuildingSO.Position is expressed in. The town scales as ONE " +
                 "unit - this rect is letterboxed into whatever space the screen gives it - because " +
                 "scaling lots individually desyncs the art from the hit-testing, the same trap " +
                 "cd-window--fixed exists to avoid. Changing this invalidates every authored " +
                 "position, so treat it as fixed once art exists.")]
        public Vector2 ReferenceSize = new Vector2(1280f, 720f);

        [Tooltip("Backdrop painted behind every lot. Null renders a flat ground colour, which is " +
                 "enough to play against before any art exists.")]
        public Sprite Backdrop;

        public List<BuildingSO> Buildings = new List<BuildingSO>();

        /// <summary>The building with this save key, or null.</summary>
        public BuildingSO Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            foreach (var building in Buildings)
            {
                if (building != null && building.SaveKey == key)
                {
                    return building;
                }
            }
            return null;
        }

        /// <summary>The building that opens this service, or null. One per service is a rule
        /// <c>HubContentTests</c> enforces, so the first match is the only match.</summary>
        public BuildingSO Find(HubService service)
        {
            foreach (var building in Buildings)
            {
                if (building != null && building.Service == service)
                {
                    return building;
                }
            }
            return null;
        }
    }
}
