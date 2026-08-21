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

        private void Update()
        {
            if (!_followParty || Party == null)
            {
                return;
            }

            var target = Party.transform.position;
            target.z = MainCamera.Instance.transform.position.z;
            MainCamera.Instance.transform.position = Vector3.Lerp(
                MainCamera.Instance.transform.position,
                target,
                _cameraFollowSpeed * Time.deltaTime);
        }
    }
}
