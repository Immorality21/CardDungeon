using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// Custom layout group that arranges children horizontally with overlap,
    /// like a hand of cards. Auto-adjusts overlap based on card count vs available width.
    /// </summary>
    public class CardHandLayout : LayoutGroup
    {
        [SerializeField]
        private float _cardWidth = 90f;

        [SerializeField]
        private float _cardHeight = 0f;

        [SerializeField]
        private float _minVisibleWidth = 30f;

        [SerializeField]
        private float _maxSpacing = 10f;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            int count = GetActiveChildCount();
            if (count == 0)
            {
                SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1, 0);
                return;
            }

            float visiblePer = GetVisibleWidthPerCard(count);
            float totalWidth = _cardWidth + (count - 1) * visiblePer + padding.horizontal;
            SetLayoutInputForAxis(totalWidth, totalWidth, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float height = rectTransform.rect.height;
            SetLayoutInputForAxis(height, height, -1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            int count = GetActiveChildCount();
            if (count == 0)
            {
                return;
            }

            float visiblePer = GetVisibleWidthPerCard(count);
            float totalWidth = _cardWidth + (count - 1) * visiblePer;
            float availableWidth = rectTransform.rect.width - padding.horizontal;

            // Center the hand if it fits
            float startX = padding.left;
            if (totalWidth < availableWidth)
            {
                startX += (availableWidth - totalWidth) * 0.5f;
            }

            int index = 0;
            for (int i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                float x = startX + index * visiblePer;
                SetChildAlongAxis(child, 0, x, _cardWidth);
                index++;
            }
        }

        public override void SetLayoutVertical()
        {
            float availableHeight = rectTransform.rect.height - padding.vertical;
            float cardH = _cardHeight > 0 ? _cardHeight : availableHeight;
            float startY = padding.top;

            if (_cardHeight > 0 && cardH < availableHeight)
            {
                startY += (availableHeight - cardH) * 0.5f;
            }

            for (int i = 0; i < rectChildren.Count; i++)
            {
                SetChildAlongAxis(rectChildren[i], 1, startY, cardH);
            }
        }

        private float GetVisibleWidthPerCard(int count)
        {
            if (count <= 1)
            {
                return _cardWidth;
            }

            float availableWidth = rectTransform.rect.width - padding.horizontal;
            float idealSpacing = _cardWidth + _maxSpacing;

            // How much space each card gets (visible portion) when spread apart
            float spacedPer = idealSpacing;

            // How much space is needed if all cards are at max spacing
            float totalSpaced = _cardWidth + (count - 1) * spacedPer;

            if (totalSpaced <= availableWidth)
            {
                // Plenty of room — use max spacing (no overlap)
                return spacedPer;
            }

            // Need to overlap. Calculate how much visible width per card to fit.
            float visiblePer = (availableWidth - _cardWidth) / (count - 1);

            // Clamp to minimum visible width
            if (visiblePer < _minVisibleWidth)
            {
                visiblePer = _minVisibleWidth;
            }

            return visiblePer;
        }

        private int GetActiveChildCount()
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).gameObject.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
