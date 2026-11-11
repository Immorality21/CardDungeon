using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using ImmoralityGaming.Fundamentals;
using UnityEngine;
using Assets.Scripts.Events;

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
            _hasFocus = false;
            // WASD walks the party (PartyWalk) and the camera follows it, so the manual pan - which
            // reads the same keys from raw Input - stays off for the whole floor.
            if (MainCamera.HasInstance)
            {
                MainCamera.Instance.AllowManualPan = false;
            }

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
            // the stairs (RoomActionUI.UseFacing). Ending the level on entry meant walking
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

            GameEvents.Publish(new RoomEntered { Room = room });
        }

        /// <summary>Puts the camera straight onto its follow target (fast travel), rather than lerping there.</summary>
        public void SnapCamera()
        {
            if (Party == null || !MainCamera.HasInstance)
            {
                return;
            }
            _focus = Party.transform.position;
            _hasFocus = true;
            MainCamera.Instance.SetPosition(CameraTarget());
        }

        private void Update()
        {
            if (!_followParty || Party == null)
            {
                return;
            }

            if (!_hasFocus)
            {
                _focus = Party.transform.position;
                _hasFocus = true;
            }
            _focus = CameraSafeArea.FollowDeadZone(_focus, Party.transform.position, DeadZoneTiles);

            Vector3 target = CameraTarget();
            target.z = MainCamera.Instance.transform.position.z;
            MainCamera.Instance.transform.position = Vector3.Lerp(
                MainCamera.Instance.transform.position,
                target,
                _cameraFollowSpeed * Time.deltaTime);
        }

        /// <summary>
        /// The leader, followed (playtest 4 findings 18 and 55): a point that trails the leader by at
        /// most <see cref="DeadZoneTiles"/>, looked <see cref="LookAhead"/> ahead the way the party
        /// faces - so walking down shows what is below - and moved clear of the dungeon HUD, the Fight
        /// bar and the party window (<see cref="CameraSafeArea.LeaderTarget"/>). It is a function of the
        /// leader and the panels only, never of the room, so a door crossed is no jump, and never of
        /// the camera's own position, so the lerp converges.
        /// </summary>
        private Vector2 CameraTarget()
        {
            Vector2 focus = _hasFocus ? _focus : (Vector2)Party.transform.position;
            var cam = MainCamera.Camera;
            if (cam == null || _roomActionUI == null)
            {
                return focus;
            }

            _roomActionUI.GetCameraSafePanels(_safePanels);
            var half = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
            return CameraSafeArea.LeaderTarget(focus, Party.Facing, LookAhead, LeaderViewHalf, half, _safePanels);
        }

        private readonly List<Rect> _safePanels = new List<Rect>();
        private Vector2 _focus;
        private bool _hasFocus;

        /// <summary>How far the leader may stray from the followed point before it moves, in tiles.</summary>
        private const float DeadZoneTiles = 0.5f;

        /// <summary>
        /// How far ahead of the followed point the camera looks, the way the party faces. More than the
        /// dead zone plus the lerp's lag at a walk, so a walking leader sits near the middle and a
        /// standing one sees a tile further where they face.
        /// </summary>
        private const float LookAhead = 1.5f;

        /// <summary>
        /// Half the box around the camera's point that is kept out from under the panels: the leader
        /// and a couple of tiles around them. A room no longer is (it made every door a camera jump).
        /// </summary>
        private static readonly Vector2 LeaderViewHalf = new Vector2(3f, 2.5f);
    }
}
