using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    public class ComboDetector
    {
        private List<CardComboSO> _combos;

        public ComboDetector(List<CardComboSO> combos)
        {
            _combos = combos ?? new List<CardComboSO>();
        }

        /// <summary>
        /// Check if playing a card with the given tags on a target triggers any combo.
        /// The target must already have some tags applied from previous cards.
        /// </summary>
        public CardComboSO DetectCombo(List<CardTag> incomingTags, ICombatUnit target, CardTagTracker tagTracker)
        {
            if (incomingTags == null || incomingTags.Count == 0)
            {
                return null;
            }

            var existingTags = tagTracker.GetTagsOnUnit(target);
            if (existingTags.Count == 0)
            {
                return null;
            }

            // Combine existing tags on target with the new card's tags
            var allTags = new HashSet<CardTag>(existingTags);
            foreach (var tag in incomingTags)
            {
                allTags.Add(tag);
            }

            // Check each combo definition
            foreach (var combo in _combos)
            {
                if (combo.RequiredTags.Count == 0)
                {
                    continue;
                }

                bool allRequired = combo.RequiredTags.All(t => allTags.Contains(t));
                if (allRequired)
                {
                    // Verify at least one required tag came from existing and one from incoming
                    bool hasExisting = combo.RequiredTags.Any(t => existingTags.Contains(t));
                    bool hasIncoming = combo.RequiredTags.Any(t => incomingTags.Contains(t));

                    if (hasExisting && hasIncoming)
                    {
                        return combo;
                    }
                }
            }

            return null;
        }
    }
}
