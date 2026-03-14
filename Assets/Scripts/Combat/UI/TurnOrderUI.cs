using System.Collections.Generic;
using Assets.Scripts.Rooms;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Combat.UI
{
    public class TurnOrderUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Transform _slotParent;
        [SerializeField] private Image _slotPrefab;

        private List<Image> _slots = new List<Image>();

        private void OnEnable()
        {
            CombatManager.Instance.OnTurnOrderChanged += Refresh;
            CombatManager.Instance.OnCombatStarted += Show;
            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
        }

        private void OnDisable()
        {
            if (CombatManager.HasInstance)
            {
                CombatManager.Instance.OnTurnOrderChanged -= Refresh;
                CombatManager.Instance.OnCombatStarted -= Show;
                CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            }
        }

        private void Start()
        {
            Hide();
        }

        private void Show()
        {
            _root.SetActive(true);
        }

        private void Hide()
        {
            _root.SetActive(false);
        }

        private void OnCombatEnded(CombatResult result)
        {
            Hide();
        }

        private void Refresh(List<ICombatUnit> turnOrder)
        {
            // Ensure we have enough slot instances
            while (_slots.Count < turnOrder.Count)
            {
                var slot = Instantiate(_slotPrefab, _slotParent);
                _slots.Add(slot);
            }

            // Update each slot
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < turnOrder.Count)
                {
                    _slots[i].gameObject.SetActive(true);
                    _slots[i].sprite = turnOrder[i].Icon;
                    _slots[i].color = turnOrder[i].Icon != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                }
                else
                {
                    _slots[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
