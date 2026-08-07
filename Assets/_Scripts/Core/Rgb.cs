using System;
using System.Globalization;

namespace InkFlow.Core
{
    /// <summary>
    /// Колір без залежності від рушія.
    ///
    /// Потрібен, бо Core і Meta збираються без UnityEngine (див. карту збірок), а
    /// дані магазину все одно несуть колір товару. Тримати там
    /// <c>UnityEngine.Color</c> означало б, що модель більше не перевіриш
    /// headless-тестами — і саме на цьому тест-раннер і спіймав першу спробу.
    ///
    /// У в'юхах перетворюється на <c>Color</c> через <c>ToColor()</c> у Style.
    /// </summary>
    public readonly struct Rgb : IEquatable<Rgb>
    {
        public Rgb(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        /// <summary>
        /// Парсить «#RRGGBB» або «#RRGGBBAA». На нерозпізнаному повертає явно
        /// помітну маджентову заглушку: тихо підставлений чорний загубився б
        /// на космічному фоні.
        /// </summary>
        public static Rgb FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return new Rgb(1f, 0f, 1f);

            var span = hex[0] == '#' ? hex.Substring(1) : hex;
            if (span.Length != 6 && span.Length != 8)
                return new Rgb(1f, 0f, 1f);

            if (!TryByte(span, 0, out var r) || !TryByte(span, 2, out var g) || !TryByte(span, 4, out var b))
                return new Rgb(1f, 0f, 1f);

            var a = 255;
            if (span.Length == 8 && !TryByte(span, 6, out a))
                return new Rgb(1f, 0f, 1f);

            return new Rgb(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static bool TryByte(string source, int offset, out int value) =>
            int.TryParse(source.Substring(offset, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out value);

        public bool Equals(Rgb other) =>
            R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);

        public override bool Equals(object? obj) => obj is Rgb other && Equals(other);

        public override int GetHashCode() =>
            (R.GetHashCode() * 397) ^ (G.GetHashCode() * 31) ^ (B.GetHashCode() * 17) ^ A.GetHashCode();

        public override string ToString() =>
            $"#{(int)(R * 255):X2}{(int)(G * 255):X2}{(int)(B * 255):X2}";
    }
}
