using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;

namespace Assets.Scripts.Events
{
    // The events GameEvents carries. Plain classes: a subscriber names the one type it wants. Each
    // says where it is raised, which is the single chokepoint for that kind of change - if a new code
    // path changes the same state without going through it, it has skipped the event too.
    //
    // Keys, not objects, wherever the thing outlives the scene (a hero, a run, an enemy kind), so an
    // achievement or a quest can store what it saw.

    /// <summary>Where a gain or a loss came from. A listener that cares only about one route (a quest
    /// for gold found in caches) filters on it; one that counts everything ignores it.</summary>
    public enum EconomySource
    {
        Unknown = 0,
        /// <summary>An enemy's death: its XP, kill-gold and drops.</summary>
        Kill,
        /// <summary>A treasure room's cache.</summary>
        Cache,
        /// <summary>A room event's outcome.</summary>
        RoomEvent,
        /// <summary>Taking the stairs: the level's bonus, its banked kill-gold, its guaranteed materials.</summary>
        LevelClear,
        /// <summary>The consolation a wipe pays.</summary>
        RunDeath,
        /// <summary>Gold or items lost because the level was not cleared (a wipe or a quit).</summary>
        Forfeit,
        /// <summary>Buying, selling, restocking or enlarging the potion belt at the Merchant.</summary>
        Merchant,
        /// <summary>Placing or upgrading a hub building.</summary>
        Building,
        /// <summary>Buying a sphere-grid node.</summary>
        SphereGrid,
        /// <summary>Upgrading a combo at the Forge.</summary>
        Forge,
        /// <summary>A spend handed back because a later step of the same purchase failed.</summary>
        Refund,
        /// <summary>The potion belt topped up at the start of a run's level.</summary>
        BeltRefill,
        /// <summary>A sandbox session's setup.</summary>
        Sandbox,
        /// <summary>A save written by an older version being brought up to date.</summary>
        Migration
    }

    public enum Currency
    {
        Gold,
        Essence
    }

    /// <summary>Items entered the inventory. Raised by <c>InventoryManager.AddItem</c> (and the belt
    /// top-up), live: a level's finds are provisional until <see cref="LevelCleared"/>.</summary>
    public class ItemAcquired
    {
        public ItemSO Item;
        public int Quantity;
        public EconomySource Source;
    }

    public enum ItemRemovalReason
    {
        /// <summary>A consumable drunk or applied.</summary>
        Used,
        /// <summary>A material paid as a price.</summary>
        Spent,
        /// <summary>Sold at the Merchant.</summary>
        Sold,
        /// <summary>Taken by a room event.</summary>
        Lost
    }

    /// <summary>Items left the inventory. Raised by <c>InventoryManager</c>'s spending paths.</summary>
    public class ItemRemoved
    {
        public ItemSO Item;
        public int Quantity;
        public ItemRemovalReason Reason;
    }

    /// <summary>A hero put on (<see cref="Equipped"/> true) or took off a piece of gear.</summary>
    public class ItemEquipChanged
    {
        public ItemSO Item;
        public string HeroKey;
        public bool Equipped;
    }

    /// <summary>
    /// Gold or Essence moved. <see cref="Pending"/> is kill- and cache-gold found on a level: real
    /// but not yet banked, and forfeited (a matching negative pending change) if the level is not
    /// cleared. Banking it on the stairs is a <b>non-pending</b> change of the whole amount, so a
    /// listener that counts gold earned counts only non-pending gains.
    /// </summary>
    public class CurrencyChanged
    {
        public Currency Currency;
        public int Delta;
        public EconomySource Source;
        public bool Pending;
    }

    /// <summary>One hero was paid XP (live; committed on <see cref="LevelCleared"/>, lost otherwise).
    /// Raised by <c>Party.DistributeXp</c>, once per hero.</summary>
    public class XpAwarded
    {
        public string HeroKey;
        public int Amount;
        public EconomySource Source;
    }

    /// <summary>An enemy fell in combat. Raised by <c>CombatManager</c> as it pays the kill out.</summary>
    public class EnemyDefeated
    {
        public EnemySO Enemy;
        public string EnemyKey;
        public bool IsBoss;

        /// <summary>The hero whose blow (or whose poison) felled it; null when it was a summon, an
        /// enemy, or nobody.</summary>
        public string KillerHeroKey;
    }

    /// <summary>A fight ended. Raised by <c>CombatManager</c> with the result.</summary>
    public class CombatFinished
    {
        public bool Won;
        public bool HadBoss;

        /// <summary>The heroes who were fielded, standing or not.</summary>
        public List<string> HeroKeys = new List<string>();
    }

    /// <summary>The party stepped into a room. Raised by <c>GameManager.EnterRoom</c>.</summary>
    public class RoomEntered
    {
        public Rooms.Room Room;
    }

    /// <summary>A level of a run was built and the party is about to walk in. <see cref="Fresh"/> is
    /// false for a level resumed from a save.</summary>
    public class LevelStarted
    {
        public string RunKey;
        public int LevelIndex;
        public bool Fresh;
    }

    /// <summary>The party took the stairs: everything the level produced is now kept.</summary>
    public class LevelCleared
    {
        public string RunKey;
        public int LevelIndex;
        public bool RunCompleted;
        public bool Revisit;
    }

    public enum ForfeitReason
    {
        PartyDied,
        LeftTheDungeon
    }

    /// <summary>The level ended without being cleared: its items, kill-gold and XP are gone.</summary>
    public class LevelForfeited
    {
        public string RunKey;
        public int LevelIndex;
        public ForfeitReason Reason;
    }

    /// <summary>A hero joined the roster (a rescue, a room event, a run's reward). Ownership is
    /// committed with the level, like XP.</summary>
    public class HeroJoined
    {
        public string HeroKey;
    }

    /// <summary>A sphere-grid node was bought at the hub. Raised by <c>HeroRoster.TryActivateNode</c>.</summary>
    public class NodeActivated
    {
        public string HeroKey;
        public string NodeKey;
    }

    /// <summary>A hub building was placed or raised to a new level.</summary>
    public class BuildingLevelChanged
    {
        public string BuildingKey;
        public int Level;
    }
}
