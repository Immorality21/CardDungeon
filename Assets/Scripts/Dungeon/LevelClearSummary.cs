using System.Collections.Generic;
using Assets.Scripts.Items;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// Everything taking the stairs banked, for the "Level Cleared" window shown before the hub.
    ///
    /// <para>Until 2026-09-28 descending went straight to the hub. Gold went 0 → 50 and Essence
    /// 0 → 5, but only the +5 of a fight and the +15 of an event had ever been shown: the level-clear
    /// bonus and the Essence were paid silently, and the floor's gold only became real on the way
    /// out (playtest finding 6). The window adds it up, so every number the hub shows has a line
    /// that explains it.</para>
    /// </summary>
    public class LevelClearSummary
    {
        public struct Line
        {
            public string Label;
            public int Amount;

            public Line(string label, int amount)
            {
                Label = label;
                Amount = amount;
            }
        }

        public string RunName;
        public string LevelName;
        /// <summary>1-based number of the level just cleared.</summary>
        public int LevelNumber;
        public int LevelCount;
        public bool RunCompleted;
        /// <summary>The level the run continues with, or null when the run is over.</summary>
        public string NextLevelName;

        /// <summary>Gold picked up on the floor (kills, caches, events) - held until now, banked by the clear.</summary>
        public int GoldFound;
        /// <summary>The flat bonus every level clear pays on top.</summary>
        public int GoldBonus;
        public int Essence;

        /// <summary>XP each hero earned on this floor, by display name.</summary>
        public List<Line> Xp = new List<Line>();
        /// <summary>Raw materials gained, by display name.</summary>
        public List<Line> Materials = new List<Line>();
        /// <summary>Equipment and consumables gained, by display name.</summary>
        public List<Line> Items = new List<Line>();
        /// <summary>Heroes who joined on this floor - freed, met, or earned by the run clear - theirs for good now.</summary>
        public List<string> Joined = new List<string>();

        public int GoldTotal => GoldFound + GoldBonus;

        /// <summary>
        /// What the floor added to the bags, per item key: the committed inventory from before the
        /// level against the one being committed, <b>plus what the level spent</b>. The spend has to
        /// be added back or a potion found and then drunk would not appear at all, and a drunk potion
        /// with none found would read as a loss. Only gains are returned, in first-seen order.
        /// </summary>
        public static List<KeyValuePair<string, int>> ItemGains(
            IEnumerable<ItemSaveData> before,
            IEnumerable<ItemSaveData> after,
            IEnumerable<ConsumableSpend> spent)
        {
            var delta = new Dictionary<string, int>();
            var order = new List<string>();

            void Add(string key, int amount)
            {
                if (string.IsNullOrEmpty(key) || amount == 0)
                {
                    return;
                }
                if (!delta.ContainsKey(key))
                {
                    delta[key] = 0;
                    order.Add(key);
                }
                delta[key] += amount;
            }

            if (after != null)
            {
                foreach (var item in after)
                {
                    if (item != null)
                    {
                        Add(item.ItemKey, Quantity(item));
                    }
                }
            }
            if (before != null)
            {
                foreach (var item in before)
                {
                    if (item != null)
                    {
                        Add(item.ItemKey, -Quantity(item));
                    }
                }
            }
            if (spent != null)
            {
                foreach (var spend in spent)
                {
                    if (spend != null)
                    {
                        Add(spend.ItemKey, spend.Count);
                    }
                }
            }

            var gains = new List<KeyValuePair<string, int>>();
            foreach (var key in order)
            {
                if (delta[key] > 0)
                {
                    gains.Add(new KeyValuePair<string, int>(key, delta[key]));
                }
            }
            return gains;
        }

        private static int Quantity(ItemSaveData item)
        {
            // Old saves predate Quantity and read 0; the inventory normalises that to 1.
            return item.Quantity > 0 ? item.Quantity : 1;
        }
    }
}
