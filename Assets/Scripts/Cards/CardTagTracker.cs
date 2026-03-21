using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    public class CardTagTracker
    {
        private Dictionary<ICombatUnit, HashSet<string>> _appliedTags = new Dictionary<ICombatUnit, HashSet<string>>();

        public void ApplyTags(ICombatUnit target, List<string> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return;
            }

            if (!_appliedTags.ContainsKey(target))
            {
                _appliedTags[target] = new HashSet<string>();
            }

            foreach (var tag in tags)
            {
                _appliedTags[target].Add(tag);
            }
        }

        public HashSet<string> GetTagsOnUnit(ICombatUnit unit)
        {
            if (_appliedTags.TryGetValue(unit, out var tags))
            {
                return tags;
            }
            return new HashSet<string>();
        }

        public void Clear()
        {
            _appliedTags.Clear();
        }
    }
}
