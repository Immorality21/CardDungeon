using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Combat.Editor
{
    /// <summary>
    /// Auto-imports any texture placed under <c>Resources/CombatBackgrounds/</c> as a crisp
    /// pixel-art Sprite, so <see cref="CombatStage"/> can load it via
    /// <c>Resources.Load&lt;Sprite&gt;("CombatBackgrounds/battle")</c> with no manual inspector
    /// tweaks. Drop a PNG named <c>battle.png</c> in that folder and it just works.
    /// </summary>
    public class CombatBackgroundPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/CombatBackgrounds/"))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;              // crisp pixels, no blur
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
        }
    }
}
