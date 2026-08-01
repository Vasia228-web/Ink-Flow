namespace InkFlow.Core
{
    /// <summary>
    /// Кольори чорнила. None — порожня клітинка (архітектура §4).
    /// Значення стабільні: вони потрапляють у збереження і в RunReplay,
    /// тому переставляти або вставляти всередину заборонено — тільки дописувати в кінець.
    /// </summary>
    public enum InkColor : byte
    {
        None = 0,
        Magenta = 1,
        Cyan = 2,
        Amber = 3,
        Lime = 4,
        Violet = 5,
        Rose = 6
    }

    public static class InkColors
    {
        /// <summary>Усі кольори, крім None — у порядку оголошення.</summary>
        public static readonly InkColor[] All =
        {
            InkColor.Magenta, InkColor.Cyan, InkColor.Amber,
            InkColor.Lime, InkColor.Violet, InkColor.Rose
        };

        public const int MaxCount = 6;

        /// <summary>Колір за індексом палітри рівня (0..colorsCount-1).</summary>
        public static InkColor FromIndex(int index) => All[index % All.Length];
    }
}
