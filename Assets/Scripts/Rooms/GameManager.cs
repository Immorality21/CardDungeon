using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class GameManager : SingletonBehaviour<GameManager>
    {
        [SerializeField] private float _cameraFollowSpeed = 5f;

        public Player Player { get; private set; }

        private bool _followPlayer;
        private RoomActionUI _roomActionUI;

        public void Initialize(Player player, RoomActionUI roomActionUI)
        {
            Player = player;
            _roomActionUI = roomActionUI;
            _followPlayer = true;
        }

        public void EnterRoom(Room room, Door entryDoor = null)
        {
            if (_roomActionUI != null)
                _roomActionUI.Show(room, entryDoor);
        }

        private void Update()
        {
            if (!_followPlayer || Player == null) return;

            var target = Player.transform.position;
            target.z = MainCamera.Instance.transform.position.z;
            MainCamera.Instance.transform.position = Vector3.Lerp(
                MainCamera.Instance.transform.position,
                target,
                _cameraFollowSpeed * Time.deltaTime);
        }
    }
}
