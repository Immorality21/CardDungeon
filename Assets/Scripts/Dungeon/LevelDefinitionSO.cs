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

        [Tooltip("How many rooms in this level are given a room event, drawn from each room's own " +
                 "PossibleEvents. Scarcity is the point: finding one should feel like something, so " +
                 "most rooms keep their flavour-text Examine/Action.")]
        public int EventsPerLevel;

        [Tooltip("Optional per-level combat backdrop. When set, the battle stage uses this instead " +
                 "of the default Resources background — lets each level/biome look distinct.")]
        public Sprite CombatBackground;
    }
}
