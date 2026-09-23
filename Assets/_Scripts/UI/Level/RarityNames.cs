using InkFlow.Core;

namespace InkFlow.UI
{
    /// <summary>Назви рідкості для написів (§6). Core назв не знає.</summary>
    public static class RarityNames
    {
        public static string Of(Rarity rarity) => rarity switch
        {
            Rarity.Rare => "РІДКІСНА",
            Rarity.Legendary => "ЛЕГЕНДАРНА",
            _ => "ЗВИЧАЙНА"
        };
    }
}
