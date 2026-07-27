using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards
{
    public class TagEntry
    {
        public MagicTag Tag;
        public int TurnsRemaining;
    }

    public class MagicTagTracker
    {
        private Dictionary<ICombatUnit, List<TagEntry>> _appliedTags = new Dictionary<ICombatUnit, List<TagEntry>>();

        public void ApplyTags(ICombatUnit target, List<MagicTag> tags, int duration)
        {
            if (tags == null || tags.Count == 0)
            {
                return;
            }

            if (!_appliedTags.ContainsKey(target))
            {
                _appliedTags[target] = new List<TagEntry>();
            }

            var entries = _appliedTags[target];
            foreach (var tag in tags)
            {
                // If tag already exists, refresh to the longer duration
                var existing = entries.Find(e => e.Tag == tag);
                if (existing != null)
                {
                    if (duration > existing.TurnsRemaining)
                    {
                        existing.TurnsRemaining = duration;
                    }
                }
                else
                {
                    entries.Add(new TagEntry { Tag = tag, TurnsRemaining = duration });
                }
            }
        }

        public HashSet<MagicTag> GetTagsOnUnit(ICombatUnit unit)
        {
            if (_appliedTags.TryGetValue(unit, out var entries))
            {
                return new HashSet<MagicTag>(entries.Select(e => e.Tag));
            }
            return new HashSet<MagicTag>();
        }

        /// <summary>
        /// Tick tag durations for a unit. Call this on the affected unit's turn only.
        /// </summary>
        public void TickTags(ICombatUnit unit)
        {
            if (!_appliedTags.TryGetValue(unit, out var entries))
            {
                return;
            }

            foreach (var entry in entries)
            {
                entry.TurnsRemaining--;
            }

            entries.RemoveAll(e => e.TurnsRemaining <= 0);

            if (entries.Count == 0)
            {
                _appliedTags.Remove(unit);
            }
        }

        public void Clear()
        {
            _appliedTags.Clear();
        }
    }
}
