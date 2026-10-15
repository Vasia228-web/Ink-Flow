using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Налаштування імпорту еталонних спрайтів: K1Candy (`Assets/_Sprites/K1Candy`) і U1Glaze
    /// (`Assets/_Sprites/U1Glaze`). Файли скопійовано з `docs/StyleRef/` як є — тому імпорт задається
    /// постпроцесором, а не руками в інспекторі: 9-slice-межі інакше довелося б виставляти після
    /// кожного оновлення еталонів.
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

        /// <summary>
        /// Спрайти U1Glaze, чий м'який хвіст альфи намальовано для змішування в ГАМІ (веб-еталон): у лінійному
        /// просторі проєкту альфа 2…10 зі 255 світлої фарби на темному фоні дає видимий ступінчастий контур — гало
        /// планети на заставці читалось диском із краєм. Для них альфа при імпорті стає a^2.2: на темному фоні
        /// лінійне змішування тоді дає те саме, що гамма-змішування еталона; непрозоре (a = 1) і порожнє не міняються.
        /// Решта спрайтів (гало кнопок, рамки) на кроці 0 виглядає як треба — їх не чіпаємо.
        /// </summary>
        private static readonly string[] GammaAuthoredAlpha = { "home_hero" };

        /// <summary>Показник sRGB-гами, яким гамма-змішування відрізняється від лінійного на темному фоні.</summary>
        private const float SrgbGamma = 2.2f;

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith(U1GlazeSprites.Folder))
                return;
            var entry = U1GlazeSprites.OfPath(assetPath);
            if (entry == null || System.Array.IndexOf(GammaAuthoredAlpha, entry.name) < 0)
                return;
            var pixels = texture.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var a = pixels[i].a;
                if (a == 0 || a == 255)
                    continue;
                pixels[i].a = (byte)Mathf.RoundToInt(255f * Mathf.Pow(a / 255f, SrgbGamma));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(K1Folder))
                ConfigureK1((TextureImporter)assetImporter);
            else if (assetPath.StartsWith(U1GlazeSprites.Folder))
                ConfigureU1((TextureImporter)assetImporter);
        }

        /// <summary>
        /// Спрайти U1Glaze (`Assets/_Sprites/U1Glaze`): PPU під канвас проєкту, межі 9-slice і
        /// Full Rect — з маніфесту sprites.json (<see cref="U1GlazeSprites"/>). Стиснення тут не
        /// вмикаємо: його робить атлас (ASTC на мобільних), а джерело лишається без втрат.
        /// </summary>
        private void ConfigureU1(TextureImporter importer)
        {
            var entry = U1GlazeSprites.OfPath(assetPath);
            if (entry == null)
            {
                Debug.LogWarning($"[InkFlow] {assetPath} немає в sprites.json — імпортую як звичайний спрайт без меж.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = U1GlazeSprites.PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = entry?.ImporterBorder ?? Vector4.zero;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
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
