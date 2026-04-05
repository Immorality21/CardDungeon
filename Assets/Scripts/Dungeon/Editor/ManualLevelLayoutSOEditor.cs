using Assets.Scripts.Dungeon;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Dungeon.Editor
{
    [CustomEditor(typeof(ManualLevelLayoutSO))]
    public class ManualLevelLayoutSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Open Visual Editor", GUILayout.Height(30)))
            {
                var window = ManualLevelLayoutEditorWindow.Open();
                window.SetTarget((ManualLevelLayoutSO)target);
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Validate Layout"))
            {
                ValidateLayout((ManualLevelLayoutSO)target);
            }
        }

        private void ValidateLayout(ManualLevelLayoutSO layout)
        {
            bool valid = true;

            if (layout.Rooms.Count == 0)
            {
                Debug.LogWarning("Layout has no rooms.");
                valid = false;
            }

            if (layout.StartRoomIndex < 0 || layout.StartRoomIndex >= layout.Rooms.Count)
            {
                Debug.LogError($"StartRoomIndex ({layout.StartRoomIndex}) is out of range.");
                valid = false;
            }

            if (layout.ExitRoomIndex < 0 || layout.ExitRoomIndex >= layout.Rooms.Count)
            {
                Debug.LogError($"ExitRoomIndex ({layout.ExitRoomIndex}) is out of range.");
                valid = false;
            }

            if (layout.StartRoomIndex == layout.ExitRoomIndex && layout.Rooms.Count > 1)
            {
                Debug.LogWarning("Start and exit room are the same.");
            }

            // Check for null RoomTemplates
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (layout.Rooms[i].RoomTemplate == null)
                {
                    Debug.LogError($"Room {i} has no RoomTemplate assigned.");
                    valid = false;
                }
            }

            // Check for overlapping rooms
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var a = layout.Rooms[i];
                if (a.RoomTemplate == null)
                {
                    continue;
                }

                for (int j = i + 1; j < layout.Rooms.Count; j++)
                {
                    var b = layout.Rooms[j];
                    if (b.RoomTemplate == null)
                    {
                        continue;
                    }

                    if (RoomsOverlap(a, b))
                    {
                        Debug.LogWarning($"Rooms {i} and {j} overlap.");
                    }
                }
            }

            // Check door connections
            foreach (var door in layout.Doors)
            {
                if (door.RoomIndexA < 0 || door.RoomIndexA >= layout.Rooms.Count ||
                    door.RoomIndexB < 0 || door.RoomIndexB >= layout.Rooms.Count)
                {
                    Debug.LogError($"Door references out-of-range room index ({door.RoomIndexA} -> {door.RoomIndexB}).");
                    valid = false;
                    continue;
                }

                var roomA = layout.Rooms[door.RoomIndexA];
                var roomB = layout.Rooms[door.RoomIndexB];
                if (roomA.RoomTemplate != null && roomB.RoomTemplate != null &&
                    !RoomsAdjacent(roomA, roomB))
                {
                    Debug.LogWarning($"Door between rooms {door.RoomIndexA} and {door.RoomIndexB}: rooms are not adjacent (no shared edge).");
                }
            }

            if (valid)
            {
                Debug.Log("Layout validation passed.");
            }
        }

        private bool RoomsOverlap(ManualRoomEntry a, ManualRoomEntry b)
        {
            int aLeft = a.GridPosition.x;
            int aRight = a.GridPosition.x + a.RoomTemplate.Width;
            int aBottom = a.GridPosition.y;
            int aTop = a.GridPosition.y + a.RoomTemplate.Height;

            int bLeft = b.GridPosition.x;
            int bRight = b.GridPosition.x + b.RoomTemplate.Width;
            int bBottom = b.GridPosition.y;
            int bTop = b.GridPosition.y + b.RoomTemplate.Height;

            return aLeft < bRight && aRight > bLeft && aBottom < bTop && aTop > bBottom;
        }

        private bool RoomsAdjacent(ManualRoomEntry a, ManualRoomEntry b)
        {
            int aLeft = a.GridPosition.x;
            int aRight = a.GridPosition.x + a.RoomTemplate.Width;
            int aBottom = a.GridPosition.y;
            int aTop = a.GridPosition.y + a.RoomTemplate.Height;

            int bLeft = b.GridPosition.x;
            int bRight = b.GridPosition.x + b.RoomTemplate.Width;
            int bBottom = b.GridPosition.y;
            int bTop = b.GridPosition.y + b.RoomTemplate.Height;

            // Check right edge of A touches left edge of B (or vice versa)
            if (aRight == bLeft || bRight == aLeft)
            {
                int overlapMin = Mathf.Max(aBottom, bBottom);
                int overlapMax = Mathf.Min(aTop, bTop);
                return overlapMax > overlapMin;
            }

            // Check top edge of A touches bottom edge of B (or vice versa)
            if (aTop == bBottom || bTop == aBottom)
            {
                int overlapMin = Mathf.Max(aLeft, bLeft);
                int overlapMax = Mathf.Min(aRight, bRight);
                return overlapMax > overlapMin;
            }

            return false;
        }
    }
}
