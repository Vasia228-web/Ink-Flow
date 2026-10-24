using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Аватар гравця (§14): спрайт набору avatar_1…8 або картинка з колекції (2026-10-24). Картинка малюється круглим
    /// портретом — темне полотно W4 колом і пікселі картинки по центру цілим масштабом (чітко, без розмиття), тож
    /// стає в ті самі `Image`, що й спрайти набору: головний, профіль, рейтинги, чужа галактика. Портрет будується
    /// раз на картинку й кешується; бібліотека — та, з якою екрани прив'язали стан (<see cref="ScreenBase.BindState"/>).
    /// Під час забігу аватар не малюється, тож текстура ніколи не створюється посеред партії.
    /// </summary>
    public static class AvatarPortrait
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Бібліотека картинок для портретів; ставить <see cref="ScreenBase.BindState"/>.</summary>
        public static PictureLibrary? Library { get; set; }

        /// <summary>
        /// Спрайт аватара: картинка з колекції, якщо вона є в бібліотеці, інакше спрайт набору за номером (поза набором —
        /// перший). Інкогніто вирішує викликач.
        /// </summary>
        public static Sprite? For(int avatarId, string? pictureId, Sprite[] set, DesignSystem? design)
        {
            if (pictureId is { Length: > 0 } && design != null)
            {
                var portrait = Portrait(pictureId, design);
                if (portrait != null)
                    return portrait;
            }
            if (set.Length == 0)
                return null;
            return set[avatarId >= 0 && avatarId < set.Length ? avatarId : 0];
        }

        private static Sprite? Portrait(string pictureId, DesignSystem design)
        {
            if (Cache.TryGetValue(pictureId, out var cached) && cached != null)
                return cached;
            var picture = Library?.Find(pictureId);
            if (picture == null)
                return null;

            // Ціле число текселів на піксель картинки (чітко), а коло — під частку діаметра: розмір текстури свій для картинки.
            var scale = Mathf.Max(1, design.AvatarPortraitPixel);
            var art = Mathf.Max(picture.Width, picture.Height) * scale;
            var size = Mathf.Max(16, Mathf.CeilToInt(art / Mathf.Clamp(design.AvatarPortraitFill, 0.1f, 1f)));
            var pixels = new Color32[size * size];
            var canvas = (Color32)design.PictureCanvasColor;
            var radius = size * 0.5f;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    // Коло з м'яким краєм у піксель: інакше аватар-картинка мала б драбинку на ободі.
                    var dx = x + 0.5f - radius;
                    var dy = y + 0.5f - radius;
                    var edge = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    var c = canvas;
                    c.a = (byte)(canvas.a * edge);
                    pixels[y * size + x] = c;
                }

            var ox = (size - picture.Width * scale) / 2;
            var oy = (size - picture.Height * scale) / 2;
            for (var y = 0; y < picture.Height; y++)
                for (var x = 0; x < picture.Width; x++)
                {
                    var index = picture[x, y];
                    if (index == MasterPalette.Empty)
                        continue;
                    Color32 color = MasterPalette.ColorOf(index).ToColor();
                    // У файлі рядки згори вниз, у текстурі — знизу вгору.
                    var ty = oy + (picture.Height - 1 - y) * scale;
                    for (var sy = 0; sy < scale; sy++)
                        for (var sx = 0; sx < scale; sx++)
                        {
                            var px = ox + x * scale + sx;
                            var py = ty + sy;
                            if (px >= 0 && px < size && py >= 0 && py < size)
                                pixels[py * size + px] = color;
                        }
                }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Avatar_{pictureId}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            Cache[pictureId] = sprite;
            return sprite;
        }
    }
}
