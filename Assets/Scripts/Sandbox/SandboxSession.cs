using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.IO;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Sandbox
{
    /// <summary>
    /// One sandbox run: points every save at a throwaway folder, writes the configured party into
    /// it, hands the dungeon its party/floor/seed, and — once the floor exists — stocks the
    /// encounter room next to the start. Started before the first scene loads (see
    /// <c>SandboxBootstrap</c>), so every manager's <c>Awake</c> already reads the sandbox folder.
    ///
    /// <para><b>Isolation is the folder, not discipline.</b> Nothing below has to remember not to
    /// save: bestiary writes, dungeon saves, a level clear's commit — all of them land in
    /// <see cref="Directory"/>, which is wiped at the next launch. The player's
    /// <c>savedata</c> folder is only ever <i>read</i> (to copy audio settings, and inventory or meta
    /// progress when asked).</para>
    /// </summary>
    public static class SandboxSession
    {
        public const int MaxEnemies = 5;

        public static bool IsActive { get; private set; }
        public static SandboxConfigSO Config { get; private set; }

        /// <summary>The sandbox save folder: <c>persistentDataPath/savedata_sandbox</c>.</summary>
        public static string Directory => $"{Application.persistentDataPath}/savedata_sandbox";

        private const string LogPrefix = "[Sandbox] ";

        public static void Begin(SandboxConfigSO config)
        {
            if (config == null)
            {
                Debug.LogError(LogPrefix + "No config; starting normally.");
                return;
            }

            Config = config;
            IsActive = true;

            PrepareFolder(config);
            FileHandler.DirectoryOverride = Directory;

            var problems = new List<string>();
            var handler = new FileHandler(Directory);
            var party = SandboxSetup.BuildPartySave(config.Heroes, problems);
            handler.Save(party);
            handler.Save(SandboxSetup.BuildLoadout(config.Heroes));

            foreach (var setup in config.Heroes.Where(h => h != null && h.Hero != null))
            {
                var entry = party.Heroes.FirstOrDefault(h => h.HeroKey == setup.Hero.SaveKey);
                foreach (var key in SandboxSetup.UnlearnedAbilities(setup, entry))
                {
                    problems.Add($"{setup.Hero.DisplayName} is set to carry '{key}' but the grid setup does not teach it; it will be dropped.");
                }
            }

            var heroes = config.Heroes.Where(h => h != null && h.Hero != null).Select(h => h.Hero).Distinct().ToList();
            if (heroes.Count == 0)
            {
                problems.Add("No heroes configured; the scene's default party will be used.");
            }
            if (config.Enemies.Count(e => e != null) > MaxEnemies)
            {
                problems.Add($"{config.Enemies.Count(e => e != null)} enemies configured; only the first {MaxEnemies} are placed (the stage is built for five).");
            }

            DungeonManager.PartyOverride = heroes.Count > 0 ? heroes : null;
            DungeonManager.LevelToLoad = config.Level;
            DungeonManager.SeedOverride = config.Seed != 0 ? config.Seed : (int?)null;
            DungeonManager.ActiveRun = null;
            DungeonManager.SeedToLoad = null;
            DungeonManager.RunLevelIndex = 0;
            DungeonManager.FreshDungeonSpawned -= OnFreshDungeonSpawned;
            DungeonManager.FreshDungeonSpawned += OnFreshDungeonSpawned;

            Debug.Log(LogPrefix + $"Started '{config.name}'. Saves go to {Directory}.");
            foreach (var problem in problems)
            {
                Debug.LogWarning(LogPrefix + problem);
            }
        }

        /// <summary>Hands the save folder back to the player and forgets every hand-off.</summary>
        public static void End()
        {
            if (!IsActive)
            {
                return;
            }
            DungeonManager.FreshDungeonSpawned -= OnFreshDungeonSpawned;
            DungeonManager.PartyOverride = null;
            DungeonManager.SeedOverride = null;
            FileHandler.DirectoryOverride = null;
            IsActive = false;
            Config = null;
        }

        private static void PrepareFolder(SandboxConfigSO config)
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, true);
            }
            System.IO.Directory.CreateDirectory(Directory);

            // Audio always, so the sandbox plays at the volume the player set.
            CopyFromRealSave("Audio");
            if (config.CopyInventory)
            {
                CopyFromRealSave("ItemCollection");
                CopyFromRealSave("ResourceMaximums");
            }
            if (config.CopyMetaProgress)
            {
                CopyFromRealSave("Meta");
            }
        }

        private static void CopyFromRealSave(string fileName)
        {
            var source = $"{FileHandler.DefaultDirectory}/{fileName}.json";
            if (File.Exists(source))
            {
                File.Copy(source, $"{Directory}/{fileName}.json", true);
            }
        }

        // ------------------------------------------------------------------
        //  Once the floor exists
        // ------------------------------------------------------------------

        private static void OnFreshDungeonSpawned(List<Room> rooms, Room startRoom)
        {
            var config = Config;
            if (config == null || !EnemyManager.HasInstance)
            {
                return;
            }

            var enemyManager = EnemyManager.Instance;
            enemyManager.SetLevelTuning(Mathf.Approximately(config.EnemyDifficulty, 1f)
                ? null
                : new LevelEnemyTuning { Difficulty = config.EnemyDifficulty });

            var encounterRoom = PickEncounterRoom(rooms, startRoom);
            if (config.ClearOtherRooms)
            {
                foreach (var room in rooms)
                {
                    enemyManager.ClearRoomEnemies(room);
                }
            }
            else
            {
                enemyManager.ClearRoomEnemies(encounterRoom);
            }

            var enemies = config.Enemies.Where(e => e != null).Take(MaxEnemies).ToList();
            if (encounterRoom != null)
            {
                foreach (var enemy in enemies)
                {
                    enemyManager.SpawnSingle(enemy, encounterRoom);
                }
            }
            else if (enemies.Count > 0)
            {
                Debug.LogWarning(LogPrefix + "The floor has no room besides the start; no encounter placed.");
            }

            GrantItems(config);

            Debug.Log(LogPrefix + BuildReport(encounterRoom));

            if (config.WalkIntoEncounter && encounterRoom != null && enemies.Count > 0)
            {
                SandboxRunner.Run(WalkIn(startRoom, encounterRoom));
            }
        }

        /// <summary>
        /// A combat room one door from the start; failing that any neighbour, then the nearest room
        /// at all. The exit room is avoided while there is a choice, so a fight never doubles as the
        /// staircase.
        /// </summary>
        private static Room PickEncounterRoom(List<Room> rooms, Room startRoom)
        {
            var neighbours = startRoom.Doors
                .Select(d => d.GetOtherRoom(startRoom))
                .Where(r => r != null && r != startRoom)
                .ToList();

            var pick = neighbours.FirstOrDefault(r => r.Kind.HoldsEnemies() && !r.IsExit)
                       ?? neighbours.FirstOrDefault(r => !r.IsExit)
                       ?? neighbours.FirstOrDefault();
            if (pick != null)
            {
                pick.Kind = RoomKind.Combat;
                return pick;
            }
            return rooms.FirstOrDefault(r => r != startRoom);
        }

        private static void GrantItems(SandboxConfigSO config)
        {
            if (!InventoryManager.HasInstance || config.Items == null)
            {
                return;
            }
            var inventory = InventoryManager.Instance;
            foreach (var grant in config.Items.Where(g => g != null && g.Item != null))
            {
                inventory.AddItem(grant.Item, grant.Count);
                if (grant.EquipOn == null || grant.Item.Category != ItemCategory.Equipment)
                {
                    continue;
                }
                var unequipped = inventory.GetItems()
                    .LastOrDefault(i => i.ItemKey == grant.Item.Key && string.IsNullOrEmpty(i.EquippedHeroKey));
                if (unequipped != null)
                {
                    inventory.Equip(unequipped, grant.Item.SlotType, grant.EquipOn.SaveKey);
                }
            }
        }

        private static IEnumerator WalkIn(Room startRoom, Room encounterRoom)
        {
            // One frame, so GameManager.EnterRoom(start) has run and the room bar is listening.
            yield return null;
            var door = startRoom.Doors.FirstOrDefault(d => d.GetOtherRoom(startRoom) == encounterRoom);
            if (door != null)
            {
                door.SendMessage("OnMouseDown");
            }
        }

        // ------------------------------------------------------------------
        //  The report: what the party actually has, read off the live objects
        // ------------------------------------------------------------------

        private static string BuildReport(Room encounterRoom)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Setup report");

            var dungeon = DungeonManager.HasInstance ? DungeonManager.Instance : null;
            var party = dungeon != null ? dungeon.Party : null;
            if (party != null)
            {
                foreach (var hero in party.Heroes.Where(h => h != null))
                {
                    sb.AppendLine($"  {hero.DisplayName} — {hero.ActivatedNodes.Count} nodes, {hero.CurrentXp} XP banked");
                    sb.AppendLine("    " + string.Join("  ", StatCatalog.Types.Select(t =>
                        $"{StatCatalog.ShortName(t)} {hero.GetEffectiveStat(t)}")));
                    var resists = hero.GetEffectiveResistances().Where(r => r != null && !Mathf.Approximately(r.Percent, 0f)).ToList();
                    if (resists.Count > 0)
                    {
                        sb.AppendLine("    resist " + string.Join(", ", resists.Select(r => $"{r.DamageType} {r.Percent:+0;-0}%")));
                    }
                    var known = hero.KnownMagic.Select(p => $"{p.Key}×{p.Value}").ToList();
                    sb.AppendLine("    knows  " + (known.Count > 0 ? string.Join(", ", known) : "(nothing)"));
                    var slots = dungeon.MagicState != null ? dungeon.MagicState.GetSlots(hero.HeroKey) : null;
                    if (slots != null)
                    {
                        sb.AppendLine("    slots  " + string.Join(", ", slots.Select(s =>
                            s.Magic != null ? $"{s.Magic.Key} {s.Charges}/{s.MaxCharges}" : "(empty)")));
                    }
                    var summons = dungeon.Summons != null ? dungeon.Summons.GetSummons(hero.HeroKey) : null;
                    if (summons != null && summons.Count > 0)
                    {
                        sb.AppendLine("    summon " + string.Join(", ", summons.Select(s =>
                            $"{s.Summon.Label} {s.Charges}/{s.MaxCharges} ({Cards.SummonOps.Describe(s.Summon, s.Grant)})")));
                    }
                    var gear = InventoryManager.HasInstance ? InventoryManager.Instance.GetEquippedItems(hero.HeroKey) : null;
                    if (gear != null && gear.Count > 0)
                    {
                        sb.AppendLine("    gear   " + string.Join(", ", gear.Select(g => g.DisplayName)));
                    }
                }
            }

            if (encounterRoom != null)
            {
                sb.AppendLine($"  Encounter (room {encounterRoom.RoomIndex}):");
                foreach (var enemy in encounterRoom.Enemies.Where(e => e != null))
                {
                    var stats = string.Join("  ", StatCatalog.Types.Select(t => $"{StatCatalog.ShortName(t)} {enemy.GetEffectiveStat(t)}"));
                    var resists = enemy.Resistances != null && enemy.Resistances.Count > 0
                        ? " | resist " + string.Join(", ", enemy.Resistances.Select(r => $"{r.DamageType} {r.Percent:+0;-0}%"))
                        : "";
                    sb.AppendLine($"    {enemy.DisplayName}{(enemy.IsBoss ? " (boss)" : "")}: {stats}{resists}");
                }
            }
            return sb.ToString();
        }
    }
}
