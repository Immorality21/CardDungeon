using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// Which heroes the player actually owns, as opposed to which ones exist.
    /// <see cref="PartyRosterSO"/> is the authored *catalog*; this is the *owned* subset, persisted
    /// as save keys in <c>Party.json</c> alongside each hero's XP.
    ///
    /// A run starts with <see cref="PartyRosterSO.StartingHeroes"/> only; the rest are acquired by
    /// rescuing a captive mid-dungeon (<c>RunLevelEntry.RescueHero</c>) or recruiting at the hub
    /// tavern. Legacy saves written before ownership existed are migrated on first read: whoever
    /// already had a <see cref="HeroSaveData"/> entry is treated as owned, so nobody loses a hero.
    ///
    /// <para>Owning is not the same as <em>fielding</em>. The party the player takes into a dungeon is
    /// a chosen subset of the owned roster (<see cref="PartySaveData.SelectedHeroKeys"/>), capped by
    /// <see cref="PartySlots"/> - because with even-split XP a wider party is a trade rather than an
    /// upgrade, and a trade needs a way to decline it. The cap is passed in rather than read here, so
    /// this stays testable and free of the meta-progress singleton.</para>
    /// </summary>
    public static class HeroRoster
    {
        /// <summary>
        /// Save keys the player owns, in acquisition order. Never empty for a valid roster — a save
        /// with no owned heroes falls back to the catalog's starting heroes.
        /// </summary>
        public static List<string> GetOwnedKeys(PartyRosterSO catalog)
        {
            var handler = new FileHandler();
            var save = handler.Load<PartySaveData>();
            var keys = Resolve(save, catalog);

            // Persist a migrated/seeded list so later reads are stable and the hub and the dungeon
            // agree on the roster even before a level is cleared.
            if (!SameOrder(save.OwnedHeroKeys, keys))
            {
                save.OwnedHeroKeys = new List<string>(keys);
                handler.Save(save);
            }

            return keys;
        }

        /// <summary>The owned heroes as definitions, resolved against the catalog and de-duplicated.</summary>
        public static List<HeroSO> GetOwnedHeroes(PartyRosterSO catalog)
        {
            var heroes = new List<HeroSO>();
            if (catalog == null)
            {
                return heroes;
            }

            foreach (var key in GetOwnedKeys(catalog))
            {
                var hero = catalog.Find(key);
                if (hero != null && !heroes.Contains(hero))
                {
                    heroes.Add(hero);
                }
            }
            return heroes;
        }

        public static bool Owns(PartyRosterSO catalog, HeroSO hero)
        {
            if (hero == null)
            {
                return false;
            }
            return GetOwnedKeys(catalog).Contains(hero.SaveKey);
        }

        /// <summary>
        /// Records <paramref name="hero"/> as owned and writes it to disk immediately. Used by the
        /// tavern, where a purchase must survive whatever happens next. In-dungeon rescues go
        /// through <c>DungeonManager</c> instead, so they follow the run's deferred-commit rule and
        /// are forfeited on death.
        /// </summary>
        public static bool TryAddOwned(PartyRosterSO catalog, HeroSO hero)
        {
            if (hero == null || string.IsNullOrEmpty(hero.SaveKey))
            {
                return false;
            }

            var handler = new FileHandler();
            var save = handler.Load<PartySaveData>();
            var keys = Resolve(save, catalog);
            if (keys.Contains(hero.SaveKey))
            {
                return false;
            }

            keys.Add(hero.SaveKey);
            save.OwnedHeroKeys = keys;
            handler.Save(save);
            return true;
        }

        /// <summary>
        /// Un-records ownership. Only for rolling back a recruitment whose payment failed - there is
        /// no in-game way to lose a hero you have paid for.
        /// </summary>
        public static void RemoveOwned(PartyRosterSO catalog, HeroSO hero)
        {
            if (hero == null)
            {
                return;
            }

            var handler = new FileHandler();
            var save = handler.Load<PartySaveData>();
            if (save.OwnedHeroKeys == null || !save.OwnedHeroKeys.Remove(hero.SaveKey))
            {
                return;
            }
            // Un-field them too: a selection entry for a hero you do not own would be filtered out on
            // the next read anyway, but leaving it there means the party silently shrinks by one.
            save.SelectedHeroKeys?.Remove(hero.SaveKey);
            handler.Save(save);
        }

        /// <summary>Catalog heroes the player does not own yet — the tavern's recruitment pool.</summary>
        public static List<HeroSO> GetRecruitable(PartyRosterSO catalog)
        {
            var result = new List<HeroSO>();
            if (catalog == null)
            {
                return result;
            }

            var owned = GetOwnedKeys(catalog);
            foreach (var hero in catalog.Heroes)
            {
                if (hero != null && !owned.Contains(hero.SaveKey) && !result.Contains(hero))
                {
                    result.Add(hero);
                }
            }
            return result;
        }

        // --- Selection: who actually enters the dungeon ------------------------

        /// <summary>
        /// Save keys of the fielded party, leader first. Persists a resolved list when the stored one
        /// was empty, stale or over the cap, so the hub and the dungeon cannot disagree about who is
        /// coming.
        /// </summary>
        public static List<string> GetSelectedKeys(PartyRosterSO catalog, int cap)
        {
            var handler = new FileHandler();
            var save = handler.Load<PartySaveData>();
            var owned = Resolve(save, catalog);
            var keys = ResolveSelection(save, owned, cap);

            if (!SameOrder(save.SelectedHeroKeys, keys))
            {
                save.SelectedHeroKeys = new List<string>(keys);
                save.OwnedHeroKeys = new List<string>(owned);
                handler.Save(save);
            }

            return keys;
        }

        /// <summary>The fielded party as definitions, resolved against the catalog and de-duplicated.</summary>
        public static List<HeroSO> GetSelectedHeroes(PartyRosterSO catalog, int cap)
        {
            var heroes = new List<HeroSO>();
            if (catalog == null)
            {
                return heroes;
            }

            foreach (var key in GetSelectedKeys(catalog, cap))
            {
                var hero = catalog.Find(key);
                if (hero != null && !heroes.Contains(hero))
                {
                    heroes.Add(hero);
                }
            }
            return heroes;
        }

        /// <summary>
        /// Replaces the fielded party. Silently drops keys the player does not own and anything past
        /// the cap; refuses an empty selection, since a party of nobody cannot enter a dungeon.
        /// Returns the list as it was actually stored.
        /// </summary>
        public static List<string> SetSelectedKeys(PartyRosterSO catalog, IEnumerable<string> keys, int cap)
        {
            var handler = new FileHandler();
            var save = handler.Load<PartySaveData>();
            var owned = Resolve(save, catalog);

            var wanted = new List<string>();
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    if (!string.IsNullOrEmpty(key) && owned.Contains(key) && !wanted.Contains(key))
                    {
                        wanted.Add(key);
                    }
                }
            }

            var stored = Clamp(wanted, cap);
            if (stored.Count == 0)
            {
                // Nothing usable was asked for - fall back rather than write a party of nobody.
                stored = ResolveSelection(save, owned, cap);
            }

            save.OwnedHeroKeys = new List<string>(owned);
            save.SelectedHeroKeys = new List<string>(stored);
            handler.Save(save);
            return stored;
        }

        /// <summary>
        /// Fields <paramref name="hero"/> if the cap has room, and writes it immediately. Called
        /// wherever a hero becomes owned in the hub: recruiting someone and then finding they were
        /// left at home is a worse surprise than a full party quietly benching them. Returns true if
        /// they were added. Mid-dungeon rescues go through <c>Party.MarkOwnedDeferred</c> instead, so
        /// they follow the run's deferred-commit rule.
        /// </summary>
        public static bool TryFieldIfRoom(PartyRosterSO catalog, HeroSO hero, int cap)
        {
            if (hero == null || string.IsNullOrEmpty(hero.SaveKey))
            {
                return false;
            }

            var handler = new FileHandler();
            var save = handler.Load<PartySaveData>();
            var owned = Resolve(save, catalog);
            var selected = ResolveSelection(save, owned, cap);

            if (selected.Contains(hero.SaveKey) || selected.Count >= EffectiveCap(cap))
            {
                return false;
            }

            selected.Add(hero.SaveKey);
            save.OwnedHeroKeys = new List<string>(owned);
            save.SelectedHeroKeys = selected;
            handler.Save(save);
            return true;
        }

        /// <summary>
        /// The fielded party for a save, without touching disk: the stored selection filtered to what
        /// is owned and clamped to the cap, falling back to the owned roster (also clamped) when that
        /// leaves nobody. The fallback is what migrates a save written before selection existed and
        /// what handles a cap that has since shrunk.
        /// </summary>
        public static List<string> ResolveSelection(PartySaveData save, List<string> owned, int cap)
        {
            var keys = new List<string>();
            if (owned == null || owned.Count == 0)
            {
                return keys;
            }

            if (save != null && save.SelectedHeroKeys != null)
            {
                foreach (var key in save.SelectedHeroKeys)
                {
                    if (!string.IsNullOrEmpty(key) && owned.Contains(key) && !keys.Contains(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            if (keys.Count == 0)
            {
                keys.AddRange(owned);
            }

            return Clamp(keys, cap);
        }

        /// <summary>The cap as a usable party size: at least one hero, never more than the ceiling.</summary>
        private static int EffectiveCap(int cap)
        {
            if (cap < 1)
            {
                return 1;
            }
            if (cap > PartySlots.MaxCap)
            {
                return PartySlots.MaxCap;
            }
            return cap;
        }

        private static List<string> Clamp(List<string> keys, int cap)
        {
            int limit = EffectiveCap(cap);
            if (keys.Count <= limit)
            {
                return keys;
            }
            return keys.GetRange(0, limit);
        }

        /// <summary>
        /// The owned-key list for a save, applying migration and the starting-hero fallback without
        /// writing anything. Kept separate so callers that already hold a save can reuse it.
        /// </summary>
        private static List<string> Resolve(PartySaveData save, PartyRosterSO catalog)
        {
            var keys = new List<string>();
            if (save == null)
            {
                return StartingKeys(catalog, keys);
            }

            if (save.OwnedHeroKeys != null)
            {
                foreach (var key in save.OwnedHeroKeys)
                {
                    if (!string.IsNullOrEmpty(key) && !keys.Contains(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            if (keys.Count > 0)
            {
                return keys;
            }

            // Migration: a save written before ownership existed lists a HeroSaveData per hero that
            // was in the party, so that list *is* the owned roster.
            foreach (var hero in save.Heroes)
            {
                if (hero != null && !string.IsNullOrEmpty(hero.HeroKey) && !keys.Contains(hero.HeroKey))
                {
                    keys.Add(hero.HeroKey);
                }
            }

            return keys.Count > 0 ? keys : StartingKeys(catalog, keys);
        }

        private static List<string> StartingKeys(PartyRosterSO catalog, List<string> into)
        {
            if (catalog == null)
            {
                return into;
            }
            foreach (var hero in catalog.StartingLineup())
            {
                if (hero != null && !into.Contains(hero.SaveKey))
                {
                    into.Add(hero.SaveKey);
                }
            }
            return into;
        }

        private static bool SameOrder(List<string> a, List<string> b)
        {
            if (a == null || b == null)
            {
                return false;
            }
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
