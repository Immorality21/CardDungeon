using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Gives combat units a subtle "breathing" idle so they aren't frozen statues — a small
    /// vertical scale pulse (units low on health breathe faster and harder, reading as strained).
    /// Uses <b>scale</b>, not position, so it never fights the position-based lunge / stage
    /// formation; and it yields the scale entirely once the unit dies so the death pop/fade owns
    /// it. Attached per-unit at combat start (alongside the HP bar) and self-manages visibility.
    /// </summary>
    public class CombatIdleMotion : MonoBehaviour
    {
        private ICombatUnit _unit;
        private Vector3 _baseScale;
        private bool _captured;
        private float _t;

        private void Awake()
        {
            _unit = GetComponent<ICombatUnit>();
        }

        private void OnDisable()
        {
            RestoreBase();
        }

        private void RestoreBase()
        {
            if (_captured)
            {
                transform.localScale = _baseScale;
                _captured = false;
            }
        }

        private void LateUpdate()
        {
            bool inCombat = CombatManager.HasInstance && CombatManager.Instance.InCombat;
            if (_unit == null || !inCombat)
            {
                RestoreBase();
                return;
            }

            if (!_unit.IsAlive)
            {
                // Dead — hand the scale to the death animation; don't restore or write.
                _captured = false;
                return;
            }

            if (!_captured)
            {
                _baseScale = transform.localScale;
                _captured = true;
                _t = Mathf.Repeat(transform.position.x * 3.1f, Mathf.PI * 2f); // desync units
            }

            _t += Time.deltaTime;

            float hpFrac = _unit.Stats != null && _unit.Stats.MaxHealth > 0
                ? (float)_unit.Stats.Health / _unit.Stats.MaxHealth
                : 1f;
            bool wounded = hpFrac <= 0.35f;

            float speed = wounded ? 8.5f : 2.4f;
            float amp = wounded ? 0.05f : 0.025f;
            float s = 1f + Mathf.Sin(_t * speed) * amp;
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * s, _baseScale.z);
        }
    }
}
