using System.Collections;
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

        // The stage's frame, kept from Begin so a summoned ally can be placed in the vanguard.
        private float _anchorX;
        private float _halfW;
        private float _halfH;

        /// <summary>How far above the camera's centre the formation is centred, as a share of the half-height.</summary>
        private const float StageCenterLift = 0.26f;

        // Hero sprites hidden while a summon has taken the party's place, shown again when it leaves.
        private readonly List<SpriteRenderer> _hidden = new List<SpriteRenderer>();
        private readonly List<UnitHealthBar> _hiddenBars = new List<UnitHealthBar>();

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

            // Above the middle, because the bottom of the screen belongs to the UI: the command menu
            // and the ability pickers dock bottom-left, right under the hero column, and the party
            // window bottom-right. At 0.15 the lowest hero stood inside that band and a picker covered
            // the very hero whose turn it was (playtest 2026-09-28, raised with the UI scale).
            float centerY = anchor.y + halfH * StageCenterLift;
            // Where a party-replacing summon stands: the single column's spot, whatever the party's shape.
            _heroColumnX = anchor.x + halfW * HeroFormation.SingleColumnX;
            _centerY = centerY;
            _anchorX = anchor.x;
            _halfW = halfW;
            _halfH = halfH;
            var heroSlots = new List<Vector3>(heroes.Count);
            foreach (var offset in HeroFormation.Layout(heroes.Count, halfW, halfH))
            {
                heroSlots.Add(new Vector3(anchor.x + offset.x, centerY + offset.y, -1f));
            }

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
        /// Stands a squad (the Demon Army) where the party stood - the hero formation, one troop per
        /// hero slot, at a hero's size (troops are hero-sized art). The party is off the stage while
        /// it fights, exactly as for a single replacement.
        /// </summary>
        public void PlaceSquad(IList<SummonUnit> troops)
        {
            if (troops == null)
            {
                return;
            }
            var slots = HeroFormation.Layout(troops.Count, _halfW, _halfH);
            for (int i = 0; i < troops.Count; i++)
            {
                var troop = troops[i];
                if (troop == null)
                {
                    continue;
                }
                troop.transform.position = new Vector3(_anchorX + slots[i].x, _centerY + slots[i].y, -1f);
                troop.transform.localScale = Vector3.one * CombatUnitScale;
                var sr = troop.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = UnitSortOrder + MaxRanks + 1;
                    sr.flipX = troop.Summon != null && troop.Summon.Facing == Cards.SummonFacing.Party;
                }
            }
        }

        /// <summary>
        /// Stands the summoned allies (<c>SummonKind.JoinParty</c>) in the vanguard column in front
        /// of the party (<see cref="HeroFormation.AllyLayout"/>), facing the enemies at a hero's
        /// size. Re-lays out every ally given, so call it with the whole living set whenever one
        /// joins or leaves: the ones already standing glide to their new spots, and
        /// <paramref name="arriving"/> is set down at once - <c>SummonPresenter.Arrive</c> reads its
        /// spot as where to stride to.
        /// </summary>
        public void PlaceAllies(IList<SummonUnit> allies, SummonUnit arriving = null)
        {
            if (allies == null)
            {
                return;
            }
            var slots = HeroFormation.AllyLayout(allies.Count, _halfW, _halfH);
            for (int i = 0; i < allies.Count; i++)
            {
                var ally = allies[i];
                if (ally == null)
                {
                    continue;
                }
                var tr = ally.transform;
                var spot = new Vector3(_anchorX + slots[i].x, _centerY + slots[i].y, -1f);
                if (ReferenceEquals(ally, arriving))
                {
                    tr.position = spot;
                    tr.localScale = Vector3.one * CombatUnitScale;
                }
                else if ((tr.position - spot).sqrMagnitude > 0.0001f)
                {
                    StartCoroutine(Glide(tr, spot));
                }
                var sr = ally.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    // In front of the hero ranks, so an ally that overlaps a hero's edge reads as nearer.
                    sr.sortingOrder = UnitSortOrder + MaxRanks + 1;
                    sr.flipX = ally.Summon != null && ally.Summon.Facing == Cards.SummonFacing.Party;
                }
            }
        }

        private const float AllyGlideTime = 0.3f;

        /// <summary>An ally stepping to its new place in the column, eased so the shuffle reads as a
        /// step rather than a jump.</summary>
        private static IEnumerator Glide(Transform tr, Vector3 to)
        {
            Vector3 from = tr.position;
            float t = 0f;
            while (t < AllyGlideTime && tr != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / AllyGlideTime);
                tr.position = Vector3.Lerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }
            if (tr != null)
            {
                tr.position = to;
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

        /// <summary>
        /// Takes other hero-side units off the stage with the party - a Sacrifice horror stands in a
        /// hero's place, so it steps out with them while a party-replacing summon fights.
        /// <see cref="RestoreParty"/> brings them back.
        /// </summary>
        public void HideUnits(IEnumerable<Component> units)
        {
            if (units == null)
            {
                return;
            }
            foreach (var unit in units)
            {
                var sr = unit != null ? unit.GetComponent<SpriteRenderer>() : null;
                if (sr != null && sr.enabled && !_hidden.Contains(sr))
                {
                    sr.enabled = false;
                    _hidden.Add(sr);
                }
                var bar = unit != null ? unit.GetComponent<UnitHealthBar>() : null;
                if (bar != null)
                {
                    bar.Hidden = true;
                    _hiddenBars.Add(bar);
                }
            }
        }

        /// <summary>Stands <paramref name="unit"/> on <paramref name="spot"/> at a hero's size, facing the
        /// enemies - a Sacrifice horror rising where its hero stood.</summary>
        public void PlaceAt(SummonUnit unit, Vector3 spot)
        {
            if (unit == null)
            {
                return;
            }
            unit.transform.position = spot;
            unit.transform.localScale = Vector3.one * CombatUnitScale;
            var sr = unit.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = UnitSortOrder + MaxRanks + 1;
                sr.flipX = unit.Summon != null && unit.Summon.Facing == Cards.SummonFacing.Party;
            }
        }

        /// <summary>The party back on the stage. Safe to call when nothing is hidden.</summary>
        public void RestoreParty()
        {
            foreach (var bar in _hiddenBars)
            {
                if (bar != null)
                {
                    bar.Hidden = false;
                }
            }
            _hiddenBars.Clear();
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
