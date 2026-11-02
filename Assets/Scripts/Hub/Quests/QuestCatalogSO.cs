using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Hub.Quests
{
    /// <summary>
    /// Every quest the tavern offers, in board order (<c>Resources/QuestCatalog.asset</c>), loaded from
    /// Resources like the other catalogs so the tracker needs no scene wiring.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestCatalog", menuName = "SO/Quest Catalog")]
    public class QuestCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "QuestCatalog";

        public List<QuestSO> Quests = new List<QuestSO>();

        public static QuestCatalogSO Load()
        {
            return UnityEngine.Resources.Load<QuestCatalogSO>(ResourcePath);
        }
    }
}
