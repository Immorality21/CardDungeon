using System.Collections.Generic;
using Assets.Scripts.IO;
using Assets.Scripts.Progression;
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
                SpawnHero(heroSO);
            }

            // Set party sprite to leader's sprite
            if (Leader != null && Leader.HeroSO.Sprite != null)
            {
                _spriteRenderer.sprite = Leader.HeroSO.Sprite;
            }
        }


        /// <summary>
        /// Adds a hero to the live party mid-run - a captive freed in a dungeon joins immediately so
        /// the rescue pays off in the fights that follow, rather than only after the level. Ignores
        /// heroes already present. Returns the new <see cref="Hero"/>, or null if nothing was added.
        /// </summary>
        public Hero AddHero(HeroSO heroSO)
        {
            if (heroSO == null)
            {
                return null;
            }
            if (Heroes.Exists(h => h != null && h.HeroKey == heroSO.SaveKey))
            {
                return null;
            }

            var hero = SpawnHero(heroSO);
            if (hero != null && hero.Stats != null)
            {
                // Joins at full health: they were not in the fights that wore the party down.
                hero.Stats.Health = hero.Stats.MaxHealth;
            }
            return hero;
        }

        /// <summary>
        /// Builds one hero GameObject: stats from the definition (restored from saved XP when a
        /// record exists) plus the combat SpriteRenderer, hidden until fan-out.
        /// </summary>
        private Hero SpawnHero(HeroSO heroSO)
        {
            if (heroSO == null)
            {
                return null;
            }

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
            return hero;
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
            // Update in place rather than rebuilding: a hero can be owned without being in the
            // current party, and clearing the list would throw away their XP record.
            foreach (var hero in Heroes)
            {
                if (hero == null)
                {
                    continue;
                }

                var existing = _saveData.Heroes.Find(h => h.HeroKey == hero.HeroKey);
                if (existing != null)
                {
                    existing.CurrentXp = hero.CurrentXp;
                }
                else
                {
                    _saveData.Heroes.Add(new HeroSaveData
                    {
                        HeroKey = hero.HeroKey,
                        CurrentXp = hero.CurrentXp
                    });
                }
            }
            _fileHandler.Save(_saveData);
        }

        /// <summary>
        /// Records <paramref name="heroSO"/> as owned in the party save without writing to disk -
        /// the write happens in <see cref="CommitProgress"/> on level clear, so a rescue is
        /// forfeited on death exactly like XP and loot.
        /// </summary>
        public void MarkOwnedDeferred(HeroSO heroSO)
        {
            if (heroSO == null || string.IsNullOrEmpty(heroSO.SaveKey))
            {
                return;
            }
            if (_saveData.OwnedHeroKeys == null)
            {
                _saveData.OwnedHeroKeys = new List<string>();
            }
            if (!_saveData.OwnedHeroKeys.Contains(heroSO.SaveKey))
            {
                _saveData.OwnedHeroKeys.Add(heroSO.SaveKey);
            }

            MarkFieldedDeferred(heroSO);
        }

        /// <summary>
        /// Fields a newly acquired hero for the next level too, if the party cap has room. Without
        /// this a captive rescued on level 1 fights the rest of that level and then vanishes from the
        /// lineup, because the next level is built from the *selected* party - which is exactly the
        /// bug the tutorial's Tank rescue would hit. Deferred like the ownership it accompanies.
        /// </summary>
        private void MarkFieldedDeferred(HeroSO heroSO)
        {
            if (_saveData.SelectedHeroKeys == null)
            {
                _saveData.SelectedHeroKeys = new List<string>();
            }
            if (_saveData.SelectedHeroKeys.Contains(heroSO.SaveKey))
            {
                return;
            }

            int cap = MetaProgressManager.HasInstance
                ? MetaProgressManager.Instance.GetPartyCap()
                : PartySlots.BaseCap;

            // An empty stored selection means "everyone owned", so it is already implicitly full -
            // count the live party instead, which is the roster plus whoever just joined.
            int fielded = _saveData.SelectedHeroKeys.Count > 0
                ? _saveData.SelectedHeroKeys.Count
                : Heroes.Count;
            if (fielded > cap)
            {
                return;
            }

            if (_saveData.SelectedHeroKeys.Count == 0)
            {
                // Materialise the implicit selection so adding to it means something.
                foreach (var hero in Heroes)
                {
                    if (hero != null && !_saveData.SelectedHeroKeys.Contains(hero.HeroKey))
                    {
                        _saveData.SelectedHeroKeys.Add(hero.HeroKey);
                    }
                }
            }

            if (!_saveData.SelectedHeroKeys.Contains(heroSO.SaveKey) &&
                _saveData.SelectedHeroKeys.Count < cap)
            {
                _saveData.SelectedHeroKeys.Add(heroSO.SaveKey);
            }
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

        /// <summary>
        /// Splits <paramref name="amount"/> XP evenly across the whole party, the leader taking the
        /// remainder. See <see cref="XpSplit"/> for why it is even and why the downed are paid.
        ///
        /// <para>This is the only XP path in the game: <c>CombatManager.HandleEnemyDeath</c> calls it
        /// per kill, in memory, and <see cref="CommitProgress"/> writes it on level clear - so a wipe
        /// forfeits the run's XP along with its gold and loot.</para>
        /// </summary>
        public void DistributeXp(int amount)
        {
            var shares = XpSplit.Split(amount, Heroes.Count);
            for (int i = 0; i < shares.Length; i++)
            {
                // Unity-aware null check rather than ?., which reads a destroyed hero as non-null.
                if (Heroes[i] != null)
                {
                    Heroes[i].AddXp(shares[i]);
                }
            }
        }
    }
}
