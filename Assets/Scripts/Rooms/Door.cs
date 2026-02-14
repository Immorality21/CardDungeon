using System;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Door : MonoBehaviour
    {
        public Room RoomA;
        public Room RoomB;
        public Vector2 PositionInA;
        public Vector2 PositionInB;

        public event Action<Door> OnDoorClicked;

        private bool _isHighlighted;
        private GameObject _arrow;

        private static readonly float BounceSpeed = 3f;
        private static readonly float BounceAmount = 0.15f;
        private static readonly float ArrowOffset = 0.7f;

        public Room GetOtherRoom(Room current)
        {
            return current == RoomA ? RoomB : RoomA;
        }

        public Vector2 GetPositionInRoom(Room room)
        {
            return room == RoomA ? PositionInA : PositionInB;
        }

        public void Highlight()
        {
            _isHighlighted = true;
            CreateArrow();
        }

        public void Unhighlight()
        {
            _isHighlighted = false;
            DestroyArrow();
        }

        private void Update()
        {
            if (_arrow != null)
            {
                var pos = _arrow.transform.localPosition;
                pos.y = ArrowOffset + Mathf.Sin(Time.time * BounceSpeed) * BounceAmount;
                _arrow.transform.localPosition = pos;
            }
        }

        private void CreateArrow()
        {
            DestroyArrow();

            _arrow = new GameObject("DoorArrow");
            _arrow.transform.SetParent(transform, false);
            _arrow.transform.localPosition = new Vector3(0, ArrowOffset, -1f);

            var meshFilter = _arrow.AddComponent<MeshFilter>();
            var meshRenderer = _arrow.AddComponent<MeshRenderer>();

            // Downward-pointing triangle
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.15f, 0.2f, 0),
                new Vector3( 0.15f, 0.2f, 0),
                new Vector3( 0f,    0f,   0)
            };
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            meshFilter.mesh = mesh;

            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.material.color = Color.yellow;
            meshRenderer.sortingOrder = 10;
        }

        private void DestroyArrow()
        {
            if (_arrow != null)
            {
                Destroy(_arrow);
                _arrow = null;
            }
        }

        private void OnMouseDown()
        {
            if (_isHighlighted)
                OnDoorClicked?.Invoke(this);
        }
    }
}
