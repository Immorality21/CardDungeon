using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// Hearthstone-style hover: spawns an enlarged preview clone above the hand.
    /// The original card stays in place untouched. Preview is destroyed on pointer exit.
    /// </summary>
    public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private float _previewScale = 1.8f;

        [SerializeField]
        private float _previewOffsetY = 80f;

        private GameObject _preview;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_preview != null)
            {
                return;
            }

            // Find the root canvas to parent the preview at the top level
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var rootCanvas = canvas.rootCanvas;

            // Clone this card
            _preview = Instantiate(gameObject, rootCanvas.transform);
            _preview.name = "CardPreview";

            // Remove hover effect from clone to prevent recursion
            var cloneHover = _preview.GetComponent<CardHoverEffect>();
            if (cloneHover != null)
            {
                Destroy(cloneHover);
            }

            // Remove button so clicking the preview doesn't trigger assign/unassign
            var cloneBtn = _preview.GetComponent<Button>();
            if (cloneBtn != null)
            {
                Destroy(cloneBtn);
            }

            // Disable raycasts on preview so pointer exit on original still fires
            var cloneGraphics = _preview.GetComponentsInChildren<Graphic>();
            foreach (var g in cloneGraphics)
            {
                g.raycastTarget = false;
            }

            // Position above the original card
            var rt = GetComponent<RectTransform>();
            var previewRT = _preview.GetComponent<RectTransform>();

            // Capture the original's actual rendered size (layout-driven)
            var originalWidth = rt.rect.width;
            var originalHeight = rt.rect.height;

            // Reset anchors to non-stretch so sizeDelta controls the size
            previewRT.anchorMin = new Vector2(0.5f, 0.5f);
            previewRT.anchorMax = new Vector2(0.5f, 0.5f);
            previewRT.pivot = new Vector2(0.5f, 0.5f);
            previewRT.sizeDelta = new Vector2(originalWidth, originalHeight);

            // Convert card center to root canvas space
            var worldCorners = new Vector3[4];
            rt.GetWorldCorners(worldCorners);
            var worldCenter = (worldCorners[0] + worldCorners[2]) * 0.5f;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(),
                RectTransformUtility.WorldToScreenPoint(null, worldCenter),
                rootCanvas.worldCamera,
                out localPoint);

            previewRT.anchoredPosition = localPoint + new Vector2(0, _previewOffsetY);
            previewRT.localScale = Vector3.one * _previewScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            DestroyPreview();
        }

        private void OnDisable()
        {
            DestroyPreview();
        }

        private void DestroyPreview()
        {
            if (_preview != null)
            {
                Destroy(_preview);
                _preview = null;
            }
        }
    }
}
