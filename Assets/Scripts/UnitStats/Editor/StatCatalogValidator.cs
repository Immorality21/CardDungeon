using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.UnitStats.Editor
{
    /// <summary>
    /// Reports a <see cref="StatType"/> that has no <see cref="StatCatalog"/> row, on every editor
    /// load and script reload.
    ///
    /// <para>This exists because a missing row fails <b>quietly</b>. Nothing throws: every stat loop
    /// in the game iterates <see cref="StatCatalog.Types"/>, which is built from the rows, so an
    /// uncatalogued stat is simply absent from recruit pricing, the power score, the inspector
    /// drawer, the tavern and hub stat lines and every analyzer column — while still being storable,
    /// selectable in dropdowns, and summed into gear bonuses. That is precisely the shape of the
    /// original bug (<c>ShopPricing</c> priced four of seven stats, so a caster's Intelligence and
    /// Spirit were free), moved one level up.</para>
    ///
    /// <para><c>StatCatalogTests</c> also covers it, but a test only fails once someone runs it. This
    /// puts the same message in the console the moment the code compiles.</para>
    /// </summary>
    public static class StatCatalogValidator
    {
        [InitializeOnLoadMethod]
        private static void Validate()
        {
            List<StatType> missing = StatCatalog.MissingRows();
            if (missing.Count == 0)
            {
                return;
            }

            var names = new string[missing.Count];
            for (int i = 0; i < missing.Count; i++)
            {
                names[i] = "StatType." + missing[i];
            }

            Debug.LogError(
                "StatCatalog has no row for: " + string.Join(", ", names)
                + ".\nAdd one to StatCatalog.Definitions. Until then these stats are invisible to "
                + "recruit pricing, the balance power score, the StatBlock inspector drawer, the "
                + "tavern and inventory stat lines and every Balance Analyzer column — they will "
                + "still be storable and still fold into gear bonuses, so nothing will throw.");
        }
    }
}
