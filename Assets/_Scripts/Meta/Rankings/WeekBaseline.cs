using System;
using System.Globalization;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// «Цей тиждень» (§16) рахується локально: на початку тижня (понеділок 00:00 UTC) запам'ятовуємо
    /// лічильники, а значення тижня — приріст від них. Сервер (тижнева таблиця зі скиданням) отримує
    /// той самий приріст, тож картка «Ти» і таблиця кажуть одне й те саме. Без Unity, під headless-тестом.
    /// </summary>
    public static class WeekBaseline
    {
        private const string Format = "yyyy-MM-dd";

        /// <summary>Понеділок 00:00 UTC тижня, у який потрапляє <paramref name="utc"/>.</summary>
        public static DateTime StartOfWeek(DateTime utc)
        {
            var day = utc.Date;
            var back = ((int)day.DayOfWeek + 6) % 7; // Monday → 0, Sunday → 6
            return DateTime.SpecifyKind(day.AddDays(-back), DateTimeKind.Utc);
        }

        /// <summary>Ключ тижня у файлі — лише інваріантна культура (календар телефону тут ні до чого).</summary>
        public static string Key(DateTime weekStart) => weekStart.ToString(Format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Новий тиждень — нова база: лічильники на його початку. Повертає true, якщо базу оновлено
        /// (і файл варто записати). Перший виклик у житті файлу теж оновлює: до нього тижня не було.
        /// </summary>
        public static bool RollOver(RankWeekData data, DateTime utcNow, int planetsNow, int galaxiesNow)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            var key = Key(StartOfWeek(utcNow));
            if (string.Equals(data.WeekStartUtc, key, StringComparison.Ordinal))
                return false;
            data.WeekStartUtc = key;
            data.PlanetsAtWeekStart = planetsNow;
            data.GalaxiesAtWeekStart = galaxiesNow;
            return true;
        }

        /// <summary>Приріст за тиждень; не нижче нуля (конфіг міг зменшити розкладку).</summary>
        public static long WeekValue(RankWeekData data, RankMetric metric, int planetsNow, int galaxiesNow)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            var baseline = metric == RankMetric.Planets ? data.PlanetsAtWeekStart : data.GalaxiesAtWeekStart;
            var now = metric == RankMetric.Planets ? planetsNow : galaxiesNow;
            return Math.Max(0, now - baseline);
        }
    }
}
