using System.Collections.Generic;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "SO/Level Definition")]
    public class LevelDefinitionSO : ScriptableObject
    {
        public string Key;
        public List<RoomSO> RoomPool;
        public int RoomsToGenerate;
        [Range(0f, 1f)] public float ChainBias = 0.6f;
        [Range(0f, 1f)] public float MomentumBias = 0.5f;
        public Color WallColor = new Color(0.15f, 0.1f, 0.08f, 1f);
        public Texture2D WallTexture;

        [Tooltip("Rooms of this level promoted to a one-shot treasure cache: gold plus a depth-rolled " +
                 "item, and no enemies. Taken off the combat room count, so raising this lowers the " +
                 "level's attrition as well as adding a reward.")]
        [Min(0)] public int TreasureRooms;

        [Tooltip("Rooms of this level promoted to a one-shot refuge, healing every hero a share of " +
                 "their bar. Also a room that no longer holds a fight - the two levers move together.")]
        [Min(0)] public int RestRooms;

        [Tooltip("Raw materials this place is made of, found in its caches. Enemies drop what they " +
                 "are made of (EnemySO.LootTable); this is what the *floor* is made of, which is how " +
                 "a material ends up gating on where the player has been rather than on how long " +
                 "they have ground. Every entry rolls on its own when a cache is opened. " +
                 "NOTE this table only ever rolls if the level actually HAS a cache - a level with " +
                 "TreasureRooms 0 never yields any of it. Use GuaranteedMaterials for anything the " +
                 "player must come home with.")]
        public List<LootDrop> MaterialTable = new List<LootDrop>();

        [Tooltip("Materials awarded for CLEARING this level, every time, regardless of how the floor " +
                 "generated or which enemies spawned. The one drop the player is promised. " +
                 "Every other tap is a roll on top of a roll: MaterialTable needs the level to have " +
                 "rolled a cache, and an EnemySO.LootTable needs that enemy to have spawned and died. " +
                 "Neither can carry a promise, which is what anything that TEACHES the player - a " +
                 "tutorial pointing at a building they should go construct - actually needs. " +
                 "Entries must be authored at Chance 1: the guarantee is that this table is always " +
                 "rolled, not a new meaning for the chance field, and MaterialContentTests enforces " +
                 "it. Quantity ranges still apply, so '1-2 timber, always' is authorable. " +
                 "Forfeited on a wipe like every other gain - you have to CLEAR the floor.")]
        public List<LootDrop> GuaranteedMaterials = new List<LootDrop>();

        [Tooltip("Optional per-level combat backdrop. When set, the battle stage uses this instead " +
                 "of the default Resources background — lets each level/biome look distinct.")]
        public Sprite CombatBackground;

        [Tooltip("Optional per-level music while walking the floor. When set it wins over the " +
                 "MusicBank's Exploration track, so a biome can sound distinct as well as look it.")]
        public AudioClip ExplorationMusic;

        [Tooltip("Optional per-level combat music. When set it wins over the MusicBank's Combat " +
                 "track. Boss fights ignore it and use the bank's BossCombat track.")]
        public AudioClip CombatMusic;
    }
}
