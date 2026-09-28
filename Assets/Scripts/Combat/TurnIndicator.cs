using Assets.Scripts.Rooms;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// A bobbing arrow beside the unit whose turn it is, so the battlefield itself says "you're up"
    /// (the turn-order list only shows it top-right). Auto-creates on first use (no scene wiring),
    /// mirroring <see cref="CombatFeedback"/>. Follows the active unit each frame and hides itself
    /// outside combat or once the unit dies.
    ///
    /// <para>It sits on the unit's <b>outer side</b> - left of a hero, right of an enemy - pointing
    /// in at its middle. It used to float above the sprite, and in a column of heroes "above this
    /// one" is also "below the one above", right on its feet and HP bar: with two heroes it read as
    /// pointing at either (playtest 2026-09-28). Nothing stands on the outer side of a column, so
    /// the arrow there can only mean one unit.</para>
    /// </summary>
    public class TurnIndicator : SingletonBehaviour<TurnIndicator>
    {
        private const int SortOrder = 950; // above HP bars (900), below floating text (1000)

        /// <summary>World units between the unit's sprite edge and the arrow's centre.</summary>
        private const float Gap = 0.35f;

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

            // Heroes stand in the left column, enemies in the right: the outer side is away from
            // the other team. The glyph points up, so a quarter turn aims it inward.
            float outward = _unit.IsHero ? -1f : 1f;
            var p = _unit.Transform.position;
            var unitSr = _unit.Transform.GetComponent<SpriteRenderer>();
            var bounds = unitSr != null ? unitSr.bounds : new Bounds(p, Vector3.one);
            float edge = _unit.IsHero ? bounds.min.x : bounds.max.x;

            transform.rotation = Quaternion.Euler(0f, 0f, _unit.IsHero ? -90f : 90f);
            transform.position = new Vector3(edge + outward * (Gap + bob), bounds.center.y, -2f);
        }
    }
}
