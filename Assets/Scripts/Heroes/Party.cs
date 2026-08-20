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

        private SpriteRenderer _spriteRenderer;
        private FileHandler _fileHandler;
        private PartySaveData _saveData;

        public void Initialize(List<HeroSO> heroDefinitions)
        {
            _fileHandler = new FileHandler();
            _saveData = _fileHandler.Load<PartySaveData>();

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            foreach (var heroSO in heroDefinitions)
            {
                var heroObj = new GameObject(heroSO.DisplayName);
                heroObj.transform.SetParent(transform, false);
                var hero = heroObj.AddComponent<Hero>();

                var savedHero = _saveData.Heroes.Find(h => h.HeroKey == heroSO.SaveKey);
                if (savedHero != null)
                {
                    hero.InitializeFromSave(heroSO, savedHero.CurrentXp);
                }
                else
                {
                    hero.Initialize(heroSO);
                }

                // Add a SpriteRenderer for combat display, hidden by default
                var heroSR = heroObj.AddComponent<SpriteRenderer>();
                if (heroSO.Sprite != null)
                {
                    heroSR.sprite = heroSO.Sprite;
                }
                heroSR.sortingOrder = 1;
                heroSR.enabled = false;

                Heroes.Add(hero);
            }

            // Set party sprite to leader's sprite
            if (Leader != null && Leader.HeroSO.Sprite != null)
            {
                _spriteRenderer.sprite = Leader.HeroSO.Sprite;
            }
        }

        public void PlaceInRoom(Room room)
        {
            PreviousRoom = CurrentRoom;
            CurrentRoom = room;
            transform.position = room.GetCenter();
        }

        public void PlaceAtDoor(Door door, Room fromRoom)
        {
            PreviousRoom = CurrentRoom;
            var destRoom = door.GetOtherRoom(fromRoom);
            CurrentRoom = destRoom;
            var doorPos = door.GetPositionInRoom(destRoom);
            transform.position = new Vector3(doorPos.x, doorPos.y, -1f);
        }

        /// <summary>
        /// Hides the travelling "party blob" sprite for the duration of combat. Hero sprites
        /// are enabled and positioned by <see cref="Assets.Scripts.Combat.CombatStage"/>.
        /// </summary>
        public void HidePartyForCombat()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Restores the party to its out-of-combat state: every hero snapped back to the party
        /// centre with its combat sprite hidden, and the party blob sprite shown again. Called
        /// by <see cref="Assets.Scripts.Combat.CombatStage"/> when the battle ends.
        /// </summary>
        public void RestoreAfterCombat()
        {
            foreach (var hero in Heroes)
            {
                if (hero == null)
                {
                    continue;
                }
                hero.transform.localPosition = Vector3.zero;
                var sr = hero.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.enabled = false;
                }
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
            }
        }


        public void SaveParty()
        {
            _saveData.Heroes.Clear();
            foreach (var hero in Heroes)
            {
                _saveData.Heroes.Add(new HeroSaveData
                {
                    HeroKey = hero.HeroKey,
                    CurrentXp = hero.CurrentXp
                });
            }
            _fileHandler.Save(_saveData);
        }

        public void HealAll()
        {
            foreach (var hero in Heroes)
            {
                if (hero.Stats != null)
                {
                    hero.Stats.Health = hero.Stats.MaxHealth;
                }
            }
        }

        public void CommitProgress()
        {
            SaveParty();
        }

        public void AddXpToLeader(int amount)
        {
            if (Leader != null)
            {
                Leader.AddXp(amount);
            }
        }
    }
}
