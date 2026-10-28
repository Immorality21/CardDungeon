using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Items.UI
{
    /// <summary>
    /// A material price as one rich-text line, each material "Name have/need", green when the bags
    /// cover it and red when they do not (playtest 3 finding 19). Shared by the Sphere Grid's node
    /// reason and the Merchant's belt upgrades so a price reads the same everywhere.
    /// </summary>
    public static class MaterialCostText
    {
        public const string HaveColour = "#8FE08F";
        public const string ShortColour = "#FF8C82";

        public static string Describe(IList<MaterialCost> costs)
        {
            var parts = new List<string>();
            if (costs == null || !InventoryManager.HasInstance)
            {
                return string.Empty;
            }
            foreach (var line in costs)
            {
                if (line == null || !line.IsValid)
                {
                    continue;
                }
                string name = string.IsNullOrEmpty(line.Material.DisplayName)
                    ? line.Material.Key
                    : line.Material.DisplayName;
                int need = Mathf.Max(1, line.Amount);
                int have = InventoryManager.Instance.GetMaterialQuantity(line.Material.Key);
                string colour = have >= need ? HaveColour : ShortColour;
                parts.Add($"<color={colour}>{name} {Mathf.Min(have, need)}/{need}</color>");
            }
            return string.Join(" · ", parts);
        }
    }
}
