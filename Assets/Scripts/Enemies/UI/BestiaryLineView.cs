using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine.UIElements;

namespace Assets.Scripts.Enemies.UI
{
    /// <summary>
    /// Renders <see cref="BestiaryLine"/>s as UI Toolkit rows. Shared by the in-combat Inspect
    /// window (<c>MagicSelectionUI</c>) and the hub bestiary (<c>BestiaryUI</c>) so the two screens
    /// cannot drift apart in either wording or colour - the wording comes from
    /// <see cref="BestiaryPresenter"/>, the colour from here, and neither view decides either.
    /// </summary>
    public static class BestiaryLineView
    {
        /// <summary>A section heading ("Resistances", "Draw", ...) inside a knowledge panel.</summary>
        public static Label Section(string title)
        {
            var label = new Label(title);
            label.AddToClassList("cd-scan__section");
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>One label/value row, toned by what the value means for the player.</summary>
        public static VisualElement Row(BestiaryLine line)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-scan__row");
            row.pickingMode = PickingMode.Ignore;

            var label = new Label(line.Label);
            label.AddToClassList("cd-scan__label");
            // A row that is all label and no value ("???", "3 not yet seen") is itself the gap in
            // the record, so the label takes the dim unknown tone.
            if (line.Tone == BestiaryTone.Unknown && string.IsNullOrEmpty(line.Value))
            {
                label.AddToClassList(ToneClass(BestiaryTone.Unknown));
            }
            AddElementClass(label, line.LabelElement);
            label.pickingMode = PickingMode.Ignore;
            row.Add(label);

            var value = new Label(line.Value);
            value.AddToClassList("cd-scan__value");
            value.AddToClassList(ToneClass(line.Tone));
            AddElementClass(value, line.ValueElement);
            value.pickingMode = PickingMode.Ignore;
            row.Add(value);

            return row;
        }

        /// <summary>
        /// Appends rows with no section heading - for a group that already carries its label on its
        /// first row, the way a drop table does.
        /// </summary>
        public static void AddRows(VisualElement parent, List<BestiaryLine> lines)
        {
            if (parent == null || lines == null)
            {
                return;
            }
            foreach (var line in lines)
            {
                parent.Add(Row(line));
            }
        }

        /// <summary>Appends a section heading plus its rows; skips the section entirely when empty.</summary>
        public static void AddSection(VisualElement parent, string title, List<BestiaryLine> lines)
        {
            if (parent == null || lines == null || lines.Count == 0)
            {
                return;
            }

            parent.Add(Section(title));
            foreach (var line in lines)
            {
                parent.Add(Row(line));
            }
        }

        /// <summary>
        /// "cd-element--fire" and so on: an element's name is drawn in the element's colour, so a
        /// resistance table reads by colour before it is read by word.
        /// </summary>
        public static string ElementClass(DamageType type)
        {
            return "cd-element--" + type.ToString().ToLowerInvariant();
        }

        private static void AddElementClass(VisualElement element, DamageType? type)
        {
            if (type.HasValue)
            {
                element.AddToClassList("cd-element");
                element.AddToClassList(ElementClass(type.Value));
            }
        }

        public static string ToneClass(BestiaryTone tone)
        {
            switch (tone)
            {
                case BestiaryTone.Good:
                    return "cd-scan__value--good";
                case BestiaryTone.Bad:
                    return "cd-scan__value--bad";
                case BestiaryTone.Unknown:
                    return "cd-scan__value--unknown";
                default:
                    return "cd-scan__value--neutral";
            }
        }
    }
}
