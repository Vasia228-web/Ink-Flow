using System.IO;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Процедурно генерує базові UI-спрайти в Assets/_Sprites/UI.
    /// Меню: Ink Flow → Setup → Generate UI Sprites.
    ///
    /// Усі спрайти БІЛІ — колір задає Image.color із DesignSystem. Так одна текстура
    /// обслуговує всі кольори палітри, а атлас лишається крихітним (мобільний бюджет §12).
    /// 9-slice border-и виставляються тут же: якщо їх не задати, заокруглені кути
    /// розтягуються й перетворюються на овали.
    /// </summary>
    public static class GenerateUISprites
    {
        private const string Folder = "Assets/_Sprites/UI";

        /// <summary>Роздільність кутового радіуса. 64 px радіуса вистачає для всіх наших розмірів.</summary>
        private const int RoundedSize = 128;
        private const int RoundedRadius = 40;

        /// <summary>Гало картинки забігу (card-glow): заокруглений силует і зона згасання назовні.</summary>
        private const int GlowSize = 256;
        private const int GlowRadius = 56;

        /// <summary>Скільки пікселів гало згасає НАЗОВНІ від контуру.</summary>
        internal const int GlowFalloff = 56;

        private const int CircleSize = 256;


        [MenuItem("Ink Flow/Setup/Generate UI Sprites")]
        public static void Generate()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            InkFlowBootstrap.EnsureFolder(Folder);

            WriteSprite("circle-soft.png", CreateSoftCircle(), pixelsPerUnit: CircleSize, border: Vector4.zero);

            // Біла площина під квади процедурних планет. Без спрайта Image видає
            // UV = (0,0,0,0), і шейдер отримав би нуль замість координат квада.
            WriteSprite("white-quad.png", CreateWhiteQuad(), pixelsPerUnit: 4, border: Vector4.zero);

            // Border = радіус кута: центр тягнеться, кути лишаються круглими.
            // PPU = 100 (як referencePixelsPerUnit канваса): тоді розмір border у
            // world-одиницях = texture_px / pixelsPerUnitMultiplier, і радіус стає
            // керованим одним множником без прихованих коефіцієнтів.
            var r = RoundedRadius;
            WriteSprite("rounded-rect.png", CreateRoundedRect(), pixelsPerUnit: 100,
                border: new Vector4(r, r, r, r));

            var gb = GlowRadius + GlowFalloff;
            // Гало картинки забігу — без внутрішнього краю (альфа 0 у силуеті, згасання лише назовні):
            // згасання в обидва боки після стиснення множником PPU сходилось би в яскраве кільце.
            WriteSprite("card-glow.png", CreateCardGlow(), pixelsPerUnit: 100,
                border: new Vector4(gb, gb, gb, gb));

            // ★ і ↺ немає в Nunito (і в жодному OFL-шрифті Google, який варто тягнути
            // заради двох знаків). У макеті вони теж намальовані фігурами, а не набрані
            // текстом — тому робимо їх іконками: жодних warning-ів про відсутні гліфи
            // і чіткість на будь-якому розмірі.
            WriteSprite("icon-star.png", CreateStar(), pixelsPerUnit: CircleSize, border: Vector4.zero);
            WriteSprite("icon-retry.png", CreateRetry(), pixelsPerUnit: CircleSize, border: Vector4.zero);

            // Іконки нижньої навігації старого хаба (icon-galaxy/ranks/profile) прибрано з кроком 1 U1Glaze:
            // навігація — спрайти набору U1Glaze (icon_play, icon_planet, icon_grid, icon_trophy).

            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] UI-спрайти згенеровано в {Folder}: circle-soft, white-quad, rounded-rect і card-glow (9-slice), " +
                      "icon-star, icon-retry.");
        }

        // ───────────────────────── Малювання ─────────────────────────

        /// <summary>Біле коло з м'яким краєм: 1.5 px антиаліасингу, щоб краплі не «пиляли».</summary>
        private static Texture2D CreateSoftCircle()
        {
            var tex = NewTexture(CircleSize);
            var pixels = new Color[CircleSize * CircleSize];
            var center = (CircleSize - 1) * 0.5f;
            var radius = center - 1f;

            for (var y = 0; y < CircleSize; y++)
            {
                for (var x = 0; x < CircleSize; x++)
                {
                    var d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    var alpha = Mathf.Clamp01((radius - d) / 1.5f);
                    pixels[y * CircleSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Заокруглений прямокутник для 9-slice: кути радіусом RoundedRadius.</summary>
        private static Texture2D CreateRoundedRect()
        {
            var tex = NewTexture(RoundedSize);
            var pixels = new Color[RoundedSize * RoundedSize];

            for (var y = 0; y < RoundedSize; y++)
            {
                for (var x = 0; x < RoundedSize; x++)
                    pixels[y * RoundedSize + x] = new Color(1f, 1f, 1f, RoundedAlpha(x, y));
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static float RoundedAlpha(int x, int y) =>
            Mathf.Clamp01(-RoundedDistance(x, y) / 1.5f);

        /// <summary>
        /// М'яке гало картки — РОЗМИТА ПЛЯМА, а не кільце.
        ///
        /// Головне тут — що альфа НЕ має розриву ніде. Перша спроба ставила
        /// нуль усередині силуету й одиницю відразу за ним: стрибок 0 → 1 на
        /// межі малює чітку лінію, і на екрані вона читається як замкнений
        /// кольоровий контур навколо картки з відступом від неї.
        ///
        /// Тут пік лежить ГЛИБОКО ВСЕРЕДИНІ силуету (його однаково закриває
        /// сама картка), а назовні альфа плавно згасає до нуля. На межі картки
        /// вона вже близько половини піку — тобто видимого краю немає взагалі.
        /// </summary>
        private static Texture2D CreateCardGlow()
        {
            var tex = NewTexture(GlowSize);
            var pixels = new Color[GlowSize * GlowSize];
            var inset = GlowFalloff;
            var max = GlowSize - 1;

            // Наскільки глибоко під карткою лежить пік. Саме цей запас і прибирає
            // стрибок: функція неперервна по всій ширині спрайта.
            const float innerRamp = 26f;

            for (var y = 0; y < GlowSize; y++)
            {
                for (var x = 0; x < GlowSize; x++)
                {
                    var cx = Mathf.Clamp(x, inset + GlowRadius, max - inset - GlowRadius);
                    var cy = Mathf.Clamp(y, inset + GlowRadius, max - inset - GlowRadius);
                    var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - GlowRadius;

                    var t = Mathf.Clamp01(1f - (d + innerRamp) / (GlowFalloff + innerRamp));

                    // Квадратичне згасання читається як світло; лінійне лишає
                    // видиму «сходинку» там, де альфа доходить до нуля.
                    pixels[y * GlowSize + x] = new Color(1f, 1f, 1f, t * t);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Відстань від точки до відрізка — основа для ланок галочки.</summary>
        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, Vector2.Dot(ab, ab)));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Суцільний білий квадрат: носій UV для шейдерних квадів.</summary>
        private static Texture2D CreateWhiteQuad()
        {
            var tex = NewTexture(4);
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Знакова відстань до контуру заокругленого прямокутника (від'ємна всередині).</summary>
        private static float RoundedDistance(int x, int y)
        {
            var r = RoundedRadius;
            var max = RoundedSize - 1;
            var cx = Mathf.Clamp(x, r, max - r);
            var cy = Mathf.Clamp(y, r, max - r);
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        }

        /// <summary>П'ятикутна зірка — спрайт під гліф ★ у будь-якому тексті (TMP Sprite Asset).</summary>
        private static Texture2D CreateStar()
        {
            var tex = NewTexture(CircleSize);
            var pixels = new Color[CircleSize * CircleSize];
            var center = new Vector2((CircleSize - 1) * 0.5f, (CircleSize - 1) * 0.5f);
            var outer = CircleSize * 0.47f;
            var inner = outer * 0.42f; // класична пропорція п'ятикутної зірки

            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                // Починаємо з вершини вгору (90°), далі через 36°.
                var angle = Mathf.Deg2Rad * (90f + i * 36f);
                var r = i % 2 == 0 ? outer : inner;
                points[i] = center + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            }

            for (var y = 0; y < CircleSize; y++)
            {
                for (var x = 0; x < CircleSize; x++)
                {
                    // 2×2 суперсемплінг: край зірки інакше помітно «пиляє».
                    var coverage = 0f;
                    for (var sy = 0; sy < 2; sy++)
                        for (var sx = 0; sx < 2; sx++)
                            if (InPolygon(points, new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f)))
                                coverage += 0.25f;

                    pixels[y * CircleSize + x] = new Color(1f, 1f, 1f, coverage);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Кругла стрілка «почати спочатку» (замість гліфа ↺).</summary>
        private static Texture2D CreateRetry()
        {
            var tex = NewTexture(CircleSize);
            var pixels = new Color[CircleSize * CircleSize];
            var center = new Vector2((CircleSize - 1) * 0.5f, (CircleSize - 1) * 0.5f);
            var radius = CircleSize * 0.33f;
            var thickness = CircleSize * 0.10f;

            // Дуга з розривом угорі праворуч, куди сідає наконечник.
            const float gapStart = 30f;
            const float gapEnd = 105f;

            var head = new Vector2[3];
            var headAngle = Mathf.Deg2Rad * gapEnd;
            var headCenter = center + new Vector2(Mathf.Cos(headAngle), Mathf.Sin(headAngle)) * radius;
            var tangent = new Vector2(-Mathf.Sin(headAngle), Mathf.Cos(headAngle));
            var normal = new Vector2(Mathf.Cos(headAngle), Mathf.Sin(headAngle));
            var headSize = thickness * 1.7f;
            head[0] = headCenter + tangent * headSize;
            head[1] = headCenter + normal * headSize - tangent * headSize * 0.35f;
            head[2] = headCenter - normal * headSize - tangent * headSize * 0.35f;

            for (var y = 0; y < CircleSize; y++)
            {
                for (var x = 0; x < CircleSize; x++)
                {
                    var coverage = 0f;
                    for (var sy = 0; sy < 2; sy++)
                    {
                        for (var sx = 0; sx < 2; sx++)
                        {
                            var p = new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f);
                            var d = Vector2.Distance(p, center);
                            var onRing = Mathf.Abs(d - radius) <= thickness * 0.5f;

                            if (onRing)
                            {
                                var angle = Mathf.Repeat(
                                    Mathf.Atan2(p.y - center.y, p.x - center.x) * Mathf.Rad2Deg, 360f);
                                if (angle > gapStart && angle < gapEnd)
                                    onRing = false;
                            }

                            if (onRing || InPolygon(head, p))
                                coverage += 0.25f;
                        }
                    }

                    pixels[y * CircleSize + x] = new Color(1f, 1f, 1f, coverage);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Класичний ray-casting: точка всередині багатокутника.</summary>
        private static bool InPolygon(Vector2[] poly, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (poly[i].y > p.y != poly[j].y > p.y &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }

            return inside;
        }


        // ───────────────────────── Іконки навігації ─────────────────────────
        // Малюємо в нормалізованих координатах 0..1, щоб числа читались як пропорції.

        // ───────────────────────── Запис і імпорт ─────────────────────────

        private static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };

        private static void WriteSprite(string fileName, Texture2D texture, int pixelsPerUnit, Vector4 border)
        {
            var path = $"{Folder}/{fileName}";
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            // Спрайти чисто-білі з альфою: 8-бітного альфа-каналу достатньо,
            // а ASTC на мобільних дає видимі артефакти на м'яких градієнтах гало.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
