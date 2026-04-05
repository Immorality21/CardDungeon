using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Rooms;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Dungeon.Editor
{
    public class ManualLevelLayoutEditorWindow : EditorWindow
    {
        private ManualLevelLayoutSO _layout;
        private SerializedObject _serializedLayout;
        private Vector2 _scrollPosition;
        private Vector2 _propertiesScrollPosition;
        private int _selectedRoomIndex = -1;
        private float _gridScale = 30f;
        private Vector2 _panOffset = new Vector2(200, 200);

        // Door connection mode
        private bool _connectingDoors;
        private int _doorSourceRoom = -1;

        // Dragging
        private bool _isDragging;
        private int _dragRoomIndex = -1;
        private Vector2Int _dragStartPos;

        // Room template to use when adding rooms
        private RoomSO _addRoomTemplate;

        private const float MinScale = 10f;
        private const float MaxScale = 80f;
        private const float LeftPanelWidth = 280f;
        private const float ToolbarHeight = 30f;

        [MenuItem("Tools/Dungeon/Manual Level Layout Editor")]
        public static ManualLevelLayoutEditorWindow Open()
        {
            var window = GetWindow<ManualLevelLayoutEditorWindow>("Level Layout Editor");
            window.minSize = new Vector2(700, 400);
            return window;
        }

        public void SetTarget(ManualLevelLayoutSO layout)
        {
            _layout = layout;
            _serializedLayout = layout != null ? new SerializedObject(layout) : null;
            _selectedRoomIndex = -1;
            _connectingDoors = false;
            Repaint();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Left panel: properties
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));
            DrawPropertiesPanel();
            EditorGUILayout.EndVertical();

            // Divider
            var dividerRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(2));
            EditorGUI.DrawRect(dividerRect, new Color(0.15f, 0.15f, 0.15f));

            // Right panel: visual grid map
            var mapRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawGridMap(mapRect);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPropertiesPanel()
        {
            _propertiesScrollPosition = EditorGUILayout.BeginScrollView(_propertiesScrollPosition);

            // Asset selection
            EditorGUI.BeginChangeCheck();
            var newLayout = (ManualLevelLayoutSO)EditorGUILayout.ObjectField(
                "Layout Asset", _layout, typeof(ManualLevelLayoutSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                SetTarget(newLayout);
            }

            if (_layout == null)
            {
                EditorGUILayout.HelpBox("Select a ManualLevelLayoutSO asset to edit.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            _serializedLayout.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_serializedLayout.FindProperty("Key"));
            EditorGUILayout.PropertyField(_serializedLayout.FindProperty("WallColor"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);

            // Start / Exit room
            var startProp = _serializedLayout.FindProperty("StartRoomIndex");
            var exitProp = _serializedLayout.FindProperty("ExitRoomIndex");
            var roomNames = GetRoomNames();
            startProp.intValue = EditorGUILayout.Popup("Start Room", startProp.intValue, roomNames);
            exitProp.intValue = EditorGUILayout.Popup("Exit Room", exitProp.intValue, roomNames);

            // Add room controls
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Add Room", EditorStyles.boldLabel);
            _addRoomTemplate = (RoomSO)EditorGUILayout.ObjectField("Room Template", _addRoomTemplate, typeof(RoomSO), false);
            if (GUILayout.Button("Add Room") && _addRoomTemplate != null)
            {
                AddRoom(_addRoomTemplate);
            }

            // Toolbar buttons
            EditorGUILayout.Space(5);
            EditorGUI.BeginDisabledGroup(_selectedRoomIndex < 0 || _selectedRoomIndex >= _layout.Rooms.Count);
            if (GUILayout.Button("Remove Selected Room"))
            {
                RemoveRoom(_selectedRoomIndex);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(5);
            if (_connectingDoors)
            {
                EditorGUILayout.HelpBox($"Click a second room to connect to Room {_doorSourceRoom}. Press Escape to cancel.", MessageType.Info);
                if (GUILayout.Button("Cancel Connection"))
                {
                    _connectingDoors = false;
                    _doorSourceRoom = -1;
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(_selectedRoomIndex < 0);
                if (GUILayout.Button("Connect Door From Selected"))
                {
                    _connectingDoors = true;
                    _doorSourceRoom = _selectedRoomIndex;
                }
                EditorGUI.EndDisabledGroup();
            }

            // Selected room details
            if (_selectedRoomIndex >= 0 && _selectedRoomIndex < _layout.Rooms.Count)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField($"Room {_selectedRoomIndex} Details", EditorStyles.boldLabel);

                var roomsProp = _serializedLayout.FindProperty("Rooms");
                var roomProp = roomsProp.GetArrayElementAtIndex(_selectedRoomIndex);

                EditorGUILayout.PropertyField(roomProp.FindPropertyRelative("RoomTemplate"));
                EditorGUILayout.PropertyField(roomProp.FindPropertyRelative("GridPosition"));
                EditorGUILayout.PropertyField(roomProp.FindPropertyRelative("GuaranteeAllSpawns"));
                EditorGUILayout.PropertyField(roomProp.FindPropertyRelative("EnemySpawnOverride"), true);

                // Show doors connected to this room
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Doors", EditorStyles.miniLabel);
                for (int i = _layout.Doors.Count - 1; i >= 0; i--)
                {
                    var door = _layout.Doors[i];
                    if (door.RoomIndexA == _selectedRoomIndex || door.RoomIndexB == _selectedRoomIndex)
                    {
                        int other = door.RoomIndexA == _selectedRoomIndex ? door.RoomIndexB : door.RoomIndexA;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  -> Room {other}", GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            Undo.RecordObject(_layout, "Remove Door");
                            _layout.Doors.RemoveAt(i);
                            EditorUtility.SetDirty(_layout);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            // Room list summary
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Rooms ({_layout.Rooms.Count})", EditorStyles.boldLabel);
            for (int i = 0; i < _layout.Rooms.Count; i++)
            {
                var room = _layout.Rooms[i];
                var label = room.RoomTemplate != null
                    ? $"[{i}] {room.RoomTemplate.Name} at ({room.GridPosition.x}, {room.GridPosition.y})"
                    : $"[{i}] (no template)";

                if (i == _layout.StartRoomIndex)
                {
                    label += " [START]";
                }
                if (i == _layout.ExitRoomIndex)
                {
                    label += " [EXIT]";
                }

                var style = i == _selectedRoomIndex ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button(label, style))
                {
                    _selectedRoomIndex = i;
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Doors ({_layout.Doors.Count})", EditorStyles.boldLabel);
            for (int i = 0; i < _layout.Doors.Count; i++)
            {
                var door = _layout.Doors[i];
                EditorGUILayout.LabelField($"  Room {door.RoomIndexA} <-> Room {door.RoomIndexB}");
            }

            _serializedLayout.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void DrawGridMap(Rect mapRect)
        {
            // Background
            EditorGUI.DrawRect(mapRect, new Color(0.18f, 0.18f, 0.18f));

            if (_layout == null)
            {
                return;
            }

            // Handle input events
            HandleMapInput(mapRect);

            // Draw grid lines
            GUI.BeginClip(mapRect);
            var localRect = new Rect(0, 0, mapRect.width, mapRect.height);
            DrawGridLines(localRect);

            // Draw door connections
            for (int i = 0; i < _layout.Doors.Count; i++)
            {
                var door = _layout.Doors[i];
                if (door.RoomIndexA >= 0 && door.RoomIndexA < _layout.Rooms.Count &&
                    door.RoomIndexB >= 0 && door.RoomIndexB < _layout.Rooms.Count)
                {
                    var roomA = _layout.Rooms[door.RoomIndexA];
                    var roomB = _layout.Rooms[door.RoomIndexB];
                    if (roomA.RoomTemplate != null && roomB.RoomTemplate != null)
                    {
                        var centerA = GetRoomScreenCenter(roomA);
                        var centerB = GetRoomScreenCenter(roomB);
                        Handles.color = new Color(1f, 0.8f, 0.2f, 0.8f);
                        Handles.DrawLine(centerA, centerB);
                    }
                }
            }

            // Draw rooms
            for (int i = 0; i < _layout.Rooms.Count; i++)
            {
                DrawRoom(i);
            }

            // Draw connection preview line
            if (_connectingDoors && _doorSourceRoom >= 0 && _doorSourceRoom < _layout.Rooms.Count)
            {
                var sourceRoom = _layout.Rooms[_doorSourceRoom];
                if (sourceRoom.RoomTemplate != null)
                {
                    var sourceCenter = GetRoomScreenCenter(sourceRoom);
                    var mousePos = Event.current.mousePosition;
                    Handles.color = new Color(0.2f, 1f, 0.2f, 0.6f);
                    Handles.DrawDottedLine(sourceCenter, mousePos, 4f);
                }
            }

            // Scale indicator
            var scaleLabel = $"Scale: {_gridScale:F0}  |  Pan: RMB drag  |  Zoom: Scroll";
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            GUI.Label(new Rect(5, localRect.height - 18, 400, 18), scaleLabel, labelStyle);

            GUI.EndClip();
        }

        private void DrawGridLines(Rect rect)
        {
            var gridColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

            // Calculate visible grid range
            float startX = -_panOffset.x;
            float startY = -_panOffset.y;
            float endX = startX + rect.width;
            float endY = startY + rect.height;

            int minGridX = Mathf.FloorToInt(startX / _gridScale);
            int maxGridX = Mathf.CeilToInt(endX / _gridScale);
            int minGridY = Mathf.FloorToInt(startY / _gridScale);
            int maxGridY = Mathf.CeilToInt(endY / _gridScale);

            Handles.color = gridColor;
            for (int x = minGridX; x <= maxGridX; x++)
            {
                float screenX = x * _gridScale + _panOffset.x;
                Handles.DrawLine(new Vector3(screenX, 0), new Vector3(screenX, rect.height));
            }
            for (int y = minGridY; y <= maxGridY; y++)
            {
                float screenY = y * _gridScale + _panOffset.y;
                Handles.DrawLine(new Vector3(0, screenY), new Vector3(rect.width, screenY));
            }

            // Draw origin crosshair
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            Handles.DrawLine(new Vector3(_panOffset.x, 0), new Vector3(_panOffset.x, rect.height));
            Handles.DrawLine(new Vector3(0, _panOffset.y), new Vector3(rect.width, _panOffset.y));
        }

        private void DrawRoom(int index)
        {
            var entry = _layout.Rooms[index];
            if (entry.RoomTemplate == null)
            {
                return;
            }

            var roomRect = GetRoomScreenRect(entry);
            var color = entry.RoomTemplate.Color;

            // Selected highlight
            bool isSelected = index == _selectedRoomIndex;
            if (isSelected)
            {
                var highlightRect = new Rect(roomRect.x - 2, roomRect.y - 2, roomRect.width + 4, roomRect.height + 4);
                EditorGUI.DrawRect(highlightRect, Color.white);
            }

            // Room fill
            var fillColor = new Color(color.r, color.g, color.b, 0.6f);
            EditorGUI.DrawRect(roomRect, fillColor);

            // Room border
            var borderColor = isSelected ? Color.white : new Color(color.r, color.g, color.b, 1f);
            DrawRectBorder(roomRect, borderColor);

            // Room label
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
            };

            var label = $"{index}";
            if (index == _layout.StartRoomIndex)
            {
                label += " S";
            }
            if (index == _layout.ExitRoomIndex)
            {
                label += " E";
            }

            GUI.Label(roomRect, label, labelStyle);

            // Room name below index if room is big enough
            if (roomRect.height > 25)
            {
                var nameRect = new Rect(roomRect.x, roomRect.y + roomRect.height * 0.5f, roomRect.width, roomRect.height * 0.5f);
                var nameStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1, 1, 1, 0.7f) },
                    fontSize = 9
                };
                GUI.Label(nameRect, entry.RoomTemplate.Name, nameStyle);
            }
        }

        private void DrawRectBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        private void HandleMapInput(Rect mapRect)
        {
            var evt = Event.current;
            var localMouse = evt.mousePosition - mapRect.position;

            if (!mapRect.Contains(evt.mousePosition))
            {
                return;
            }

            switch (evt.type)
            {
                case EventType.ScrollWheel:
                    float oldScale = _gridScale;
                    _gridScale = Mathf.Clamp(_gridScale - evt.delta.y * 2f, MinScale, MaxScale);

                    // Zoom toward mouse position
                    float scaleRatio = _gridScale / oldScale;
                    _panOffset = localMouse - (localMouse - _panOffset) * scaleRatio;

                    evt.Use();
                    Repaint();
                    break;

                case EventType.MouseDown:
                    if (evt.button == 0) // Left click
                    {
                        int clickedRoom = GetRoomAtScreenPos(localMouse);

                        if (_connectingDoors)
                        {
                            if (clickedRoom >= 0 && clickedRoom != _doorSourceRoom)
                            {
                                AddDoor(_doorSourceRoom, clickedRoom);
                                _connectingDoors = false;
                                _doorSourceRoom = -1;
                            }
                            evt.Use();
                        }
                        else if (clickedRoom >= 0)
                        {
                            _selectedRoomIndex = clickedRoom;
                            _isDragging = true;
                            _dragRoomIndex = clickedRoom;
                            _dragStartPos = _layout.Rooms[clickedRoom].GridPosition;
                            evt.Use();
                        }
                        else
                        {
                            _selectedRoomIndex = -1;
                            evt.Use();
                        }
                        Repaint();
                    }
                    else if (evt.button == 1) // Right click - start pan
                    {
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (evt.button == 1) // Pan
                    {
                        _panOffset += evt.delta;
                        evt.Use();
                        Repaint();
                    }
                    else if (evt.button == 0 && _isDragging && _dragRoomIndex >= 0)
                    {
                        // Convert mouse position to grid coordinates and snap
                        var gridPos = ScreenToGrid(localMouse);
                        var snapped = new Vector2Int(Mathf.RoundToInt(gridPos.x), Mathf.RoundToInt(gridPos.y));

                        if (snapped != _layout.Rooms[_dragRoomIndex].GridPosition)
                        {
                            Undo.RecordObject(_layout, "Move Room");
                            _layout.Rooms[_dragRoomIndex].GridPosition = snapped;
                            EditorUtility.SetDirty(_layout);
                        }
                        evt.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (evt.button == 0 && _isDragging)
                    {
                        _isDragging = false;
                        _dragRoomIndex = -1;
                        evt.Use();
                    }
                    break;

                case EventType.KeyDown:
                    if (evt.keyCode == KeyCode.Escape && _connectingDoors)
                    {
                        _connectingDoors = false;
                        _doorSourceRoom = -1;
                        evt.Use();
                        Repaint();
                    }
                    else if (evt.keyCode == KeyCode.Delete && _selectedRoomIndex >= 0)
                    {
                        RemoveRoom(_selectedRoomIndex);
                        evt.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseMove:
                    if (_connectingDoors)
                    {
                        Repaint();
                    }
                    break;
            }
        }

        // Coordinate conversion: grid (Y-up) -> screen (Y-down)
        private Vector2 GridToScreen(Vector2 gridPos)
        {
            return new Vector2(
                gridPos.x * _gridScale + _panOffset.x,
                -gridPos.y * _gridScale + _panOffset.y
            );
        }

        private Vector2 ScreenToGrid(Vector2 screenPos)
        {
            return new Vector2(
                (screenPos.x - _panOffset.x) / _gridScale,
                -(screenPos.y - _panOffset.y) / _gridScale
            );
        }

        private Rect GetRoomScreenRect(ManualRoomEntry entry)
        {
            // Room's bottom-left in grid -> screen top-left
            var topLeft = GridToScreen(new Vector2(entry.GridPosition.x, entry.GridPosition.y + entry.RoomTemplate.Height));
            float width = entry.RoomTemplate.Width * _gridScale;
            float height = entry.RoomTemplate.Height * _gridScale;
            return new Rect(topLeft.x, topLeft.y, width, height);
        }

        private Vector2 GetRoomScreenCenter(ManualRoomEntry entry)
        {
            var rect = GetRoomScreenRect(entry);
            return rect.center;
        }

        private int GetRoomAtScreenPos(Vector2 screenPos)
        {
            // Check in reverse order so rooms drawn on top are clicked first
            for (int i = _layout.Rooms.Count - 1; i >= 0; i--)
            {
                var entry = _layout.Rooms[i];
                if (entry.RoomTemplate == null)
                {
                    continue;
                }

                var rect = GetRoomScreenRect(entry);
                if (rect.Contains(screenPos))
                {
                    return i;
                }
            }
            return -1;
        }

        private void AddRoom(RoomSO template)
        {
            Undo.RecordObject(_layout, "Add Room");

            // Find a non-overlapping position
            var pos = Vector2Int.zero;
            if (_layout.Rooms.Count > 0)
            {
                var last = _layout.Rooms[_layout.Rooms.Count - 1];
                if (last.RoomTemplate != null)
                {
                    pos = new Vector2Int(last.GridPosition.x + last.RoomTemplate.Width + 1, last.GridPosition.y);
                }
            }

            _layout.Rooms.Add(new ManualRoomEntry
            {
                RoomTemplate = template,
                GridPosition = pos,
                EnemySpawnOverride = new List<Enemies.EnemySpawnEntry>()
            });

            _selectedRoomIndex = _layout.Rooms.Count - 1;
            EditorUtility.SetDirty(_layout);
            if (_serializedLayout != null)
            {
                _serializedLayout.Update();
            }
        }

        private void RemoveRoom(int index)
        {
            if (index < 0 || index >= _layout.Rooms.Count)
            {
                return;
            }

            Undo.RecordObject(_layout, "Remove Room");

            _layout.Rooms.RemoveAt(index);

            // Remove doors referencing this room and remap indices
            for (int i = _layout.Doors.Count - 1; i >= 0; i--)
            {
                var door = _layout.Doors[i];
                if (door.RoomIndexA == index || door.RoomIndexB == index)
                {
                    _layout.Doors.RemoveAt(i);
                }
                else
                {
                    if (door.RoomIndexA > index)
                    {
                        door.RoomIndexA--;
                    }
                    if (door.RoomIndexB > index)
                    {
                        door.RoomIndexB--;
                    }
                }
            }

            // Adjust start/exit indices
            if (_layout.StartRoomIndex == index)
            {
                _layout.StartRoomIndex = 0;
            }
            else if (_layout.StartRoomIndex > index)
            {
                _layout.StartRoomIndex--;
            }

            if (_layout.ExitRoomIndex == index)
            {
                _layout.ExitRoomIndex = Mathf.Max(0, _layout.Rooms.Count - 1);
            }
            else if (_layout.ExitRoomIndex > index)
            {
                _layout.ExitRoomIndex--;
            }

            _selectedRoomIndex = -1;
            EditorUtility.SetDirty(_layout);
            if (_serializedLayout != null)
            {
                _serializedLayout.Update();
            }
        }

        private void AddDoor(int roomA, int roomB)
        {
            // Check if door already exists
            foreach (var existing in _layout.Doors)
            {
                if ((existing.RoomIndexA == roomA && existing.RoomIndexB == roomB) ||
                    (existing.RoomIndexA == roomB && existing.RoomIndexB == roomA))
                {
                    Debug.LogWarning($"Door between rooms {roomA} and {roomB} already exists.");
                    return;
                }
            }

            Undo.RecordObject(_layout, "Add Door");
            _layout.Doors.Add(new ManualDoorEntry
            {
                RoomIndexA = roomA,
                RoomIndexB = roomB
            });
            EditorUtility.SetDirty(_layout);
            if (_serializedLayout != null)
            {
                _serializedLayout.Update();
            }
        }

        private string[] GetRoomNames()
        {
            var names = new string[_layout.Rooms.Count];
            for (int i = 0; i < _layout.Rooms.Count; i++)
            {
                var room = _layout.Rooms[i];
                names[i] = room.RoomTemplate != null
                    ? $"[{i}] {room.RoomTemplate.Name}"
                    : $"[{i}] (no template)";
            }
            return names;
        }
    }
}
