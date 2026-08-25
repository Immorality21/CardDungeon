using System.Collections.Generic;
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

        [Tooltip("Rooms of this level promoted to a one-shot treasure cache: gold plus a depth-rolled " +
                 "item, and no enemies. Taken off the combat room count, so raising this lowers the " +
                 "level's attrition as well as adding a reward.")]
        [Min(0)] public int TreasureRooms;

        [Tooltip("Rooms of this level promoted to a one-shot refuge, healing every hero a share of " +
                 "their bar. Also a room that no longer holds a fight - the two levers move together.")]
        [Min(0)] public int RestRooms;

        [Tooltip("Optional per-level combat backdrop. When set, the battle stage uses this instead " +
                 "of the default Resources background — lets each level/biome look distinct.")]
        public Sprite CombatBackground;
    }
}
