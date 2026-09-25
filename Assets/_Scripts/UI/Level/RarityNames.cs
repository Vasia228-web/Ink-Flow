using InkFlow.Core;

namespace InkFlow.UI
{
    /// <summary>Назви рідкості для написів (§6). Core назв не знає.</summary>
    public static class RarityNames
    {
        public static string Of(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => "НЕЗВИЧАЙНА",
            Rarity.Rare => "РІДКІСНА",
            Rarity.Epic => "ЕПІЧНА",
            Rarity.Legendary => "ЛЕГЕНДАРНА",
            Rarity.Cosmic => "КОСМІЧНА",
            _ => "ЗВИЧАЙНА"
        };

        /// <summary>«1 КОЛІР · 2 КОЛЬОРИ · 5 КОЛЬОРІВ» — для картки перед забігом.</summary>
        public static string Colors(int count)
        {
            var last = count % 10;
            var tens = count % 100;
            if (last == 1 && tens != 11)
                return $"{count} КОЛІР";
            if (last >= 2 && last <= 4 && (tens < 12 || tens > 14))
                return $"{count} КОЛЬОРИ";
            return $"{count} КОЛЬОРІВ";
        }
    }
}
