using System.Collections.Generic;
using Assets.Scripts.IO;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    public class Party : MonoBehaviour
    {
        public List<Hero> Heroes = new List<Hero>();

        public Hero Leader => Heroes.Count > 0 ? Heroes[0] : null;
        public Room CurrentRoom { get; private set; }
        public Room PreviousRoom { get; private set; }

        private FileHandler _fileHandler;
        private PartySaveData _saveData;

        public void Initialize(List<HeroSO> heroDefinitions)
        {
            _fileHandler = new FileHandler();
            _saveData = _fileHandler.Load<PartySaveData>();

            foreach (var heroSO in heroDefinitions)
            {
                var heroObj = new GameObject(heroSO.Label);
                heroObj.transform.SetParent(transform, false);
                var hero = heroObj.AddComponent<Hero>();

                var savedHero = _saveData.Heroes.Find(h => h.HeroKey == heroSO.Label);
                if (savedHero != null)
                {
                    hero.HeroSO = heroSO;
                    hero.Level = savedHero.Level;
                    hero.CurrentXp = savedHero.CurrentXp;
                    hero.Stats = new Stats(savedHero.Attack, savedHero.Defense, savedHero.Health)
                    {
                        MaxHealth = savedHero.MaxHealth
                    };
                }
                else
                {
                    hero.Initialize(heroSO);
                }

                Heroes.Add(hero);
            }
        }

        public void PlaceInRoom(Room room)
        {
            PreviousRoom = CurrentRoom;
            CurrentRoom = room;
            var center = new Vector3(
                room.GridPosition.x + room.RoomSO.Width / 2f - 0.5f,
                room.GridPosition.y + room.RoomSO.Height / 2f - 0.5f,
                -1f);
            transform.position = center;
        }

        public void PlaceAtDoor(Door door, Room fromRoom)
        {
            PreviousRoom = CurrentRoom;
            var destRoom = door.GetOtherRoom(fromRoom);
            CurrentRoom = destRoom;
            var doorPos = door.GetPositionInRoom(destRoom);
            transform.position = new Vector3(doorPos.x, doorPos.y, -1f);
        }

        public void SaveParty()
        {
            _saveData.Heroes.Clear();
            foreach (var hero in Heroes)
            {
                _saveData.Heroes.Add(new HeroSaveData
                {
                    HeroKey = hero.HeroKey,
                    Level = hero.Level,
                    CurrentXp = hero.CurrentXp,
                    Attack = hero.Stats.Attack,
                    Defense = hero.Stats.Defense,
                    Health = hero.Stats.Health,
                    MaxHealth = hero.Stats.MaxHealth
                });
            }
            _fileHandler.Save(_saveData);
        }

        public void AddXpToLeader(int amount)
        {
            if (Leader != null)
            {
                Leader.AddXp(amount);
                SaveParty();
            }
        }
    }
}
