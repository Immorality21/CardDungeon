using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// The full list of every <see cref="EnemySO"/> in the game, loaded from Resources so the hub
    /// MenuScene - which spawns no enemies and wires no combat managers - can still render the
    /// bestiary. Same "auto-load from Resources" arrangement as
    /// <c>ItemCatalogSO</c>, and for the same reason.
    ///
    /// <para>It is the bestiary's <b>denominator</b>: the screen shows every entry here, locked
    /// until met, so "12 of 14 discovered" means something. An enemy missing from this list is
    /// invisible in the bestiary even after it has been fought, which is what
    /// <c>BestiaryCatalogTests</c> guards against.</para>
    ///
    /// <para>Lives at <c>Assets/Resources/EnemyCatalog.asset</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Enemy Catalog")]
    public class EnemyCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "EnemyCatalog";

        public List<EnemySO> Enemies = new List<EnemySO>();

        /// <summary>
        /// The catalog from Resources, or null when the asset is missing. Callers that render the
        /// bestiary should degrade to an empty screen rather than throwing.
        /// </summary>
        public static EnemyCatalogSO Load()
        {
            return UnityEngine.Resources.Load<EnemyCatalogSO>(ResourcePath);
        }
    }
}
