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

        /// <summary>
        /// Гало — РАНТ уздовж контуру, а не заповнений силует. Заповнений заливав
        /// акцентом усю площу картки під напівпрозорим склом, і картка світилась
        /// цілком замість того, щоб світився її контур.
        /// </summary>
        private const int GlowSize = 256;
        private const int GlowRadius = 56;

        /// <summary>Скільки пікселів гало згасає НАЗОВНІ від контуру.</summary>
        internal const int GlowFalloff = 56;

        /// <summary>Скільки пікселів гало заходить УСЕРЕДИНУ: рант має триматись
        /// контуру, а не обриватись на ньому кантом.</summary>
        private const int GlowInnerFade = 14;

        /// <summary>Туманність: величезне м'яке ядро без жодного видимого краю.</summary>
        private const int NebulaSize = 256;

        /// <summary>Товщина обведення в пікселях текстури (≈2 px макета після масштабу).</summary>
        private const float OutlineThickness = 3f;

        private const int CircleSize = 256;

        /// <summary>Роздільність іконок навігації (26 px макета → з запасом на retina).</summary>
        private const int IconSize = 128;

        [MenuItem("Ink Flow/Setup/Generate UI Sprites")]
        public static void Generate()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            InkFlowBootstrap.EnsureFolder(Folder);

            WriteSprite("circle-soft.png", CreateSoftCircle(), pixelsPerUnit: CircleSize, border: Vector4.zero);

            // Туманність окремим спрайтом: circle-soft — це суцільний диск із краєм
            // у 1.5 px, і саме тому туманність читалась як куля з чіткою межею.
            WriteSprite("nebula.png", CreateNebula(), pixelsPerUnit: NebulaSize, border: Vector4.zero);
            WriteSprite("circle-gloss.png", CreateGloss(), pixelsPerUnit: CircleSize, border: Vector4.zero);

            // Border = радіус кута: центр тягнеться, кути лишаються круглими.
            // PPU = 100 (як referencePixelsPerUnit канваса): тоді розмір border у
            // world-одиницях = texture_px / pixelsPerUnitMultiplier, і радіус стає
            // керованим одним множником без прихованих коефіцієнтів.
            var r = RoundedRadius;
            WriteSprite("rounded-rect.png", CreateRoundedRect(), pixelsPerUnit: 100,
                border: new Vector4(r, r, r, r));

            // Справжня ОБВЕДЕННЯ-рамка з прозорою серединою. Раніше і заливка, і «рамка»
            // малювались одним заповненим спрайтом — рамка лягала суцільною плашкою
            // поверх заливки, через що скло виглядало залитим кольором.
            WriteSprite("rounded-rect-outline.png", CreateRoundedOutline(), pixelsPerUnit: 100,
                border: new Vector4(r, r, r, r));

            // Гало: заокруглений силует із широкою м'якою зоною згасання.
            // Старе glow.png мало прямі кути й вузький край — звідси прямокутні смуги.
            var gb = GlowRadius + GlowFalloff;
            WriteSprite("glow.png", CreateGlow(), pixelsPerUnit: 100,
                border: new Vector4(gb, gb, gb, gb));

            // ★ і ↺ немає в Nunito (і в жодному OFL-шрифті Google, який варто тягнути
            // заради двох знаків). У макеті вони теж намальовані фігурами, а не набрані
            // текстом — тому робимо їх іконками: жодних warning-ів про відсутні гліфи
            // і чіткість на будь-якому розмірі.
            WriteSprite("icon-star.png", CreateStar(), pixelsPerUnit: CircleSize, border: Vector4.zero);
            WriteSprite("icon-retry.png", CreateRetry(), pixelsPerUnit: CircleSize, border: Vector4.zero);

            // Іконки нижньої навігації. У макеті вони складені з <div>-фігур; тут
            // малюємо ті самі силуети процедурно — білими, колір задає DesignSystem.
            WriteSprite("icon-galaxy.png", CreateGalaxyIcon(), pixelsPerUnit: IconSize, border: Vector4.zero);
            WriteSprite("icon-shop.png", CreateShopIcon(), pixelsPerUnit: IconSize, border: Vector4.zero);
            WriteSprite("icon-ranks.png", CreateRanksIcon(), pixelsPerUnit: IconSize, border: Vector4.zero);
            WriteSprite("icon-profile.png", CreateProfileIcon(), pixelsPerUnit: IconSize, border: Vector4.zero);

            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] UI-спрайти згенеровано в {Folder}: circle-soft, circle-gloss, nebula, " +
                      "rounded-rect + outline (9-slice), glow (9-slice), icon-star, icon-retry, " +
                      "icon-galaxy, icon-shop, icon-ranks, icon-profile.");
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

        private static float RoundedAlpha(int x, int y) =>
            Mathf.Clamp01(-RoundedDistance(x, y) / 1.5f);

        /// <summary>
        /// Обведення заокругленого прямокутника: видно лише кант завтовшки
        /// OutlineThickness, середина прозора. Саме це дає «тонку рамку» скла.
        /// </summary>
        private static Texture2D CreateRoundedOutline()
        {
            var tex = NewTexture(RoundedSize);
            var pixels = new Color[RoundedSize * RoundedSize];

            for (var y = 0; y < RoundedSize; y++)
            {
                for (var x = 0; x < RoundedSize; x++)
                {
                    // Відстань до контуру фігури: додатна зовні, від'ємна всередині.
                    var d = RoundedDistance(x, y);
                    // Кант лежить усередині від контуру.
                    var alpha = Mathf.Clamp01((OutlineThickness - Mathf.Abs(d + OutlineThickness * 0.5f)) / 1.2f);
                    pixels[y * RoundedSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Гало: заокруглений силует, що плавно згасає до нуля на GlowFalloff пікселів.
        /// Центр непрозорий, але його завжди перекриває сама картка — видно лише
        /// м'яке світіння назовні, без видимих країв.
        /// </summary>
        private static Texture2D CreateGlow()
        {
            var tex = NewTexture(GlowSize);
            var pixels = new Color[GlowSize * GlowSize];
            var inset = GlowFalloff;
            var max = GlowSize - 1;

            for (var y = 0; y < GlowSize; y++)
            {
                for (var x = 0; x < GlowSize; x++)
                {
                    // Відстань до заокругленого прямокутника, вписаного з відступом inset.
                    var cx = Mathf.Clamp(x, inset + GlowRadius, max - inset - GlowRadius);
                    var cy = Mathf.Clamp(y, inset + GlowRadius, max - inset - GlowRadius);
                    var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - GlowRadius;

                    // Рант: назовні згасає за GlowFalloff, усередину — за GlowInnerFade.
                    // Середина картки лишається прозорою, тому гало підсвічує контур,
                    // а не заливає всю площу.
                    var t = d >= 0f
                        ? Mathf.Clamp01(1f - d / GlowFalloff)
                        : Mathf.Clamp01(1f + d / GlowInnerFade);

                    // Квадратичне згасання читається як світіння; лінійне дає видимий кант.
                    pixels[y * GlowSize + x] = new Color(1f, 1f, 1f, t * t);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Туманність: купол (1-t²)³ від центру до краю. Похідна на межі — нуль,
        /// тому спрайт не має ані канта, ані видимої «кулі»; гравець бачить лише
        /// те, що фон перестав бути пласким.
        /// </summary>
        private static Texture2D CreateNebula()
        {
            var tex = NewTexture(NebulaSize);
            var pixels = new Color[NebulaSize * NebulaSize];
            var center = (NebulaSize - 1) * 0.5f;

            for (var y = 0; y < NebulaSize; y++)
            {
                for (var x = 0; x < NebulaSize; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var core = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    var alpha = core * core * core;
                    pixels[y * NebulaSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

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


        // ───────────────────────── Іконки навігації ─────────────────────────
        // Малюємо в нормалізованих координатах 0..1, щоб числа читались як пропорції.

        /// <summary>Планета з кільцем — вкладка «Галактика».</summary>
        private static Texture2D CreateGalaxyIcon() => DrawIcon((u, v) =>
        {
            var d = Dist(u, v, 0.5f, 0.52f);
            var planet = Cover(0.30f - d);

            // Кільце — еліпс, нахилений на -20°, з вирізаною серединою.
            var (rx, ry) = Rotate(u - 0.5f, v - 0.52f, -20f);
            var ring = Mathf.Sqrt(rx * rx / (0.46f * 0.46f) + ry * ry / (0.15f * 0.15f));
            var ringBand = Cover(0.06f - Mathf.Abs(ring - 1f) * 0.5f);

            return Mathf.Max(planet, ringBand);
        });

        /// <summary>Відро з фарбою — вкладка «Магазин».</summary>
        private static Texture2D CreateShopIcon() => DrawIcon((u, v) =>
        {
            // Корпус — трапеція, звужена донизу.
            var top = 0.66f;
            var bottom = 0.16f;
            var t = Mathf.InverseLerp(bottom, top, v);
            var halfWidth = Mathf.Lerp(0.20f, 0.28f, t);
            var body = v >= bottom && v <= top ? Cover((halfWidth - Mathf.Abs(u - 0.5f)) * 4f) : 0f;

            // Ручка — дуга над відром.
            var hd = Mathf.Abs(Dist(u, v, 0.5f, top) - 0.24f);
            var handle = v > top ? Cover((0.035f - hd) * 6f) : 0f;

            return Mathf.Max(body, handle);
        });

        /// <summary>Кубок — вкладка «Рейтинги».</summary>
        private static Texture2D CreateRanksIcon() => DrawIcon((u, v) =>
        {
            // Чаша: звужується донизу, зверху рівна.
            var cupTop = 0.78f;
            var cupBottom = 0.40f;
            var t = Mathf.InverseLerp(cupBottom, cupTop, v);
            var half = Mathf.Lerp(0.10f, 0.26f, Mathf.Sqrt(Mathf.Clamp01(t)));
            var cup = v >= cupBottom && v <= cupTop ? Cover((half - Mathf.Abs(u - 0.5f)) * 5f) : 0f;

            // Вушка з боків.
            var earL = Cover((0.055f - Mathf.Abs(Dist(u, v, 0.24f, 0.63f) - 0.09f)) * 6f);
            var earR = Cover((0.055f - Mathf.Abs(Dist(u, v, 0.76f, 0.63f) - 0.09f)) * 6f);

            // Ніжка й основа.
            var stem = v > 0.24f && v < cupBottom ? Cover((0.055f - Mathf.Abs(u - 0.5f)) * 6f) : 0f;
            var baseBar = v > 0.14f && v < 0.24f ? Cover((0.22f - Mathf.Abs(u - 0.5f)) * 6f) : 0f;

            return Mathf.Max(Mathf.Max(cup, Mathf.Max(earL, earR)), Mathf.Max(stem, baseBar));
        });

        /// <summary>Силует людини — вкладка «Профіль».</summary>
        private static Texture2D CreateProfileIcon() => DrawIcon((u, v) =>
        {
            var head = Cover((0.17f - Dist(u, v, 0.5f, 0.72f)) * 6f);
            // Плечі — половина еліпса знизу.
            var sx = (u - 0.5f) / 0.34f;
            var sy = (v - 0.16f) / 0.34f;
            var shoulders = v >= 0.16f && v < 0.5f
                ? Cover((1f - Mathf.Sqrt(sx * sx + sy * sy)) * 4f)
                : 0f;
            return Mathf.Max(head, shoulders);
        });

        private static float Dist(float x, float y, float cx, float cy) =>
            Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        private static (float x, float y) Rotate(float x, float y, float degrees)
        {
            var a = degrees * Mathf.Deg2Rad;
            return (x * Mathf.Cos(a) - y * Mathf.Sin(a), x * Mathf.Sin(a) + y * Mathf.Cos(a));
        }

        /// <summary>Плавний перехід 0..1 навколо межі — дешевий антиаліасинг.</summary>
        private static float Cover(float signedDistance) =>
            Mathf.Clamp01(signedDistance * IconSize * 0.5f);

        /// <summary>Малює іконку за функцією покриття з 2×2 суперсемплінгом.</summary>
        private static Texture2D DrawIcon(System.Func<float, float, float> coverage)
        {
            var tex = NewTexture(IconSize);
            var pixels = new Color[IconSize * IconSize];

            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var a = 0f;
                    for (var sy = 0; sy < 2; sy++)
                        for (var sx = 0; sx < 2; sx++)
                            a += coverage((x + 0.25f + sx * 0.5f) / IconSize,
                                          (y + 0.25f + sy * 0.5f) / IconSize) * 0.25f;
                    pixels[y * IconSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
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
