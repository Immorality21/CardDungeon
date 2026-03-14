using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Items.UI
{
    public class InventoryEntryUI : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _label;

        public Button Button => _button;

        public void SetLabel(string text)
        {
            _label.text = text;
        }

        public void SetLabelColor(Color color)
        {
            _label.color = color;
        }

        public void SetBackgroundColor(Color color)
        {
            _background.color = color;
        }
    }
}
