using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Rooms;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Presents combat as a Final-Fantasy-style side-view battle stage: heroes formed up in a
    /// left column, enemies in a right column, over a full-viewport background that hides the
    /// dungeon. It relocates the existing unit Transforms into fixed slots (rather than
    /// building a new render layer), so the world-space HP bars, hit-flash, floating damage
    /// text, and lunge animation all keep working at the new positions with no changes.
    /// Auto-creates on first use (no scene wiring), mirroring <see cref="CombatFeedback"/>.
    /// </summary>
    public class CombatStage : SingletonBehaviour<CombatStage>
    {
        // Battle sorting band. Dungeon tiles/walls/enemies sit at 0..5; the background hides
        // them at 400; relocated units sit at 600 (above the background, below the HP bars at
        // 900 and floating text at 1000). Bumping units to 600 is mandatory — enemies start at
        // sortingOrder 5, i.e. *below* the background, and would otherwise be hidden.
        private const int BackgroundSortOrder = 400;
        private const int UnitSortOrder = 600;
        private const int MaxRanks = 2; // EnemyFormation ranks 0..2 sort at 602..600
        private const float CombatUnitScale = 1.5f;

        // Battle backdrop loaded from Resources (drop a sprite here to replace the solid fill).
        private const string BackgroundResourcePath = "CombatBackgrounds/battle";

        [SerializeField] private Sprite _backgroundArt; // inspector override (wins over Resources)

        private static Sprite _solidSprite;

        private struct UnitRestore
        {
            public SpriteRenderer Sr;
            public int OrigSortingOrder;
            public bool OrigFlipX;
            public Vector3 OrigPos;
            public Vector3 OrigScale;
            public bool IsHero;
        }

        private readonly List<UnitRestore> _restores = new List<UnitRestore>();
        private GameObject _backgroundGo;
        private SpriteRenderer _backgroundSr;
        private Material _backgroundMaterial;
        private Party _party;
        private Behaviour _partyLight;
        private GameObject _leftCombatLight;
        private GameObject _rightCombatLight;

        // Where the hero column stands, kept from Begin so a party-replacing summon can take it.
        private float _heroColumnX;
        private float _centerY;

        // Hero sprites hidden while a summon has taken the party's place, shown again when it leaves.
        private readonly List<SpriteRenderer> _hidden = new List<SpriteRenderer>();

        /// <summary>A summon's scale on the stage. The Cairn Golem (64 px at PPU 38) comes out about
        /// three units tall, the size of a boss - it is a wall, and should read as one.</summary>
        private const float SummonScale = 1.8f;

        /// <summary>
        /// Freezes the camera, raises the background, and forms alive heroes (left) and enemies
        /// (right) into columns centred on the current view. Call once when combat starts,
        /// before <c>EnsureHealthBars</c> so the bars anchor at the battle positions.
        /// </summary>
        public void Begin(Party party, Room room)
        {
            _party = party;

            foreach (var component in party.GetComponents<Behaviour>())
            {
                if (component.GetType().Name == "Light2D")
                {
                    _partyLight = component;
                    _partyLight.enabled = false;
                    break;
                }
            }

            _restores.Clear();

            // Snap the camera to the party centre, then freeze the follow so the stage holds.
            var mainCamera = MainCamera.Instance;
            mainCamera.SetPosition(party.transform.position);
            mainCamera.AllowManualPan = false; // arrow/WASD drive the command cursor, not the camera
            if (GameManager.HasInstance)
            {
                GameManager.Instance.SetCameraFollow(false);
            }

            var cam = Camera.main;
            float halfH = cam != null ? cam.orthographicSize : 5f;
            float halfW = halfH * (cam != null ? cam.aspect : 1.78f);
            var camPos = mainCamera.transform.position;
            var anchor = new Vector3(camPos.x, camPos.y, -1f);

            _leftCombatLight = GameObject.Find("CombatLightLeft");
            _rightCombatLight = GameObject.Find("CombatLightRight");

            SetLightEnabled(_leftCombatLight, true);
            SetLightEnabled(_rightCombatLight, true);

            if (_leftCombatLight != null)
            {
                _leftCombatLight.transform.position = new Vector3(
                    anchor.x - halfW * 0.88f,
                    anchor.y + halfH * 0.15f,
                    -1f
                );

                _leftCombatLight.SetActive(true);
            }

            if (_rightCombatLight != null)
            {
                _rightCombatLight.transform.position = new Vector3(
                    anchor.x + halfW * 0.88f,
                    anchor.y + halfH * 0.15f,
                    -1f
                );

                _rightCombatLight.SetActive(true);
            }


            RaiseBackground(cam, halfW, halfH);

            var heroes = party.Heroes.Where(h => h != null && h.IsAlive).Cast<ICombatUnit>().ToList();
            var enemies = room.Enemies.Where(e => e != null && e.IsAlive).Cast<ICombatUnit>().ToList();

            float centerY = anchor.y + halfH * 0.15f;
            _heroColumnX = anchor.x - halfW * 0.55f;
            _centerY = centerY;
            var heroSlots = BuildColumn(_heroColumnX, centerY, heroes.Count, halfH);

            // Enemies rank up FF-style: one column up to three, front 2 / back 3 beyond that, and a
            // boss alone at the back with its escort in front (see EnemyFormation).
            int bossIndex = enemies.FindIndex(u => u is Enemy e && e.IsBoss);
            float bossHalfWidth = bossIndex >= 0
                ? StageHalfWidth(enemies[bossIndex])
                : EnemyFormation.DefaultBossHalfWidth;
            var enemySlots = EnemyFormation.Layout(enemies.Count, bossIndex, halfW, halfH, bossHalfWidth);

            party.HidePartyForCombat();
            for (int i = 0; i < heroes.Count; i++)
            {
                PlaceUnit(heroes[i], heroSlots[i], faceRight: true, isHero: true, scale: 1.5f, sortingOrder: UnitSortOrder);
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var slot = enemySlots[i];
                var position = new Vector3(anchor.x + slot.Offset.x, centerY + slot.Offset.y, -1f);
                // A nearer rank draws over the one behind it, where a large boss overlaps its escort.
                PlaceUnit(enemies[i], position, faceRight: false, isHero: false,
                    scale: EnemyStageScale(enemies[i]), sortingOrder: UnitSortOrder + MaxRanks - slot.Rank);
            }
        }

        /// <summary>Enemies double their out-of-combat scale on the stage, times their CombatScale.</summary>
        private static float EnemyStageScale(ICombatUnit unit)
        {
            float combatScale = unit is Enemy enemy && enemy.Definition != null
                ? enemy.Definition.CombatScale
                : 1f;
            return 2f * combatScale;
        }

        /// <summary>Half the width an enemy will have once placed — read before it is rescaled.</summary>
        private static float StageHalfWidth(ICombatUnit unit)
        {
            var sr = unit.Transform != null ? unit.Transform.GetComponent<SpriteRenderer>() : null;
            if (sr == null || sr.sprite == null)
            {
                return EnemyFormation.DefaultBossHalfWidth;
            }
            return sr.bounds.extents.x * EnemyStageScale(unit);
        }

        /// <summary>
        /// Tears the stage down: restores unit sorting/facing, lowers the background, unfreezes
        /// the camera, and returns heroes to the party. Enemy positions are only restored when
        /// <paramref name="restoreEnemyPositions"/> is true (a defensive hook — today flee is
        /// resolved before the stage is ever raised, and victory destroys the enemies).
        /// </summary>
        public void End(bool restoreEnemyPositions)
        {
            RestoreParty();

            foreach (var rec in _restores)
            {
                if (rec.Sr == null)
                {
                    continue; // destroyed (e.g. an enemy killed during combat)
                }
                rec.Sr.sortingOrder = rec.OrigSortingOrder;
                rec.Sr.flipX = rec.OrigFlipX;
                rec.Sr.transform.localScale = rec.OrigScale;
                if (!rec.IsHero && restoreEnemyPositions)
                {
                    rec.Sr.transform.position = rec.OrigPos;
                }
            }
            _restores.Clear();

            if (_backgroundGo != null)
            {
                _backgroundGo.SetActive(false);
            }
            if (MainCamera.HasInstance)
            {
                MainCamera.Instance.AllowManualPan = true;
            }
            if (GameManager.HasInstance)
            {
                GameManager.Instance.SetCameraFollow(true);
            }
            if (_party != null)
            {
                _party.RestoreAfterCombat();

                if (_partyLight != null)
                {
                    _partyLight.enabled = true;
                }
            }
            SetLightEnabled(_leftCombatLight, false);
            SetLightEnabled(_rightCombatLight, false);
        }

        /// <summary>
        /// Puts a party-replacing summon where the hero column stands, facing the enemies. The party
        /// is off the stage entirely while it fights (<see cref="HideParty"/>) - the turn order and the
        /// party window still say who is waiting to come back.
        /// </summary>
        public void PlaceSummon(SummonUnit summon)
        {
            if (summon == null)
            {
                return;
            }
            var tr = summon.transform;
            tr.position = new Vector3(_heroColumnX, _centerY, -1f);
            tr.localScale = Vector3.one * SummonScale;
            var sr = summon.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = UnitSortOrder + MaxRanks + 1;
                // Art faces right, toward the enemies.
                sr.flipX = summon.Summon != null && summon.Summon.Facing == Cards.SummonFacing.Party;
            }
        }

        /// <summary>
        /// Takes every hero off the stage while a summon fights: sprite and HP bar both hidden.
        /// A hero who was already down stays hidden on return, because only the ones this call
        /// actually hid are brought back.
        /// </summary>
        public void HideParty()
        {
            if (_party == null)
            {
                return;
            }
            foreach (var hero in _party.Heroes)
            {
                var sr = hero != null ? hero.GetComponent<SpriteRenderer>() : null;
                if (sr != null && sr.enabled && !_hidden.Contains(sr))
                {
                    sr.enabled = false;
                    _hidden.Add(sr);
                }
                var bar = hero != null ? hero.GetComponent<UnitHealthBar>() : null;
                if (bar != null)
                {
                    bar.Hidden = true;
                }
            }
        }

        /// <summary>The party back on the stage. Safe to call when nothing is hidden.</summary>
        public void RestoreParty()
        {
            foreach (var sr in _hidden)
            {
                if (sr != null)
                {
                    sr.enabled = true;
                }
            }
            _hidden.Clear();
            if (_party != null)
            {
                foreach (var hero in _party.Heroes)
                {
                    var bar = hero != null ? hero.GetComponent<UnitHealthBar>() : null;
                    if (bar != null)
                    {
                        bar.Hidden = false;
                    }
                }
            }
        }

        private static void SetLightEnabled(GameObject lightObject, bool enabled)
        {
            if (lightObject == null)
                return;

            foreach (var component in lightObject.GetComponents<Behaviour>())
            {
                if (component.GetType().Name == "Light2D")
                {
                    component.enabled = enabled;
                    return;
                }
            }
        }

        /// <summary>Evenly spaced vertical slots centred on <paramref name="centerY"/>, top-first.</summary>
        private static List<Vector3> BuildColumn(float x, float centerY, int count, float halfH)
        {
            var slots = new List<Vector3>();
            foreach (float y in EnemyFormation.ColumnYs(count, halfH))
            {
                slots.Add(new Vector3(x, centerY + y, -1f));
            }
            return slots;
        }

        /// <param name="scale">Multiplier on the unit's out-of-combat scale.</param>
        private void PlaceUnit(ICombatUnit unit, Vector3 slot, bool faceRight, bool isHero, float scale, int sortingOrder)
        {
            var tr = unit.Transform;
            var sr = tr.GetComponent<SpriteRenderer>();

            var rec = new UnitRestore
            {
                IsHero = isHero,
                Sr = sr,
                OrigPos = tr.position,
                OrigScale = tr.localScale
            };

            if (sr != null)
            {
                rec.OrigSortingOrder = sr.sortingOrder;
                rec.OrigFlipX = sr.flipX;
            }

            _restores.Add(rec);

            tr.position = slot;

            // Make unit larger during combat
            tr.localScale = rec.OrigScale * scale;

            if (sr != null)
            {
                if (isHero)
                {
                    sr.enabled = true;
                }

                sr.sortingOrder = sortingOrder;
                sr.flipX = !faceRight;
            }
        }

        private void RaiseBackground(Camera cam, float halfW, float halfH)
        {
            // Precedence: the current level's per-level backdrop, then the inspector override, then
            // the default Resources battle backdrop; a solid fill is the last-resort fallback so the
            // dungeon is always hidden. (Qualify UnityEngine.Resources — the game has its own
            // Assets.Scripts.Resources namespace.)
            Sprite levelArt = Assets.Scripts.Dungeon.DungeonManager.HasInstance
                ? Assets.Scripts.Dungeon.DungeonManager.Instance.CurrentLevel?.CombatBackground
                : null;
            var art = levelArt != null
                ? levelArt
                : (_backgroundArt != null
                    ? _backgroundArt
                    : UnityEngine.Resources.Load<Sprite>(BackgroundResourcePath));

            if (_backgroundGo == null)
            {
                _backgroundGo = new GameObject("BattleBackground");
                _backgroundSr = _backgroundGo.AddComponent<SpriteRenderer>();
                _backgroundSr.sortingOrder = BackgroundSortOrder;
            }

            var material =
                UnityEngine.Resources.Load<Material>(
                    "Lit-Material"
                );

            if (material != null)
            {
                _backgroundSr.material = material;
            }

            _backgroundSr.sprite = art != null ? art : SolidSprite();
            _backgroundSr.color = art != null ? Color.white : new Color(0.10f, 0.09f, 0.16f);

            // Parent to the camera so a screen shake never exposes an edge, and cover the view.
            var camTransform = cam != null ? cam.transform : MainCamera.Instance.transform;
            _backgroundGo.transform.SetParent(camTransform, false);
            _backgroundGo.transform.localPosition = new Vector3(0f, 0f, 10f);
            _backgroundGo.transform.localRotation = Quaternion.identity;

            float coverW = halfW * 2f + 2f;
            float coverH = halfH * 2f + 2f;
            if (art != null)
            {
                // Uniform cover-fit so real art keeps its aspect (crops overflow, no stretch).
                var size = art.bounds.size;
                float scale = Mathf.Max(coverW / Mathf.Max(0.01f, size.x), coverH / Mathf.Max(0.01f, size.y));
                _backgroundGo.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                _backgroundGo.transform.localScale = new Vector3(coverW, coverH, 1f);
            }
            _backgroundGo.SetActive(true);
        }

        private static Sprite SolidSprite()
        {
            if (_solidSprite != null)
            {
                return _solidSprite;
            }
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _solidSprite;
        }
    }
}
