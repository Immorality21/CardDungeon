using System.Collections.Generic;
using Assets.Scripts.Items;

namespace Assets.Scripts.Combat.Triggers
{
    /// <summary>Turns the trigger lists authored on definitions into what a unit carries - shared by the
    /// live units and the simulator's, so both read an item's or an enemy's triggers the same way.</summary>
    public static class TriggerSources
    {
        /// <summary>Every trigger in <paramref name="triggers"/>, credited to <paramref name="from"/>.</summary>
        public static IEnumerable<CarriedTrigger> From(IEnumerable<TriggeredEffect> triggers, string from)
        {
            if (triggers == null)
            {
                yield break;
            }
            foreach (var trigger in triggers)
            {
                if (trigger != null)
                {
                    yield return new CarriedTrigger(trigger, from);
                }
            }
        }

        /// <summary>Every trigger the given gear carries, each credited to its item.</summary>
        public static IEnumerable<CarriedTrigger> FromItems(IEnumerable<ItemSO> items)
        {
            if (items == null)
            {
                yield break;
            }
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                foreach (var carried in From(item.Triggers, item.DisplayName))
                {
                    yield return carried;
                }
            }
        }
    }
}
