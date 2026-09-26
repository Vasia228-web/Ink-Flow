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
    /// яскраві, але м'які — плюс світлий і тіньовий тон кожного ігрового кольору (31+).
    /// Усі картинки беруть кольори лише звідси, тож гармонують між собою. Індекс 0 —
    /// порожньо. Порядок ЗАФІКСОВАНО: номери живуть у файлах картинок.
    ///
    /// Ігровий колір — той, у який фарбуються фігури (основний тон родини, §3); тони
    /// лише малюють картинку.
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
            new PaletteEntry(30, "морська піна", "#79E8A8", true),
            // Тони (§3): світлий і тіньовий відтінок кожного ігрового кольору. Ними малюють
            // картинки, фігури в них не фарбуються — тому не ігрові. Пари йдуть у порядку
            // ігрових кольорів 4..30: тон 31 + 2·(i − 4) — світло, +1 — тінь.
            new PaletteEntry(31, "білий · світло", "#FFFFFF", false),
            new PaletteEntry(32, "білий · тінь", "#C9CCD8", false),
            new PaletteEntry(33, "вершки · світло", "#FFFBF4", false),
            new PaletteEntry(34, "вершки · тінь", "#C7BFA6", false),
            new PaletteEntry(35, "срібло · світло", "#E7E9EF", false),
            new PaletteEntry(36, "срібло · тінь", "#8A8DA0", false),
            new PaletteEntry(37, "сланець · світло", "#B4B8C6", false),
            new PaletteEntry(38, "сланець · тінь", "#55576F", false),
            new PaletteEntry(39, "корал · світло", "#FFB2B9", false),
            new PaletteEntry(40, "корал · тінь", "#A8273C", false),
            new PaletteEntry(41, "апельсин · світло", "#FFC7A2", false),
            new PaletteEntry(42, "апельсин · тінь", "#B4601A", false),
            new PaletteEntry(43, "бурштин · світло", "#FFE1A6", false),
            new PaletteEntry(44, "бурштин · тінь", "#B98A18", false),
            new PaletteEntry(45, "лимон · світло", "#FFF7BF", false),
            new PaletteEntry(46, "лимон · тінь", "#BDB244", false),
            new PaletteEntry(47, "лайм · світло", "#E2FAAC", false),
            new PaletteEntry(48, "лайм · тінь", "#6F9C24", false),
            new PaletteEntry(49, "зелень · світло", "#A3EABB", false),
            new PaletteEntry(50, "зелень · тінь", "#268F55", false),
            new PaletteEntry(51, "бірюза · світло", "#8ADACA", false),
            new PaletteEntry(52, "бірюза · тінь", "#12857B", false),
            new PaletteEntry(53, "ціан · світло", "#B7FBFF", false),
            new PaletteEntry(54, "ціан · тінь", "#2F8C9E", false),
            new PaletteEntry(55, "небо · світло", "#B0D5FF", false),
            new PaletteEntry(56, "небо · тінь", "#2A5AB0", false),
            new PaletteEntry(57, "барвінок · світло", "#C1C0FF", false),
            new PaletteEntry(58, "барвінок · тінь", "#4F42B0", false),
            new PaletteEntry(59, "фіалка · світло", "#D8C0FF", false),
            new PaletteEntry(60, "фіалка · тінь", "#6742B0", false),
            new PaletteEntry(61, "маджента · світло", "#FFC0FA", false),
            new PaletteEntry(62, "маджента · тінь", "#A33EA1", false),
            new PaletteEntry(63, "рожевий · світло", "#FFD1E6", false),
            new PaletteEntry(64, "рожевий · тінь", "#B05C88", false),
            new PaletteEntry(65, "персик · світло", "#FFDDC7", false),
            new PaletteEntry(66, "персик · тінь", "#B47F4E", false),
            new PaletteEntry(67, "карамель · світло", "#F4D5B2", false),
            new PaletteEntry(68, "карамель · тінь", "#997232", false),
            new PaletteEntry(69, "кора · світло", "#C5A890", false),
            new PaletteEntry(70, "кора · тінь", "#74522A", false),
            new PaletteEntry(71, "пісок · світло", "#FFF6E1", false),
            new PaletteEntry(72, "пісок · тінь", "#A5996F", false),
            new PaletteEntry(73, "крига · світло", "#D1EEFF", false),
            new PaletteEntry(74, "крига · тінь", "#5C86AE", false),
            new PaletteEntry(75, "м'ята · світло", "#E5FFEF", false),
            new PaletteEntry(76, "м'ята · тінь", "#79A991", false),
            new PaletteEntry(77, "рум'янець · світло", "#FFE5EC", false),
            new PaletteEntry(78, "рум'янець · тінь", "#B07C8E", false),
            new PaletteEntry(79, "олива · світло", "#CDCC87", false),
            new PaletteEntry(80, "олива · тінь", "#767D18", false),
            new PaletteEntry(81, "малина · світло", "#FA9BCE", false),
            new PaletteEntry(82, "малина · тінь", "#9C1262", false),
            new PaletteEntry(83, "морська піна · світло", "#C4FFDD", false),
            new PaletteEntry(84, "морська піна · тінь", "#449F75", false)
        };

        /// <summary>Перший індекс тонів: 1–3 контурні, 4–30 ігрові, далі — світло/тінь.</summary>
        public const byte FirstTone = 31;
        public const byte FirstFill = 4;
        public const byte LastFill = 30;

        /// <summary>Чи це тон (світло або тінь ігрового кольору), а не основний колір і не контур.</summary>
        public static bool IsTone(byte index) => index >= FirstTone && index < Entries.Length;

        /// <summary>Світлий тон ігрового кольору; для не-ігрових — сам колір.</summary>
        public static byte LightOf(byte fill) => fill >= FirstFill && fill <= LastFill ? (byte)(FirstTone + 2 * (fill - FirstFill)) : fill;

        /// <summary>Тіньовий тон ігрового кольору; для не-ігрових — сам колір.</summary>
        public static byte ShadowOf(byte fill) => fill >= FirstFill && fill <= LastFill ? (byte)(FirstTone + 2 * (fill - FirstFill) + 1) : fill;

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
