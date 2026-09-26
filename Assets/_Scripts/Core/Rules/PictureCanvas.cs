using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Математика полотна картинки W4DarkCanvas (`docs/StyleRef/W4DarkCanvas/`), яку не можна
    /// лишати шейдеру: що саме видно на кожному пікселі арту і де світиться гало фарби.
    /// Живе в Core, щоб «готова картинка = піксель-арт піксель у піксель» і «гало лише навколо
    /// зафарбованого» тримали headless-тести. Стиль змінює лише відображення — не сам малюнок.
    /// </summary>
    public static class PictureCanvas
    {
        /// <summary>
        /// Покриття кожного пікселя арту 0..1 (рядок 0 — верхній). Пікселі заливки — їхнє проявлення
        /// (<paramref name="reveal"/>). Контурні пікселі кроками не зафарбовуються, тож у порожній
        /// картинці їх не видно (незафарбоване — лише світлі лінії); вони проявляються разом із
        /// сусідньою фарбою: максимум серед 8 сусідів, другим проходом — і від сусідніх контурних
        /// (контур у два пікселі). Коли вся заливка проявлена, видно весь контур — готова картинка
        /// збігається з артом.
        /// </summary>
        public static void Coverage(PixelPicture picture, IReadOnlyList<float> reveal, float[] into) =>
            Coverage(picture, reveal, into, new float[picture.Width * picture.Height]);

        /// <param name="scratch">Робочий буфер розміру арту: другий прохід читає результат першого, а не
        /// щойно оновлені сусіди — інакше проявлення протягнулося б уздовж усього контуру за один хід.</param>
        public static void Coverage(PixelPicture picture, IReadOnlyList<float> reveal, float[] into, float[] scratch)
        {
            if (picture is null) throw new ArgumentNullException(nameof(picture));
            if (reveal is null) throw new ArgumentNullException(nameof(reveal));
            var w = picture.Width;
            var h = picture.Height;
            var n = w * h;
            if (into is null || into.Length < n) throw new ArgumentException("Буфер покриття замалий.", nameof(into));
            if (scratch is null || scratch.Length < n) throw new ArgumentException("Робочий буфер замалий.", nameof(scratch));

            var complete = true;
            for (var i = 0; i < n; i++)
            {
                if (picture.IsFillPixel(i))
                {
                    var v = i < reveal.Count ? Clamp01(reveal[i]) : 0f;
                    into[i] = v;
                    if (v < 1f) complete = false;
                }
                else
                    into[i] = 0f;
            }

            for (var pass = 0; pass < 2; pass++)
            {
                Array.Copy(into, scratch, n);
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var i = y * w + x;
                        if (!IsOutline(picture, x, y))
                            continue;
                        if (complete)
                        {
                            into[i] = 1f;
                            continue;
                        }
                        var best = into[i];
                        for (var dy = -1; dy <= 1; dy++)
                            for (var dx = -1; dx <= 1; dx++)
                            {
                                var nx = x + dx;
                                var ny = y + dy;
                                if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h)
                                    continue;
                                var j = ny * w + nx;
                                // Перший прохід — лише від заливки; другий — і від уже проявленого контуру.
                                if (pass == 0 && !picture.IsFillPixel(j))
                                    continue;
                                if (scratch[j] > best)
                                    best = scratch[j];
                            }
                        into[i] = best;
                    }
            }
        }

        /// <summary>Непорожній піксель арту, що не належить жодній родині, — контур.</summary>
        public static bool IsOutline(PixelPicture picture, int x, int y) =>
            picture[x, y] != MasterPalette.Empty && !picture.IsFillPixel(y * picture.Width + x);

        /// <summary>
        /// Гало фарби (еталон: `darkcanvas.py`, шар `_halo`): гаусове розмиття кольорів ПОКРИТИХ
        /// пікселів радіусом <paramref name="sigma"/> пікселів арту; альфа = min(cap, розмите покриття ×
        /// cap × <paramref name="gain"/>). Рахується на сітці полотна — арт плюс <paramref name="margin"/>
        /// порожніх пікселів з кожного боку, — бо світіння виходить за силует. Вихід — RGBA по
        /// 4 float на клітинку, рядок 0 — верхній; колір у sRGB 0..1, як у еталоні.
        /// </summary>
        public static void Halo(PixelPicture picture, float[] coverage, int margin, float sigma, float gain, float cap,
            float[] into, float[] scratch)
        {
            if (picture is null) throw new ArgumentNullException(nameof(picture));
            if (margin < 0) margin = 0;
            var w = picture.Width;
            var h = picture.Height;
            var cw = w + margin * 2;
            var ch = h + margin * 2;
            var cells = cw * ch;
            if (into is null || into.Length < cells * 4) throw new ArgumentException("Буфер гало замалий.", nameof(into));
            if (scratch is null || scratch.Length < cells * 4) throw new ArgumentException("Робочий буфер замалий.", nameof(scratch));

            // Премультиплікований колір і покриття на сітці полотна.
            Array.Clear(into, 0, cells * 4);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var a = coverage[y * w + x];
                    if (a <= 0f)
                        continue;
                    var color = MasterPalette.ColorOf(picture[x, y]);
                    var k = ((y + margin) * cw + x + margin) * 4;
                    into[k] = color.R * a;
                    into[k + 1] = color.G * a;
                    into[k + 2] = color.B * a;
                    into[k + 3] = a;
                }

            if (sigma > 0f)
            {
                var radius = (int)Math.Ceiling(sigma * 3f);
                var kernel = new float[radius * 2 + 1];
                var sum = 0f;
                for (var i = -radius; i <= radius; i++)
                {
                    kernel[i + radius] = (float)Math.Exp(-(i * i) / (2f * sigma * sigma));
                    sum += kernel[i + radius];
                }
                for (var i = 0; i < kernel.Length; i++)
                    kernel[i] /= sum;
                Blur(into, scratch, cw, ch, kernel, radius, horizontal: true);
                Blur(scratch, into, cw, ch, kernel, radius, horizontal: false);
            }

            for (var c = 0; c < cells; c++)
            {
                var k = c * 4;
                var a = into[k + 3];
                if (a <= 1e-6f)
                {
                    into[k] = into[k + 1] = into[k + 2] = into[k + 3] = 0f;
                    continue;
                }
                into[k] /= a;
                into[k + 1] /= a;
                into[k + 2] /= a;
                into[k + 3] = Math.Min(cap, a * cap * gain);
            }
        }

        private static void Blur(float[] src, float[] dst, int w, int h, float[] kernel, int radius, bool horizontal)
        {
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (var t = -radius; t <= radius; t++)
                    {
                        var sx = horizontal ? x + t : x;
                        var sy = horizontal ? y : y + t;
                        if (sx < 0 || sy < 0 || sx >= w || sy >= h)
                            continue; // за полотном — порожньо (нулі), як у еталоні
                        var weight = kernel[t + radius];
                        var k = (sy * w + sx) * 4;
                        r += src[k] * weight;
                        g += src[k + 1] * weight;
                        b += src[k + 2] * weight;
                        a += src[k + 3] * weight;
                    }
                    var o = (y * w + x) * 4;
                    dst[o] = r;
                    dst[o + 1] = g;
                    dst[o + 2] = b;
                    dst[o + 3] = a;
                }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
