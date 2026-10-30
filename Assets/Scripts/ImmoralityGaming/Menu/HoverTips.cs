using UnityEngine;
using UnityEngine.UIElements;

namespace ImmoralityGaming.Menu
{
    /// <summary>
    /// Shows an element's <see cref="VisualElement.tooltip"/> in a small box beside it. Runtime UI
    /// Toolkit panels never draw tooltips themselves, so every <c>tooltip</c> the game set (stat
    /// cells, resistances) was invisible until this.
    ///
    /// <para>One per document root (<see cref="Attach"/>). The pointer drives it: hovering an element,
    /// or anything inside one, that has a tooltip shows it. A keyboard cursor drives it through
    /// <see cref="ShowFor"/> - the shared <see cref="KeyboardNavigator"/> calls it as its selection
    /// moves.</para>
    /// </summary>
    public sealed class HoverTips
    {
        public const string BoxClass = "cd-tip";
        private const float Gap = 6f;

        private readonly VisualElement _root;
        private readonly Label _box;
        private VisualElement _current;

        private HoverTips(VisualElement root)
        {
            _root = root;
            _box = new Label { pickingMode = PickingMode.Ignore };
            _box.AddToClassList(BoxClass);
            _box.style.position = Position.Absolute;
            _box.style.display = DisplayStyle.None;
            _root.Add(_box);
            _box.RegisterCallback<GeometryChangedEvent>(_ => Place());

            _root.RegisterCallback<PointerOverEvent>(OnPointerOver, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerLeaveEvent>(_ => Hide());
        }

        /// <summary>Starts showing tooltips for everything under <paramref name="root"/>.</summary>
        public static HoverTips Attach(VisualElement root)
        {
            return root == null ? null : new HoverTips(root);
        }

        /// <summary>Shows <paramref name="element"/>'s tooltip (or the nearest ancestor's), or hides
        /// the box when there is none.</summary>
        public void ShowFor(VisualElement element)
        {
            var owner = TooltipOwner(element);
            if (owner == null)
            {
                Hide();
                return;
            }
            _current = owner;
            _box.text = owner.tooltip;
            _box.style.display = DisplayStyle.Flex;
            _box.BringToFront();
            Place();
        }

        public void Hide()
        {
            _current = null;
            _box.style.display = DisplayStyle.None;
        }

        private void OnPointerOver(PointerOverEvent evt)
        {
            ShowFor(evt.target as VisualElement);
        }

        private VisualElement TooltipOwner(VisualElement element)
        {
            for (var e = element; e != null && e != _root; e = e.parent)
            {
                if (e == _box)
                {
                    return null;
                }
                if (!string.IsNullOrEmpty(e.tooltip) && e.resolvedStyle.display != DisplayStyle.None)
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>Under the element, or above it when there is no room below; kept inside the root.</summary>
        private void Place()
        {
            if (_current == null || _current.panel == null)
            {
                return;
            }
            Rect target = _root.WorldToLocal(_current.worldBound);
            Rect area = _root.contentRect;
            float width = _box.resolvedStyle.width;
            float height = _box.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height))
            {
                return;
            }

            float x = Mathf.Clamp(target.xMin, area.xMin, Mathf.Max(area.xMin, area.xMax - width));
            float y = target.yMax + Gap;
            if (y + height > area.yMax)
            {
                y = target.yMin - Gap - height;
            }
            y = Mathf.Clamp(y, area.yMin, Mathf.Max(area.yMin, area.yMax - height));
            _box.style.left = x;
            _box.style.top = y;
        }
    }
}
