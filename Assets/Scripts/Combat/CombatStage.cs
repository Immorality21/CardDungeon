using System.Collections.Generic;
using System.Linq;
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
            public bool IsHero;
        }

        private readonly List<UnitRestore> _restores = new List<UnitRestore>();
        private GameObject _backgroundGo;
        private SpriteRenderer _backgroundSr;
        private Party _party;

        /// <summary>
        /// Freezes the camera, raises the background, and forms alive heroes (left) and enemies
        /// (right) into columns centred on the current view. Call once when combat starts,
        /// before <c>EnsureHealthBars</c> so the bars anchor at the battle positions.
        /// </summary>
        public void Begin(Party party, Room room)
        {
            _party = party;
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

            RaiseBackground(cam, halfW, halfH);

            var heroes = party.Heroes.Where(h => h != null && h.IsAlive).Cast<ICombatUnit>().ToList();
            var enemies = room.Enemies.Where(e => e != null && e.IsAlive).Cast<ICombatUnit>().ToList();

            float centerY = anchor.y + halfH * 0.15f;
            var heroSlots = BuildColumn(anchor.x - halfW * 0.55f, centerY, heroes.Count, halfH);
            var enemySlots = BuildColumn(anchor.x + halfW * 0.55f, centerY, enemies.Count, halfH);

            party.HidePartyForCombat();
            for (int i = 0; i < heroes.Count; i++)
            {
                PlaceUnit(heroes[i], heroSlots[i], faceRight: true, isHero: true);
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                PlaceUnit(enemies[i], enemySlots[i], faceRight: false, isHero: false);
            }
        }

        /// <summary>
        /// Tears the stage down: restores unit sorting/facing, lowers the background, unfreezes
        /// the camera, and returns heroes to the party. Enemy positions are only restored when
        /// <paramref name="restoreEnemyPositions"/> is true (a defensive hook — today flee is
        /// resolved before the stage is ever raised, and victory destroys the enemies).
        /// </summary>
        public void End(bool restoreEnemyPositions)
        {
            foreach (var rec in _restores)
            {
                if (rec.Sr == null)
                {
                    continue; // destroyed (e.g. an enemy killed during combat)
                }
                rec.Sr.sortingOrder = rec.OrigSortingOrder;
                rec.Sr.flipX = rec.OrigFlipX;
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
            }
        }

        /// <summary>Evenly spaced vertical slots centred on <paramref name="centerY"/>, top-first.</summary>
        private static List<Vector3> BuildColumn(float x, float centerY, int count, float halfH)
        {
            var slots = new List<Vector3>();
            if (count <= 0)
            {
                return slots;
            }
            float spacing = Mathf.Min(halfH * 0.5f, (halfH * 1.3f) / count);
            float topOffset = (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                float y = centerY + (topOffset - i) * spacing;
                slots.Add(new Vector3(x, y, -1f));
            }
            return slots;
        }

        private void PlaceUnit(ICombatUnit unit, Vector3 slot, bool faceRight, bool isHero)
        {
            var tr = unit.Transform;
            var sr = tr.GetComponent<SpriteRenderer>();

            var rec = new UnitRestore { IsHero = isHero, Sr = sr, OrigPos = tr.position };
            if (sr != null)
            {
                rec.OrigSortingOrder = sr.sortingOrder;
                rec.OrigFlipX = sr.flipX;
            }
            _restores.Add(rec);

            tr.position = slot;
            if (sr != null)
            {
                if (isHero)
                {
                    sr.enabled = true;
                }
                sr.sortingOrder = UnitSortOrder;
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
