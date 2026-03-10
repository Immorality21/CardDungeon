using Assets.Scripts.Rooms;
using System.Collections.Generic;
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
    }
}
