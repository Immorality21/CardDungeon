using System;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Door : MonoBehaviour
    {
        /// <summary>
        /// How the door under the keyboard cursor is drawn. Tint alone is not enough on a dim sprite
        /// and scale alone reads as an animation glitch, so it does both.
        /// </summary>
        private static readonly Color HighlightTint = new Color(1f, 0.93f, 0.55f);
        private const float HighlightScale = 1.3f;

        public Room RoomA;
        public Room RoomB;
        public Vector2 PositionInA;
        public Vector2 PositionInB;

        public event Action<Door> OnDoorClicked;

        private SpriteRenderer _renderer;
        private Color _baseColor;
        private Vector3 _baseScale;
        private bool _cachedBase;
        private bool _highlighted;

        /// <summary>
        /// Marks this door as the one the arrow keys are pointing at. The untouched look is captured
        /// the first time rather than assumed, so a door prefab that is tinted or scaled by the level
        /// still returns to exactly what it was.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            CacheBaseLook();
            if (_highlighted == highlighted)
            {
                return;
            }

            _highlighted = highlighted;
            if (_renderer != null)
            {
                _renderer.color = highlighted ? HighlightTint : _baseColor;
            }
            // x/y only - z scale means nothing to a sprite and leaving it at 1 keeps the transform
            // honest for anything that later reads it.
            transform.localScale = highlighted
                ? new Vector3(_baseScale.x * HighlightScale, _baseScale.y * HighlightScale, _baseScale.z)
                : _baseScale;
        }

        private void CacheBaseLook()
        {
            if (_cachedBase)
            {
                return;
            }

            _renderer = GetComponent<SpriteRenderer>();
            _baseColor = _renderer != null ? _renderer.color : Color.white;
            _baseScale = transform.localScale;
            _cachedBase = true;
        }

        public Room GetOtherRoom(Room current)
        {
            return current == RoomA ? RoomB : RoomA;
        }

        public Vector2 GetPositionInRoom(Room room)
        {
            return room == RoomA ? PositionInA : PositionInB;
        }

        private void OnMouseDown()
        {
            OnDoorClicked?.Invoke(this);
        }
    }
}
