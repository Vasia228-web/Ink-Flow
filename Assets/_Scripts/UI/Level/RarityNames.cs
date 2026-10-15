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
