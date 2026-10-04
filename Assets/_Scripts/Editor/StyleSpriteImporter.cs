using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Налаштування імпорту еталонних спрайтів K1Candy (`Assets/_Sprites/K1Candy`). Файли скопійовано з
    /// `docs/StyleRef/` як є — тому імпорт задається постпроцесором, а не руками в інспекторі:
    /// 9-slice-межі панелі й слота лотка інакше довелося б виставляти після кожного оновлення еталонів.
    ///
    /// Межі (px текстури) — з README K1Candy: панель 64 з кожного боку, слот 44.
    /// </summary>
    public sealed class StyleSpriteImporter : AssetPostprocessor
    {
        public const string K1Folder = "Assets/_Sprites/K1Candy";

        /// <summary>PPU для всіх спрайтів стилю: як у UI-спрайтів, радіуси керуються pixelsPerUnitMultiplier.</summary>
        public const int PixelsPerUnit = 100;

        public const int PanelBorder = 64;
        public const int SlotBorder = 44;

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(K1Folder))
                ConfigureK1((TextureImporter)assetImporter);
        }

        private void ConfigureK1(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            // Градієнти блоків і м'яке гало: стиснення ASTC дало б смуги.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var border = name == "panel" ? PanelBorder : name == "tray-slot" ? SlotBorder : 0;
            importer.spriteBorder = new Vector4(border, border, border, border);
        }
    }
}
