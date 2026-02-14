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
    }
}
