using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class GameManager : SingletonBehaviour<GameManager>
    {
        [SerializeField] private float _cameraFollowSpeed = 5f;

        public Party Party { get; private set; }

        private bool _followParty;
        private RoomActionUI _roomActionUI;

        public void Initialize(Party party, RoomActionUI roomActionUI)
        {
            Party = party;
            _roomActionUI = roomActionUI;
            _followParty = true;

            // Both the new-level and the resumed-level paths come through here, so this is the one
            // place a floor's theme has to start. Asking for the track already playing is a no-op,
            // so descending into a level with no music of its own keeps the bed running.
            LevelMusic.PlayExploration();
        }

        /// <summary>
        /// Enables/disables the camera's party-follow lerp. Combat freezes it (via
        /// <see cref="CombatStage"/>) so the battle stage stays centered on the frozen view.
        /// </summary>
        public void SetCameraFollow(bool follow)
        {
            _followParty = follow;
        }

        public void EnterRoom(Room room, Door entryDoor = null)
        {
            room.Reveal();

            // The exit room is an ordinary room: it completes the level only when the player takes
            // the stairs (RoomActionUI's Descend button). Ending the level on entry meant walking
            // into the wrong room finished it for you, with a level's worth of unexplored rooms and
            // unspent room events behind you.
            if (_roomActionUI != null)
            {
                _roomActionUI.Show(room, entryDoor);
            }

            if (DungeonSaveManager.Instance != null)
            {
                DungeonSaveManager.Instance.Save(room);
            }
        }

        /// <summary>Puts the camera straight onto its follow target (fast travel), rather than lerping there.</summary>
        public void SnapCamera()
        {
            if (Party == null || !MainCamera.HasInstance)
            {
                return;
            }
            MainCamera.Instance.SetPosition(CameraTarget());
        }

        private void Update()
        {
            if (!_followParty || Party == null)
            {
                return;
            }

            Vector3 target = CameraTarget();
            target.z = MainCamera.Instance.transform.position.z;
            MainCamera.Instance.transform.position = Vector3.Lerp(
                MainCamera.Instance.transform.position,
                target,
                _cameraFollowSpeed * Time.deltaTime);
        }

        /// <summary>
        /// The party, nudged so the room it stands in is not drawn under the dungeon HUD, the room
        /// and Fight/Flee bars or the party window (<see cref="CameraSafeArea"/>). With none of them
        /// up - the level-clear window - it is the party's own position.
        /// </summary>
        private Vector2 CameraTarget()
        {
            Vector2 target = Party.transform.position;
            var room = Party.CurrentRoom;
            var cam = MainCamera.Camera;
            if (room == null || room.RoomSO == null || cam == null || _roomActionUI == null)
            {
                return target;
            }

            _roomActionUI.GetCameraSafePanels(_safePanels);
            if (_safePanels.Count == 0)
            {
                return target;
            }

            // Tiles are centred on integer coordinates, so a room's floor runs half a tile past them.
            var bounds = new Rect(room.GridPosition.x - 0.5f, room.GridPosition.y - 0.5f, room.RoomSO.Width, room.RoomSO.Height);
            var half = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
            return CameraSafeArea.Nudge(bounds, target, half, _safePanels, RoomSafeMargin);
        }

        private readonly List<Rect> _safePanels = new List<Rect>();

        /// <summary>World space kept between the room's floor and the HUD: the wall band and a breath.</summary>
        private const float RoomSafeMargin = 0.4f;
    }
}
