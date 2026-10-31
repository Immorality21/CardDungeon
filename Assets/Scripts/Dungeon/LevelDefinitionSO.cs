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

        [Tooltip("Rooms of this level promoted to a one-shot treasure cache: gold plus a depth-rolled " +
                 "item, and no enemies. Taken off the combat room count, so raising this lowers the " +
                 "level's attrition as well as adding a reward.")]
        [Min(0)] public int TreasureRooms;

        [Tooltip("Rooms of this level promoted to a one-shot refuge, healing every hero a share of " +
                 "their bar. Also a room that no longer holds a fight - the two levers move together.")]
        [Min(0)] public int RestRooms;

        [Tooltip("The room events this level can offer, each at its own chance. Empty means none: " +
                 "an event belongs to a biome and is tuned per level.")]
        public List<Rooms.Events.LevelEventEntry> RoomEvents = new List<Rooms.Events.LevelEventEntry>();

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

        [Tooltip("Recipes this level can hand over when it is CLEARED (ItemCategory.Recipe). Each entry " +
                 "rolls on its own chance at the stairs, and one the player already knows is skipped - " +
                 "so a Chance of 1 teaches it on the first clear and never again. Forfeited on a wipe " +
                 "like every other gain. Enemy drop tables can carry recipes too.")]
        public List<LootDrop> RecipeTable = new List<LootDrop>();

        [Tooltip("Recipes a room event can pay on this level (RoomEventOutcome.PaysPoolRecipe), found " +
                 "no other way. A recipe's RevisitFear sets the Fear level it opens at. The event stops " +
                 "turning up once the party knows all it can reach here.")]
        public List<ItemSO> EventRecipes = new List<ItemSO>();

        [Tooltip("Optional per-level combat backdrop. When set, the battle stage uses this instead " +
                 "of the default Resources background — lets each level/biome look distinct.")]
        public Sprite CombatBackground;

        [Tooltip("Optional per-level rock drawn under the whole floor while walking it. Must be " +
                 "imported with Mesh Type Full Rect and Wrap Mode Repeat, since it is tiled. When " +
                 "empty the default Resources/DungeonBackdrops/bedrock is used.")]
        public Sprite MapBackdrop;

        [Tooltip("Required: what the floor is drawn with (a tile-tall north wall, thin side walls, " +
                 "props). Changing it invalidates a save made mid-floor on the old biome.")]
        public BiomeSO Biome;

        [Tooltip("Optional per-level music while walking the floor. When set it wins over the " +
                 "MusicBank's Exploration track, so a biome can sound distinct as well as look it.")]
        public AudioClip ExplorationMusic;

        [Tooltip("Optional per-level combat music. When set it wins over the MusicBank's Combat " +
                 "track. Boss fights ignore it and use the bank's BossCombat track.")]
        public AudioClip CombatMusic;

        [Tooltip("Optional stairs drawn in the exit room, so a biome's way down looks like it belongs " +
                 "there (playtest 3 finding 14). Empty uses DungeonManager's default stairs.")]
        public Sprite ExitMarker;

        [Tooltip("The chests a cache room on this floor can show, each a closed and an opened sprite " +
                 "(a sunken chest in the March). One is picked per room. Empty uses DungeonManager's " +
                 "generic chests.")]
        public List<ChestSkin> Chests = new List<ChestSkin>();
    }

    /// <summary>A cache room's chest: how it looks before and after it is opened.</summary>
    [System.Serializable]
    public class ChestSkin
    {
        public Sprite Closed;
        public Sprite Open;

        public bool IsValid => Closed != null && Open != null;
    }
}
