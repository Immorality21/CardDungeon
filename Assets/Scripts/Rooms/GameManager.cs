using System.Linq;
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

        public void EnterRoom(Room room, Door entryDoor = null)
        {
            room.Reveal();

            // Check if entering a cleared exit room
            if (room.IsExit && !room.Enemies.Any(e => e != null && e.IsAlive))
            {
                if (CombatManager.HasInstance)
                {
                    CombatManager.Instance.NotifyDungeonCleared();
                }
                return;
            }

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
