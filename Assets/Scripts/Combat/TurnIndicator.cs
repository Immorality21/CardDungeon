using Assets.Scripts.Rooms;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// A bobbing arrow that floats above the unit whose turn it is, so the battlefield itself
    /// says "you're up" (the turn-order list only shows it top-right). Auto-creates on first use
    /// (no scene wiring), mirroring <see cref="CombatFeedback"/>. Follows the active unit each
    /// frame and hides itself outside combat or once the unit dies.
    /// </summary>
    public class TurnIndicator : SingletonBehaviour<TurnIndicator>
    {
        private const int SortOrder = 950; // above HP bars (900), below floating text (1000)

        private ICombatUnit _unit;
        private SpriteRenderer _sr;
        private float _t;

        /// <summary>Point the marker at the unit taking its turn.</summary>
        public void SetTarget(ICombatUnit unit)
        {
            EnsureSprite();
            _unit = unit;
            _t = 0f;
        }

        /// <summary>Hide the marker (combat ended / between turns with no owner).</summary>
        public void Clear()
        {
            _unit = null;
            if (_sr != null)
            {
                _sr.enabled = false;
            }
        }

        private void EnsureSprite()
        {
            if (_sr != null)
            {
                return;
            }
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = CombatIcons.Get("arrow");
            _sr.color = new Color(1f, 0.85f, 0.25f);
            _sr.flipY = true; // the arrow glyph points up by default; flip it to point down
            _sr.sortingOrder = SortOrder;
            transform.localScale = Vector3.one * 0.5f;
        }

        private void LateUpdate()
        {
            bool active = _unit != null && _unit.IsAlive && _unit.Transform != null
                && CombatManager.HasInstance && CombatManager.Instance.InCombat;

            if (!active)
            {
                if (_sr != null && _sr.enabled)
                {
                    _sr.enabled = false;
                }
                return;
            }

            EnsureSprite();
            _sr.enabled = _sr.sprite != null;

            _t += Time.deltaTime;
            float bob = Mathf.Sin(_t * 6f) * 0.07f;

            var unitSr = _unit.Transform.GetComponent<SpriteRenderer>();
            float top = unitSr != null ? unitSr.bounds.max.y : _unit.Transform.position.y + 0.5f;
            var p = _unit.Transform.position;
            transform.position = new Vector3(p.x, top + 0.9f + bob, -2f);
        }
    }
}
