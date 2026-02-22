using System.Collections.Generic;
using Assets.Scripts.Enemies;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    [CreateAssetMenu(menuName = "SO/Room")]
    public class RoomSO : ScriptableObject
    {
        public string Name;

        public int Width;

        public int Height;

        public Color Color;

        [TextArea]
        public List<string> ExamineOptions;

        [TextArea]
        public List<string> ActionOptions;

        public List<EnemySpawnEntry> EnemySpawnTable;
    }
}
