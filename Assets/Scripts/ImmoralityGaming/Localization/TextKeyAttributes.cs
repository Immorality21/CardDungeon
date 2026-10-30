using UnityEngine;

namespace ImmoralityGaming.Localization
{
    /// <summary>
    /// On an asset's identity field (its <c>Key</c>, or an enum like a tutorial cue): the asset's texts
    /// live in the text table under keys derived from it, <c>{Area}.{slug(id)}.{field}</c>
    /// (<see cref="Loc.KeyFor"/>). The inspector shows and edits their wording right under the field,
    /// and the Texts window counts the asset as their user. An empty <c>Key</c> on a top-level field
    /// falls back to the asset's name, as the save keys do.
    /// </summary>
    public class TextKeysAttribute : PropertyAttribute
    {
        public readonly string Area;
        public readonly string[] Fields;

        public TextKeysAttribute(string area, params string[] fields)
        {
            Area = area;
            Fields = fields;
        }
    }

    /// <summary>
    /// On a string (or string array) field that holds a text key itself: for entries with no identity
    /// of their own (an event outcome, a run's level). The inspector shows the key and its wording.
    /// </summary>
    public class TextKeyAttribute : PropertyAttribute
    {
    }
}
