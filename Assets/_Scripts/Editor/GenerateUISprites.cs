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

        /// <summary>Глибина «розмиття» країв гало в пікселях.</summary>
        private const int GlowSize = 128;
        private const int GlowInset = 40;

        private const int CircleSize = 256;

        [MenuItem("Ink Flow/Setup/Generate UI Sprites")]
        public static void Generate()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            InkFlowBootstrap.EnsureFolder(Folder);

            WriteSprite("circle-soft.png", CreateSoftCircle(), pixelsPerUnit: CircleSize, border: Vector4.zero);
            WriteSprite("circle-gloss.png", CreateGloss(), pixelsPerUnit: CircleSize, border: Vector4.zero);

            // Border = радіус кута: центр тягнеться, кути лишаються круглими.
            var r = RoundedRadius;
            WriteSprite("rounded-rect.png", CreateRoundedRect(), pixelsPerUnit: RoundedSize,
                border: new Vector4(r, r, r, r));

            var g = GlowInset;
            WriteSprite("glow.png", CreateGlow(), pixelsPerUnit: GlowSize,
                border: new Vector4(g, g, g, g));

            // ★ і ↺ немає в Nunito (і в жодному OFL-шрифті Google, який варто тягнути
            // заради двох знаків). У макеті вони теж намальовані фігурами, а не набрані
            // текстом — тому робимо їх іконками: жодних warning-ів про відсутні гліфи
            // і чіткість на будь-якому розмірі.
            WriteSprite("icon-star.png", CreateStar(), pixelsPerUnit: CircleSize, border: Vector4.zero);
            WriteSprite("icon-retry.png", CreateRetry(), pixelsPerUnit: CircleSize, border: Vector4.zero);

            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] UI-спрайти згенеровано в {Folder}: circle-soft, circle-gloss, " +
                      "rounded-rect (9-slice), glow (9-slice), icon-star, icon-retry.");
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

        /// <summary>
        /// Відблиск краплі: м'яка пляма у верхньому лівому куті (34% / 27% за макетом),
        /// що згасає до 44% радіуса, плюс легке затемнення по краю кулі.
        /// </summary>
        private static Texture2D CreateGloss()
        {
            var tex = NewTexture(CircleSize);
            var pixels = new Color[CircleSize * CircleSize];
            var center = (CircleSize - 1) * 0.5f;
            var radius = center - 1f;

            // Макет: circle at 34% 27% — рахуємо від верхнього лівого кута,
            // тому по Y (знизу вгору в Unity) це 1 - 0.27.
            var hx = CircleSize * 0.34f;
            var hy = CircleSize * (1f - 0.27f);
            var highlightRadius = CircleSize * 0.44f;

            for (var y = 0; y < CircleSize; y++)
            {
                for (var x = 0; x < CircleSize; x++)
                {
                    var inside = Mathf.Clamp01(
                        (radius - Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center))) / 1.5f);

                    var hd = Mathf.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy));
                    // Дві зупинки макета: 1.0 у центрі, .25 на 20%, 0 на 44%.
                    var highlight = Mathf.Clamp01(1f - hd / highlightRadius);
                    highlight = highlight * highlight;

                    pixels[y * CircleSize + x] = new Color(1f, 1f, 1f, highlight * inside);
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

        private static float RoundedAlpha(int x, int y)
        {
            var r = RoundedRadius;
            var max = RoundedSize - 1;

            // Відстань до найближчого центру кута; поза кутовими зонами — усередині фігури.
            var cx = x < r ? r : (x > max - r ? max - r : x);
            var cy = y < r ? r : (y > max - r ? max - r : y);
            var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01((r - d) / 1.5f);
        }

        /// <summary>
        /// Гало для 9-slice: суцільний центр і м'яко згасаючі краї. Використовується
        /// як зовнішнє світіння під кнопками й панелями (Image з кольором акценту).
        /// </summary>
        private static Texture2D CreateGlow()
        {
            var tex = NewTexture(GlowSize);
            var pixels = new Color[GlowSize * GlowSize];
            var max = GlowSize - 1;

            for (var y = 0; y < GlowSize; y++)
            {
                for (var x = 0; x < GlowSize; x++)
                {
                    // Відстань до краю по кожній осі, нормалізована до зони згасання.
                    var fx = Mathf.Clamp01(Mathf.Min(x, max - x) / (float)GlowInset);
                    var fy = Mathf.Clamp01(Mathf.Min(y, max - y) / (float)GlowInset);
                    var edge = fx * fy;
                    // Квадратичне згасання читається як м'яке гало, лінійне — як сірий кант.
                    pixels[y * GlowSize + x] = new Color(1f, 1f, 1f, edge * edge);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>П'ятикутна зірка рейтингу рівня (замість гліфа ★).</summary>
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
