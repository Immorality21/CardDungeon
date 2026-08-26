using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Cards;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Above-unit combat readout: a procedurally-drawn HP bar, a row of status/buff icons
    /// (Attack/Defense up-down, Frozen, Haste, Slow) below it, and — for enemies — a
    /// predicted next-action "intent" icon above it. Attached to the unit's GameObject; needs
    /// no art assets (bars are 1px sprites; glyphs are tinted white icons from Resources).
    /// </summary>
    public class UnitHealthBar : MonoBehaviour
    {
        private const float Width = 0.85f;
        private const float Height = 0.12f;
        private const float BossBarScale = 1.7f;   // bosses get a wider, taller bar
        private const int BgSortOrder = 900;
        private const float IconSize = 0.34f;
        private const float IconGap = 0.30f;
        private const float RefreshInterval = 0.2f;

        private static readonly Color Green = new Color(0.35f, 0.9f, 0.35f);
        private static readonly Color Red = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color Cyan = new Color(0.55f, 0.85f, 1f);
        private static readonly Color Yellow = new Color(0.98f, 0.85f, 0.25f);
        private static readonly Color SlowBlue = new Color(0.55f, 0.6f, 0.85f);
        private static readonly Color IntentRed = new Color(0.95f, 0.4f, 0.35f);
        private static readonly Color Orange = new Color(1f, 0.6f, 0.15f);
        private static readonly Color Purple = new Color(0.78f, 0.45f, 0.95f);

        private static Sprite _centerSprite;
        private static Sprite _leftSprite;

        private ICombatUnit _unit;
        private Enemy _enemy;
        private Transform _barRoot;
        private SpriteRenderer _fill;
        private Transform _statusRoot;
        private SpriteRenderer _intent;
        private float _barWidth = Width;
        private float _barHeight = Height;
        private readonly List<GameObject> _statusIcons = new List<GameObject>();
        private string _statusSig;
        private string _intentSig;
        private float _refreshTimer;
        private bool _built;

        private struct IconDesc
        {
            public string Name;
            public Color Tint;
            public bool FlipX;
            public bool FlipY;
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _unit = GetComponent<ICombatUnit>();
            if (_unit == null || _unit.Stats == null)
            {
                return;
            }
            _enemy = GetComponent<Enemy>();

            bool isBoss = _enemy != null && _enemy.IsBoss;
            _barWidth = isBoss ? Width * BossBarScale : Width;
            _barHeight = isBoss ? Height * BossBarScale : Height;

            var unitSprite = GetComponent<SpriteRenderer>();
            float topY = unitSprite != null ? unitSprite.bounds.extents.y + 0.18f : 0.6f;

            _barRoot = new GameObject("HealthBar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = new Vector3(0f, topY, -1f);

            // Boss bars get a crimson backdrop so they read as the climax fight.
            Color bgColor = isBoss ? new Color(0.18f, 0.04f, 0.05f, 0.9f) : new Color(0.08f, 0.08f, 0.10f, 0.85f);
            var bg = MakeRenderer(_barRoot, CenterSprite(), bgColor, BgSortOrder);
            bg.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);

            var fillAnchor = new GameObject("Fill").transform;
            fillAnchor.SetParent(_barRoot, false);
            fillAnchor.localPosition = new Vector3(-_barWidth * 0.5f, 0f, -0.01f);
            _fill = MakeRenderer(fillAnchor, LeftSprite(), Color.green, BgSortOrder + 1);
            _fill.transform.localScale = new Vector3(_barWidth, _barHeight * 0.72f, 1f);

            _statusRoot = new GameObject("StatusIcons").transform;
            _statusRoot.SetParent(_barRoot, false);
            _statusRoot.localPosition = new Vector3(-0.6f, -0.22f, 0f);

            _intent = MakeRenderer(_barRoot, null, Color.white, BgSortOrder + 2);
            _intent.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            _intent.transform.localScale = new Vector3(IconSize, IconSize, 1f);
            _intent.enabled = false;

            _built = true;
        }

        private void LateUpdate()
        {
            bool inCombat = CombatManager.HasInstance && CombatManager.Instance.InCombat;
            if (!inCombat)
            {
                if (_barRoot != null && _barRoot.gameObject.activeSelf)
                {
                    _barRoot.gameObject.SetActive(false);
                }
                return;
            }

            EnsureBuilt();
            if (!_built)
            {
                return;
            }

            bool alive = _unit != null && _unit.IsAlive;
            if (_barRoot.gameObject.activeSelf != alive)
            {
                _barRoot.gameObject.SetActive(alive);
            }
            if (!alive)
            {
                return;
            }

            // HP fill — every frame (cheap).
            float max = Mathf.Max(1, _unit.GetEffectiveStat(StatType.MaxHealth));
            float ratio = Mathf.Clamp01(_unit.Stats.Health / max);
            _fill.transform.localScale = new Vector3(_barWidth * ratio, _barHeight * 0.72f, 1f);
            _fill.color = ratio > 0.5f
                ? Color.Lerp(Yellow, Green, (ratio - 0.5f) * 2f)
                : Color.Lerp(Red, Yellow, ratio * 2f);

            // Status + intent — throttled.
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = RefreshInterval;
                RefreshStatusIcons();
                RefreshIntent();
            }
        }

        private void RefreshStatusIcons()
        {
            var descs = BuildStatusDescriptors();
            string sig = Signature(descs);
            if (sig == _statusSig)
            {
                return;
            }
            _statusSig = sig;

            foreach (var go in _statusIcons)
            {
                Destroy(go);
            }
            _statusIcons.Clear();

            float startX = -(descs.Count - 1) * IconGap * 0.5f;
            for (int i = 0; i < descs.Count; i++)
            {
                var sr = MakeRenderer(_statusRoot, CombatIcons.Get(descs[i].Name), descs[i].Tint, BgSortOrder + 2);
                sr.flipX = descs[i].FlipX;
                sr.flipY = descs[i].FlipY;
                sr.transform.localPosition = new Vector3(startX + i * IconGap, 0f, 0f);
                sr.transform.localScale = new Vector3(IconSize, IconSize, 1f);
                _statusIcons.Add(sr.gameObject);
            }
        }

        private List<IconDesc> BuildStatusDescriptors()
        {
            var list = new List<IconDesc>();
            var bt = CombatManager.HasInstance ? CombatManager.Instance.BuffTracker : null;
            if (bt == null)
            {
                return list;
            }

            int atk = bt.GetBuffAmount(_unit, StatType.Strength);
            if (atk != 0)
            {
                list.Add(new IconDesc { Name = "sword", Tint = atk > 0 ? Green : Red });
            }
            int def = bt.GetBuffAmount(_unit, StatType.Endurance);
            if (def != 0)
            {
                list.Add(new IconDesc { Name = "shield", Tint = def > 0 ? Green : Red });
            }

            foreach (var status in bt.GetActiveStatusEffects(_unit))
            {
                switch (status)
                {
                    case BuffType.Frozen:
                        list.Add(new IconDesc { Name = "snowflake", Tint = Cyan });
                        break;
                    case BuffType.Haste:
                        list.Add(new IconDesc { Name = "chevrons", Tint = Yellow });
                        break;
                    case BuffType.Slow:
                        list.Add(new IconDesc { Name = "chevrons", Tint = SlowBlue, FlipX = true });
                        break;
                }
            }

            return list;
        }

        private void RefreshIntent()
        {
            if (_enemy == null)
            {
                return;
            }

            var intent = CombatManager.HasInstance ? CombatManager.Instance.PredictIntent(_enemy) : null;
            string sig = intent?.ToString() ?? "";
            if (sig == _intentSig)
            {
                return;
            }
            _intentSig = sig;

            if (!intent.HasValue)
            {
                _intent.enabled = false;
                return;
            }

            IconDesc d = IntentDescriptor(intent.Value);
            _intent.sprite = CombatIcons.Get(d.Name);
            _intent.color = d.Tint;
            _intent.flipX = d.FlipX;
            _intent.flipY = d.FlipY;
            _intent.enabled = _intent.sprite != null;
        }

        private IconDesc IntentDescriptor(EnemyActionType type)
        {
            switch (type)
            {
                case EnemyActionType.Heal:
                    return new IconDesc { Name = "cross", Tint = Green };
                case EnemyActionType.Debuff:
                    return new IconDesc { Name = "arrow", Tint = Purple, FlipY = true };
                case EnemyActionType.ChargeHeavy:
                case EnemyActionType.HeavyAttack:
                case EnemyActionType.ChargeAoe:
                case EnemyActionType.AoeAttack:
                    return new IconDesc { Name = "burst", Tint = Orange };
                default:
                    return new IconDesc { Name = "sword", Tint = IntentRed };
            }
        }

        private static string Signature(List<IconDesc> descs)
        {
            var sb = new StringBuilder();
            foreach (var d in descs)
            {
                sb.Append(d.Name).Append(ColorUtility.ToHtmlStringRGB(d.Tint)).Append(d.FlipX ? 'L' : 'r').Append('|');
            }
            return sb.ToString();
        }

        private static SpriteRenderer MakeRenderer(Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject("icon");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private static Sprite CenterSprite()
        {
            return _centerSprite != null ? _centerSprite : (_centerSprite = MakeWhite(new Vector2(0.5f, 0.5f)));
        }

        private static Sprite LeftSprite()
        {
            return _leftSprite != null ? _leftSprite : (_leftSprite = MakeWhite(new Vector2(0f, 0.5f)));
        }

        private static Sprite MakeWhite(Vector2 pivot)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), pivot, 1f);
        }
    }
}
