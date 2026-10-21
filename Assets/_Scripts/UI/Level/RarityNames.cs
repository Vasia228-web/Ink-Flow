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

        /// <summary>Родовий множини малими — «Ще немає космічних картинок» (S06Collection_03).</summary>
        public static string GenitivePlural(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => "незвичайних",
            Rarity.Rare => "рідкісних",
            Rarity.Epic => "епічних",
            Rarity.Legendary => "легендарних",
            Rarity.Cosmic => "космічних",
            _ => "звичайних"
        };

        /// <summary>Назва рідкості з великої — підпис чипа фільтра («Звичайна», S06).</summary>
        public static string Title(Rarity rarity)
        {
            var upper = Of(rarity);
            return upper.Substring(0, 1) + upper.Substring(1).ToLowerInvariant();
        }

        /// <summary>Знахідний відмінок малими — «Викинути епічну картинку?» (S02Run_04).</summary>
        public static string Accusative(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => "незвичайну",
            Rarity.Rare => "рідкісну",
            Rarity.Epic => "епічну",
            Rarity.Legendary => "легендарну",
            Rarity.Cosmic => "космічну",
            _ => "звичайну"
        };
    }
}
