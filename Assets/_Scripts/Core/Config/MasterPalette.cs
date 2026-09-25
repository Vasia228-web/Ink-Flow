using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Один колір майстер-палітри: номер, назва й чи можна ним фарбувати фігури.</summary>
    public readonly struct PaletteEntry
    {
        public PaletteEntry(byte index, string name, string hex, bool isFill)
        {
            Index = index;
            Name = name;
            Color = Rgb.FromHex(hex);
            IsFill = isFill;
        }

        public byte Index { get; }
        public string Name { get; }
        public Rgb Color { get; }

        /// <summary>
        /// Ігровий колір: ним фарбуються фігури на полі. Контурні (найтемніші) кольори
        /// не ігрові — темна фігура на темному полі не читається (документ §3).
        /// </summary>
        public bool IsFill { get; }
    }

    /// <summary>
    /// Майстер-палітра (документ §3): одна на всю гру, 30 кольорів у стилі піксель-арту —
    /// яскраві, але м'які. Усі картинки беруть кольори лише звідси, тож гармонують між
    /// собою. Індекс 0 — порожньо. Порядок ЗАФІКСОВАНО: номери живуть у файлах картинок.
    ///
    /// Кожен ігровий колір мусить читатись як фігура на темному полі: тест міряє
    /// контраст до <see cref="BoardBackground"/> за формулою WCAG, а не око.
    /// </summary>
    public static class MasterPalette
    {
        public const byte Empty = 0;

        /// <summary>Фон поля для тесту контрасту — темний низ космічного градієнта під скляною плитою.</summary>
        public static readonly Rgb BoardBackground = Rgb.FromHex("#14112A");

        /// <summary>Мінімальний контраст ігрового кольору до фону (WCAG для графіки — 3; беремо із запасом).</summary>
        public const float MinFillContrast = 4f;

        public static readonly PaletteEntry[] Entries =
        {
            new PaletteEntry(0, "порожньо", "#00000000", false),
            // Контурні — не ігрові.
            new PaletteEntry(1, "чорнило", "#1B1730", false),
            new PaletteEntry(2, "какао", "#3A2C2C", false),
            new PaletteEntry(3, "глибокий синій", "#26305A", false),
            // Ігрові.
            new PaletteEntry(4, "білий", "#FFFFFF", true),
            new PaletteEntry(5, "вершки", "#F3ECDD", true),
            new PaletteEntry(6, "срібло", "#C8CCD8", true),
            new PaletteEntry(7, "сланець", "#8E93A8", true),
            new PaletteEntry(8, "корал", "#FF5E6E", true),
            new PaletteEntry(9, "апельсин", "#FF8A3D", true),
            new PaletteEntry(10, "бурштин", "#FFC145", true),
            new PaletteEntry(11, "лимон", "#FFEE7A", true),
            new PaletteEntry(12, "лайм", "#B6E24F", true),
            new PaletteEntry(13, "зелень", "#4ED37A", true),
            new PaletteEntry(14, "бірюза", "#2EC4A6", true),
            new PaletteEntry(15, "ціан", "#5FE1E8", true),
            new PaletteEntry(16, "небо", "#5AA7FF", true),
            new PaletteEntry(17, "барвінок", "#7D7CFF", true),
            new PaletteEntry(18, "фіалка", "#AE7BFF", true),
            new PaletteEntry(19, "маджента", "#F075E6", true),
            new PaletteEntry(20, "рожевий", "#FF9FCB", true),
            new PaletteEntry(21, "персик", "#FFB98B", true),
            new PaletteEntry(22, "карамель", "#DDA25F", true),
            new PaletteEntry(23, "кора", "#B07A4E", true),
            new PaletteEntry(24, "пісок", "#E8D7B0", true),
            new PaletteEntry(25, "крига", "#9FDBFF", true),
            new PaletteEntry(26, "м'ята", "#C3F7D6", true),
            new PaletteEntry(27, "рум'янець", "#FFC8D8", true),
            new PaletteEntry(28, "олива", "#B8B534", true),
            new PaletteEntry(29, "малина", "#E2308F", true),
            new PaletteEntry(30, "морська піна", "#79E8A8", true)
        };

        public static int Count => Entries.Length;

        public static bool IsValid(byte index) => index < Entries.Length;

        public static Rgb ColorOf(byte index) => IsValid(index) ? Entries[index].Color : Entries[0].Color;

        public static string NameOf(byte index) => IsValid(index) ? Entries[index].Name : "?";

        public static bool IsFill(byte index) => IsValid(index) && Entries[index].IsFill;

        /// <summary>Усі ігрові індекси в порядку палітри.</summary>
        public static IReadOnlyList<byte> FillIndices { get; } = BuildFills();

        private static byte[] BuildFills()
        {
            var list = new List<byte>();
            for (var i = 0; i < Entries.Length; i++)
                if (Entries[i].IsFill)
                    list.Add(Entries[i].Index);
            return list.ToArray();
        }

        /// <summary>Відносна яскравість sRGB за WCAG 2.x.</summary>
        public static float RelativeLuminance(Rgb color)
        {
            static float Channel(float c) => c <= 0.03928f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4);
            return 0.2126f * Channel(color.R) + 0.7152f * Channel(color.G) + 0.0722f * Channel(color.B);
        }

        /// <summary>Контраст двох кольорів за WCAG: (світліший + 0.05) / (темніший + 0.05).</summary>
        public static float ContrastRatio(Rgb a, Rgb b)
        {
            var la = RelativeLuminance(a);
            var lb = RelativeLuminance(b);
            var light = Math.Max(la, lb);
            var dark = Math.Min(la, lb);
            return (light + 0.05f) / (dark + 0.05f);
        }

        /// <summary>Контраст кольору до фону поля — те, що перевіряє тест палітри.</summary>
        public static float ContrastToBoard(byte index) => ContrastRatio(ColorOf(index), BoardBackground);
    }
}
