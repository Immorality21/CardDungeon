using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// A sprite-based HP bar that sits above a combat unit. Attached to the unit's GameObject
    /// (so it follows and is destroyed with it), built lazily from procedurally-generated
    /// white sprites — no art assets. Visible only during combat and while the unit is alive.
    /// </summary>
    public class UnitHealthBar : MonoBehaviour
    {
        private const float Width = 0.85f;
        private const float Height = 0.12f;
        private const int BgSortOrder = 900;

        private static Sprite _centerSprite;
        private static Sprite _leftSprite;

        private ICombatUnit _unit;
        private Transform _barRoot;
        private SpriteRenderer _fill;
        private bool _built;

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

            var unitSprite = GetComponent<SpriteRenderer>();
            float topY = unitSprite != null ? unitSprite.bounds.extents.y + 0.18f : 0.6f;

            _barRoot = new GameObject("HealthBar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = new Vector3(0f, topY, -1f);

            var bg = MakeRenderer(_barRoot, CenterSprite(), new Color(0.08f, 0.08f, 0.10f, 0.85f), BgSortOrder);
            bg.transform.localScale = new Vector3(Width, Height, 1f);

            var fillAnchor = new GameObject("Fill").transform;
            fillAnchor.SetParent(_barRoot, false);
            fillAnchor.localPosition = new Vector3(-Width * 0.5f, 0f, -0.01f);
            _fill = MakeRenderer(fillAnchor, LeftSprite(), Color.green, BgSortOrder + 1);
            _fill.transform.localScale = new Vector3(Width, Height * 0.72f, 1f);

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

            float max = Mathf.Max(1, _unit.Stats.MaxHealth);
            float ratio = Mathf.Clamp01(_unit.Stats.Health / max);
            _fill.transform.localScale = new Vector3(Width * ratio, Height * 0.72f, 1f);
            _fill.color = ratio > 0.5f
                ? Color.Lerp(new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.85f, 0.3f), (ratio - 0.5f) * 2f)
                : Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.95f, 0.8f, 0.2f), ratio * 2f);
        }

        private static SpriteRenderer MakeRenderer(Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject("bar");
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
