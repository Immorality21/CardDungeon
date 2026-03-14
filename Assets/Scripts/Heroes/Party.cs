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
                    hero.HeroSO = heroSO;
                    hero.Level = savedHero.Level;
                    hero.CurrentXp = savedHero.CurrentXp;
                    hero.Stats = new Stats(savedHero.Attack, savedHero.Defense, savedHero.Health, savedHero.Agility)
                    {
                        MaxHealth = savedHero.MaxHealth
                    };
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

            foreach (var hero in Heroes)
            {
                if (!hero.IsAlive)
                {
                    continue;
                }

                aliveHeroes.Add(hero);
                var worldPos = GetRandomPositionInRoom(room, targets);
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

        private Vector3 GetRandomPositionInRoom(Room room, List<Vector3> existingLocalPositions)
        {
            var gridPos = room.GridPosition;
            var width = room.RoomSO.Width;
            var height = room.RoomSO.Height;

            float minX = gridPos.x + 1;
            float maxX = gridPos.x + width - 2;
            float minY = gridPos.y + 1;
            float maxY = gridPos.y + height - 2;

            if (minX > maxX || minY > maxY)
            {
                return new Vector3(
                    gridPos.x + width / 2f - 0.5f,
                    gridPos.y + height / 2f - 0.5f,
                    -1f);
            }

            for (int attempt = 0; attempt < 10; attempt++)
            {
                var worldPos = new Vector3(
                    Random.Range(minX, maxX + 1),
                    Random.Range(minY, maxY + 1),
                    -1f);

                var localPos = worldPos - transform.position;
                localPos.z = 0f;

                bool overlaps = false;
                foreach (var existing in existingLocalPositions)
                {
                    if (Vector3.Distance(existing, localPos) < 0.8f)
                    {
                        overlaps = true;
                        break;
                    }
                }

                // Also check against enemies
                if (!overlaps)
                {
                    foreach (var enemy in room.Enemies)
                    {
                        if (enemy != null && Vector3.Distance(enemy.transform.position, worldPos) < 0.8f)
                        {
                            overlaps = true;
                            break;
                        }
                    }
                }

                if (!overlaps)
                {
                    return worldPos;
                }
            }

            return new Vector3(
                Random.Range(minX, maxX + 1),
                Random.Range(minY, maxY + 1),
                -1f);
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
                    MaxHealth = hero.Stats.MaxHealth,
                    Agility = hero.Stats.Agility
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
