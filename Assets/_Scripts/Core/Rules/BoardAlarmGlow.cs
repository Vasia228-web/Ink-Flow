using System;

namespace InkFlow.Core
{
    /// <summary>Шар тривоги поля A1Breathe (документ §11, `docs/StyleRef/A1Breathe/SPEC.md`).</summary>
    public enum AlarmLayer : byte
    {
        OuterCalm = 0,
        OuterCritical = 1,
        InnerCalm = 2,
        InnerCritical = 3,
        Frame = 4
    }

    /// <summary>
    /// Шари тривоги «мало місця» в стилі A1Breathe, пораховані з тих самих параметрів, з яких
    /// намальовано еталон (`preview/A1Breathe_live.html`): штрих по контуру панелі (заокруглений
    /// прямокутник 352×352, кут 28 px макета), розмитий гаусом; внутрішнє сяйво обрізане панеллю;
    /// тонка рамка — штрих 2 px на відступі 1 px. Колір — діагональний градієнт від коралового до
    /// бурштинового по прямокутнику панелі.
    ///
    /// Навіщо формула, а не PNG еталона: товщину сяйва треба крутити повзунком, а розтягнутий спрайт
    /// змінив би разом із товщиною й заокруглення кутів. Розмиття прямого штриха шириною w — це
    /// різниця двох нормальних розподілів: α(d) = op · (Φ((d + w/2)/σ) − Φ((d − w/2)/σ)), де d —
    /// відстань до контуру панелі. На товщині 1 модель збігається з PNG еталона в середньому
    /// до 2/255 (перевіряє UI-тест), тож «вигляд як там» зберігається, а товщина — токен.
    ///
    /// Геометрія — як у спрайтів еталона: квадрат 864 px, поле 704 px із відступом 80 px
    /// (масштаб 2× від макета); шар прив'язаний до панелі якорями −80/704…1+80/704.
    /// </summary>
    public static class BoardAlarmGlow
    {
        /// <summary>Сторона спрайта еталона, px.</summary>
        public const float SpritePx = 864f;

        /// <summary>Поле в спрайті еталона, px (панель 352 px макета × 2).</summary>
        public const float BoardPx = 704f;

        /// <summary>Відступ поля від краю спрайта, px.</summary>
        public const float MarginPx = 80f;

        /// <summary>Px спрайта на px макета в еталоні.</summary>
        private const float Scale = BoardPx / 352f;

        /// <summary>Параметри шару в px макета (панель 352): ширина штриха, розмиття σ, непрозорість, чи обрізаний панеллю.</summary>
        public static void Spec(AlarmLayer layer, out float width, out float sigma, out float opacity, out bool clipped)
        {
            switch (layer)
            {
                case AlarmLayer.OuterCalm: width = 8f; sigma = 9f; opacity = 1f; clipped = false; break;
                case AlarmLayer.OuterCritical: width = 12f; sigma = 9f; opacity = 1f; clipped = false; break;
                case AlarmLayer.InnerCalm: width = 14f; sigma = 7f; opacity = 0.55f; clipped = true; break;
                case AlarmLayer.InnerCritical: width = 22f; sigma = 7f; opacity = 0.55f; clipped = true; break;
                default: width = 2f; sigma = 0f; opacity = 0.85f; clipped = false; break;
            }
        }

        /// <summary>
        /// Альфа шару в точці спрайта (px еталона 0…864, y униз). <paramref name="thickness"/> множить
        /// ширину штриха й розмиття сяйв (1 — як в еталоні); тонка рамка від неї не залежить.
        /// </summary>
        public static float Alpha(AlarmLayer layer, float x, float y, float thickness)
        {
            Spec(layer, out var width, out var sigma, out var opacity, out var clipped);
            const float centre = SpritePx * 0.5f;
            if (layer == AlarmLayer.Frame)
            {
                // Рамка: прямокутник з відступом 1 px макета, кут 27, штрих 2 — без розмиття, зі згладженням у піксель.
                var half = (BoardPx - 2f * Scale) * 0.5f;
                var d = RoundedRectDistance(x - centre, y - centre, half, half, 27f * Scale);
                var coverage = Clamp01(width * Scale * 0.5f + 0.5f - Math.Abs(d));
                return opacity * coverage;
            }

            var k = thickness <= 0f ? 0.01f : thickness;
            var w = width * Scale * k;
            var s = Math.Max(sigma * Scale * k, 1e-3f);
            var dist = RoundedRectDistance(x - centre, y - centre, BoardPx * 0.5f, BoardPx * 0.5f, 28f * Scale);
            var alpha = opacity * (Phi((dist + w * 0.5f) / s) - Phi((dist - w * 0.5f) / s));
            if (clipped)
                alpha *= Clamp01(0.5f - dist); // обрізано панеллю, зі згладженням краю в піксель
            return Clamp01(alpha);
        }

        /// <summary>Положення на діагональному градієнті 0 (лівий верхній кут панелі) … 1 (правий нижній).</summary>
        public static float GradientT(float x, float y) =>
            Clamp01(((x - MarginPx) / BoardPx + (y - MarginPx) / BoardPx) * 0.5f);

        /// <summary>Знакова відстань до заокругленого прямокутника з центром у 0; від'ємна — усередині.</summary>
        public static float RoundedRectDistance(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            var qx = Math.Abs(x) - (halfWidth - radius);
            var qy = Math.Abs(y) - (halfHeight - radius);
            var ox = qx > 0f ? qx : 0f;
            var oy = qy > 0f ? qy : 0f;
            return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f) - radius;
        }

        /// <summary>Функція розподілу стандартного нормального закону.</summary>
        public static float Phi(float z) => 0.5f * (1f + Erf(z * 0.70710678f));

        /// <summary>Функція помилок, наближення Абрамовіца–Стіган 7.1.26 (похибка ≤ 1.5·10⁻⁷).</summary>
        public static float Erf(float x)
        {
            var sign = x < 0f ? -1f : 1f;
            x = Math.Abs(x);
            var t = 1f / (1f + 0.3275911f * x);
            var y = 1f - (((((1.061405429f * t - 1.453152027f) * t) + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f) * t
                * (float)Math.Exp(-x * x);
            return sign * y;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
