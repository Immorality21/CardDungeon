using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// The soft shadow under a unit on the battle stage: what makes it read as standing on the floor
    /// rather than pasted over a picture. A child of the unit, so it follows a lunge or a flinch and
    /// goes with an enemy that dies; it hides while the unit's own sprite is hidden (a party a summon
    /// has replaced). Auto-built, no assets.
    /// </summary>
    public class UnitGroundShadow : MonoBehaviour
    {
        private const string ChildName = "GroundShadow";
        private const int SortOrder = 590; // over the backdrop, under every unit and the cast glow (595)
        private const float WidthShare = 0.9f;
        private const float HeightShare = 0.26f;
        private const float Opacity = 0.75f;

        // Sprites keep a few empty pixels under the feet, so the shadow sits this share of the sprite
        // up from its bottom edge, or it lands under the unit's plate.
        private const float FeetInset = 0.07f;
        private const int TextureWidth = 32;
        private const int TextureHeight = 12;

        private static Sprite _sprite;

        private SpriteRenderer _unit;
        private SpriteRenderer _shadow;

        /// <summary>Puts a shadow at <paramref name="unit"/>'s feet, or refits the one it has.</summary>
        public static void Attach(Transform unit)
        {
            var sr = unit != null ? unit.GetComponent<SpriteRenderer>() : null;
            if (sr == null || sr.sprite == null)
            {
                return;
            }
            var child = unit.Find(ChildName);
            if (child == null)
            {
                var go = new GameObject(ChildName);
                go.transform.SetParent(unit, false);
                child = go.transform;
                var shadow = go.AddComponent<SpriteRenderer>();
                shadow.sprite = ShadowSprite();
                shadow.color = new Color(0f, 0f, 0f, Opacity);
                shadow.sortingOrder = SortOrder;
                var component = go.AddComponent<UnitGroundShadow>();
                component._unit = sr;
                component._shadow = shadow;
            }
            var bounds = sr.sprite.bounds;
            var size = ShadowSprite().bounds.size;
            child.localPosition = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * FeetInset, 0f);
            child.localScale = new Vector3(bounds.size.x * WidthShare / size.x, bounds.size.x * HeightShare / size.y, 1f);
        }

        /// <summary>Takes the shadow off a unit leaving the stage.</summary>
        public static void Remove(Transform unit)
        {
            var child = unit != null ? unit.Find(ChildName) : null;
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (_unit != null && _shadow != null)
            {
                _shadow.enabled = _unit.enabled;
            }
        }

        private static Sprite ShadowSprite()
        {
            if (_sprite != null)
            {
                return _sprite;
            }
            var tex = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < TextureHeight; y++)
            {
                for (int x = 0; x < TextureWidth; x++)
                {
                    float dx = (x + 0.5f) / TextureWidth * 2f - 1f;
                    float dy = (y + 0.5f) / TextureHeight * 2f - 1f;
                    float distance = dx * dx + dy * dy;
                    // Two hard steps, not a blur: this sits under pixel art.
                    float alpha = distance > 1f ? 0f : distance > 0.6f ? 0.55f : 1f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, TextureWidth, TextureHeight), new Vector2(0.5f, 0.5f), TextureWidth);
            return _sprite;
        }
    }
}
