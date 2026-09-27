using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Sandbox
{
    /// <summary>
    /// A disposable test setup: which heroes, how far along their sphere grids, what they carry,
    /// which floor, and which enemies wait one door from the start. Launched from
    /// <c>Tools ▸ Sandbox</c> (or <c>SandboxLauncher.Launch</c>), it runs the real
    /// <c>MainGameScene</c> against a throwaway save folder, so nothing here can reach the player's
    /// save. See <c>docs/SANDBOX.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Sandbox", menuName = "SO/Sandbox Config")]
    public class SandboxConfigSO : ScriptableObject
    {
        [Header("Party")]
        [Tooltip("Heroes in order, leader first. Any HeroSO works, including one not in the roster yet.")]
        public List<SandboxHeroSetup> Heroes = new List<SandboxHeroSetup>();

        [Header("Dungeon")]
        [Tooltip("The floor to generate. Empty uses the scene's test level (DungeonManager._testLevel).")]
        public LevelDefinitionSO Level;

        [Tooltip("0 = a new random layout every launch. Anything else reproduces the same floor.")]
        public int Seed;

        [Header("Encounter")]
        [Tooltip("Up to five enemies, placed in a room next to the start. An enemy with IsBoss takes " +
                 "the boss slot and turns on the boss banner, bar and no-flee rule.")]
        public List<EnemySO> Enemies = new List<EnemySO>();

        [Tooltip("The level's Difficulty dial for these enemies (1 = the template exactly). Scales MaxHealth and Strength.")]
        [Min(0.1f)] public float EnemyDifficulty = 1f;

        [Tooltip("Remove every other enemy from the floor, so the encounter is the only fight.")]
        public bool ClearOtherRooms = true;

        [Tooltip("Walk the party into the encounter room on start, so the Fight bar is waiting.")]
        public bool WalkIntoEncounter = true;

        [Header("Starting save")]
        [Tooltip("Start from a copy of your real inventory (gear, potions, materials) instead of an empty one.")]
        public bool CopyInventory;

        [Tooltip("Start from a copy of your real meta progress (Forge upgrades, bestiary, gold) instead of a fresh one.")]
        public bool CopyMetaProgress;

        [Tooltip("Extra items to give the party. Equipment with a hero set is equipped on them.")]
        public List<SandboxItemGrant> Items = new List<SandboxItemGrant>();
    }

    [Serializable]
    public class SandboxHeroSetup
    {
        public HeroSO Hero;

        [Tooltip("Unspent XP left in the bank (spendable later at the hub's sphere grid).")]
        [Min(0)] public int BankedXp;

        [Tooltip("Spend this much XP on the grid the way the balance model does: cheapest reachable node first.")]
        [Min(0)] public int SpendXpOnGrid;

        [Tooltip("Node keys to reach. Every node on the shortest path from the start is unlocked, free.")]
        public List<string> UnlockPathTo = new List<string>();

        [Tooltip("Unlock every reachable node, free.")]
        public bool UnlockEntireGrid;

        [Tooltip("Magic keys to carry, in slot order. Empty auto-fills from what the grid teaches, " +
                 "exactly as a hero who never opened the Spells tab would.")]
        public List<MagicSO> Abilities = new List<MagicSO>();
    }

    [Serializable]
    public class SandboxItemGrant
    {
        public ItemSO Item;
        [Min(1)] public int Count = 1;

        [Tooltip("Equipment only: the hero to equip it on. Empty leaves it in the bag.")]
        public HeroSO EquipOn;
    }
}
