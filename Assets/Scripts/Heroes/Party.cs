using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.IO;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    public class Party : MonoBehaviour
    {
        [SerializeField] private float _fanOutDuration = 0.5f;
        [SerializeField] private float _gatherDuration = 0.4f;

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
                var heroObj = new GameObject(heroSO.Label);
                heroObj.transform.SetParent(transform, false);
                var hero = heroObj.AddComponent<Hero>();

                var savedHero = _saveData.Heroes.Find(h => h.HeroKey == heroSO.Label);
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

        public Coroutine FanOutHeroes(Room room)
        {
            return StartCoroutine(FanOutCoroutine(room));
        }

        private IEnumerator FanOutCoroutine(Room room)
        {
            // Hide the party sprite
            _spriteRenderer.enabled = false;

            // Calculate random target positions for each alive hero
            var targets = new List<Vector3>();
            var aliveHeroes = new List<Hero>();

            // Build avoid list starting with enemy positions
            var avoidPositions = new List<Vector3>();
            foreach (var enemy in room.Enemies)
            {
                if (enemy != null)
                {
                    avoidPositions.Add(enemy.transform.position);
                }
            }

            foreach (var hero in Heroes)
            {
                if (!hero.IsAlive)
                {
                    continue;
                }

                aliveHeroes.Add(hero);
                var worldPos = room.GetRandomWalkablePosition(avoidPositions, 0.8f);
                avoidPositions.Add(worldPos);
                var localPos = worldPos - transform.position;
                localPos.z = 0f;
                targets.Add(localPos);

                // Enable hero sprite for combat
                var sr = hero.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.enabled = true;
                }
            }

            // Animate heroes from center to their positions
            float elapsed = 0f;
            while (elapsed < _fanOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _fanOutDuration);

                for (int i = 0; i < aliveHeroes.Count; i++)
                {
                    aliveHeroes[i].transform.localPosition = Vector3.Lerp(Vector3.zero, targets[i], t);
                }

                yield return null;
            }

            // Snap to final positions
            for (int i = 0; i < aliveHeroes.Count; i++)
            {
                aliveHeroes[i].transform.localPosition = targets[i];
            }
        }

        public Coroutine GatherHeroes()
        {
            return StartCoroutine(GatherCoroutine());
        }

        private IEnumerator GatherCoroutine()
        {
            // Record starting positions
            var startPositions = new List<Vector3>();
            var aliveHeroes = new List<Hero>();

            foreach (var hero in Heroes)
            {
                if (!hero.IsAlive)
                {
                    continue;
                }

                aliveHeroes.Add(hero);
                startPositions.Add(hero.transform.localPosition);
            }

            // Animate back to center
            float elapsed = 0f;
            while (elapsed < _gatherDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _gatherDuration);

                for (int i = 0; i < aliveHeroes.Count; i++)
                {
                    aliveHeroes[i].transform.localPosition = Vector3.Lerp(startPositions[i], Vector3.zero, t);
                }

                yield return null;
            }

            // Snap and hide hero sprites, restore party sprite
            foreach (var hero in Heroes)
            {
                hero.transform.localPosition = Vector3.zero;
                var sr = hero.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.enabled = false;
                }
            }

            _spriteRenderer.enabled = true;
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
