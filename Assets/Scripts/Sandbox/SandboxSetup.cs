using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Sandbox
{
    /// <summary>
    /// The pure half of the sandbox: turns a <see cref="SandboxConfigSO"/> into the save files the
    /// game already knows how to read. No scene, no disk — <see cref="SandboxSession"/> writes what
    /// this returns. Covered by <c>SandboxSetupTests</c>.
    ///
    /// <para>Going through the save files rather than poking live objects is the point: the dungeon
    /// then boots exactly the way it does after the hub, so what the sandbox shows is what a player
    /// with that save would get.</para>
    /// </summary>
    public static class SandboxSetup
    {
        /// <summary>
        /// A hero's save entry: the grid walked per the setup, then the bank. Order of operations is
        /// whole grid → forced paths → greedy spend, so a spend budget buys what the free unlocks
        /// have not already given. Problems (unknown node keys, a hero with no grid) are appended to
        /// <paramref name="problems"/> rather than thrown — a typo should not stop a test session.
        /// </summary>
        public static HeroSaveData BuildHeroSave(SandboxHeroSetup setup, List<string> problems)
        {
            var entry = new HeroSaveData();
            if (setup == null || setup.Hero == null)
            {
                return entry;
            }

            entry.HeroKey = setup.Hero.SaveKey;
            entry.CurrentXp = setup.BankedXp;

            var grid = setup.Hero.SphereGrid;
            if (grid == null)
            {
                if (setup.UnlockEntireGrid || setup.SpendXpOnGrid > 0 || HasAny(setup.UnlockPathTo))
                {
                    problems?.Add($"{setup.Hero.DisplayName} has no sphere grid; grid options ignored.");
                }
                return entry;
            }

            var activated = new List<string>();
            if (setup.UnlockEntireGrid)
            {
                // Depth order, so the list reads as a walk from the start like a real save's does.
                foreach (var pair in SphereGridOps.DepthsFrom(grid).OrderBy(p => p.Value))
                {
                    AddOnce(activated, pair.Key);
                }
            }

            if (setup.UnlockPathTo != null)
            {
                foreach (var target in setup.UnlockPathTo)
                {
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }
                    var path = SphereGridOps.PathTo(grid, target);
                    if (path.Count == 0)
                    {
                        problems?.Add($"{setup.Hero.DisplayName}: no node '{target}' reachable on {grid.name}.");
                        continue;
                    }
                    foreach (var key in path)
                    {
                        AddOnce(activated, key);
                    }
                }
            }

            if (setup.SpendXpOnGrid > 0)
            {
                activated = SphereGridOps.GreedySpend(grid, activated, setup.SpendXpOnGrid, out _);
            }

            entry.ActivatedNodes = activated;
            return entry;
        }

        /// <summary>Party.json for the configured heroes: owned, selected in order, and walked.</summary>
        public static PartySaveData BuildPartySave(IEnumerable<SandboxHeroSetup> heroes, List<string> problems)
        {
            var save = new PartySaveData();
            foreach (var setup in Valid(heroes))
            {
                var key = setup.Hero.SaveKey;
                if (save.OwnedHeroKeys.Contains(key))
                {
                    problems?.Add($"{setup.Hero.DisplayName} is listed twice; the first entry wins.");
                    continue;
                }
                save.Heroes.Add(BuildHeroSave(setup, problems));
                save.OwnedHeroKeys.Add(key);
                save.SelectedHeroKeys.Add(key);
            }
            return save;
        }

        /// <summary>
        /// MagicLoadout.json: an entry only for heroes whose abilities were chosen, so everyone else
        /// auto-fills from their grid exactly as an untouched save would.
        /// </summary>
        public static MagicLoadoutSaveData BuildLoadout(IEnumerable<SandboxHeroSetup> heroes)
        {
            var loadout = new MagicLoadoutSaveData();
            foreach (var setup in Valid(heroes))
            {
                var keys = setup.Abilities == null
                    ? new List<string>()
                    : setup.Abilities.Where(m => m != null && !string.IsNullOrEmpty(m.Key)).Select(m => m.Key).ToList();
                if (keys.Count > 0)
                {
                    loadout.For(setup.Hero.SaveKey).EquippedKeys = keys;
                }
            }
            return loadout;
        }

        /// <summary>
        /// Abilities chosen that the hero's grid will not teach with this setup — they would be
        /// silently dropped when the loadout resolves, so they are worth saying out loud.
        /// </summary>
        public static List<string> UnlearnedAbilities(SandboxHeroSetup setup, HeroSaveData entry)
        {
            var missing = new List<string>();
            if (setup == null || setup.Hero == null || setup.Abilities == null)
            {
                return missing;
            }
            var known = SphereGridOps.KnownMagicForNodes(setup.Hero.SphereGrid, entry != null ? entry.ActivatedNodes : null)
                .Select(p => p.Key)
                .ToList();
            foreach (var magic in setup.Abilities)
            {
                if (magic != null && !known.Contains(magic.Key))
                {
                    missing.Add(magic.Key);
                }
            }
            return missing;
        }

        private static IEnumerable<SandboxHeroSetup> Valid(IEnumerable<SandboxHeroSetup> heroes)
        {
            return heroes == null
                ? Enumerable.Empty<SandboxHeroSetup>()
                : heroes.Where(h => h != null && h.Hero != null);
        }

        private static bool HasAny(List<string> keys)
        {
            return keys != null && keys.Any(k => !string.IsNullOrEmpty(k));
        }

        private static void AddOnce(List<string> list, string key)
        {
            if (!string.IsNullOrEmpty(key) && !list.Contains(key))
            {
                list.Add(key);
            }
        }
    }
}
