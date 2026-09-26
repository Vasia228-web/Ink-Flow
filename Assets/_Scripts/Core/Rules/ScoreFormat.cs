using System;
using System.Globalization;
using System.Text;

namespace InkFlow.Core
{
    /// <summary>
    /// Рахунок і рекорд на екрані (документ §11): розряди відділяються вузьким пробілом;
    /// коли повне число не вміщається в колонку навіть найменшим читабельним шрифтом —
    /// компактний формат (1,2 млн · 987 млн · 1,2 млрд). Цифри однакової ширини (табличні),
    /// тож ширина не стрибає під час нарахування; ширину рахує це правило, а не око:
    /// у тестах воно перевіряється для будь-яких значень до 999 999 999 на найвужчій колонці.
    /// </summary>
    public static class ScoreFormat
    {
        /// <summary>Вузький пробіл між розрядами (U+2009). Є в Nunito.</summary>
        public const char ThinSpace = ' ';

        /// <summary>Ширина табличної цифри в em — стільки займає кожна цифра при моноширинному наборі.</summary>
        public const float DigitEm = 0.62f;

        /// <summary>Ширина вузького пробілу в em.</summary>
        public const float ThinSpaceEm = 0.22f;

        /// <summary>Ширина літери суфікса («млн», «млрд») в em — з запасом.</summary>
        public const float LetterEm = 0.62f;

        /// <summary>Повний запис: 1 234 567.</summary>
        public static string Full(long value)
        {
            var negative = value < 0;
            var digits = Math.Abs(value).ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder(digits.Length + 4);
            if (negative)
                sb.Append('−');
            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    sb.Append(ThinSpace);
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        /// <summary>Компактний запис: до 999 999 — повний; далі 1,2 млн; 12 млн; 987 млн; 1,2 млрд.</summary>
        public static string Compact(long value)
        {
            var abs = Math.Abs(value);
            if (abs < 1_000_000)
                return Full(value);
            var sign = value < 0 ? "−" : string.Empty;
            // Одиниця — за ОКРУГЛЕНИМ значенням: 999 999 999 — це «1,0 млрд», а не «1000 млн».
            if (abs + 500_000 < 1_000_000_000)
                return sign + Scaled(abs, 1_000_000) + ThinSpace + "млн";
            return sign + Scaled(abs, 1_000_000_000) + ThinSpace + "млрд";
        }

        /// <summary>1,2 — одна десяткова до 10; далі ціле (12; 987). Округлення до найближчого.</summary>
        private static string Scaled(long abs, long unit)
        {
            var tenths = (abs * 10 + unit / 2) / unit; // округлено до десятих
            if (tenths < 100)
                return (tenths / 10).ToString(CultureInfo.InvariantCulture) + "," + (tenths % 10).ToString(CultureInfo.InvariantCulture);
            var whole = (abs + unit / 2) / unit;
            return whole.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Скільки em займає рядок при табличних цифрах.</summary>
        public static float WidthEm(string text)
        {
            var w = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == ThinSpace) w += ThinSpaceEm;
                else if (c >= '0' && c <= '9') w += DigitEm;
                else if (c == ',') w += 0.3f;
                else w += LetterEm;
            }
            return w;
        }

        /// <summary>
        /// Найбільший розмір шрифту, при якому число вміщається в колонку: спершу повний запис
        /// від <paramref name="maxFont"/> до <paramref name="minFont"/>, далі — компактний.
        /// </summary>
        public static string Fit(long value, float columnWidth, float maxFont, float minFont, out float fontSize)
        {
            if (columnWidth <= 0f || maxFont <= 0f)
                throw new ArgumentOutOfRangeException(nameof(columnWidth));
            if (minFont <= 0f || minFont > maxFont)
                minFont = maxFont;

            var full = Full(value);
            var needed = WidthEm(full);
            if (needed * maxFont <= columnWidth)
            {
                fontSize = maxFont;
                return full;
            }
            if (needed * minFont <= columnWidth)
            {
                fontSize = columnWidth / needed;
                return full;
            }

            var compact = Compact(value);
            var compactNeeded = WidthEm(compact);
            fontSize = Math.Max(minFont, Math.Min(maxFont, columnWidth / compactNeeded));
            return compact;
        }
    }
}
