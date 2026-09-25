namespace InkFlow.Core
{
    /// <summary>Рідкість картинки (документ §6): шість рівнів, від 45 % до 1 %.</summary>
    public enum Rarity : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Cosmic = 5
    }

    public static class Rarities
    {
        public const int Count = 6;

        public static readonly Rarity[] All =
        {
            Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary, Rarity.Cosmic
        };

        /// <summary>Ідентифікатор у файлах картинок і збереженні: латиницею, без індексів.</summary>
        public static string IdOf(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => "uncommon",
            Rarity.Rare => "rare",
            Rarity.Epic => "epic",
            Rarity.Legendary => "legendary",
            Rarity.Cosmic => "cosmic",
            _ => "common"
        };

        public static bool TryParse(string? id, out Rarity rarity)
        {
            rarity = Rarity.Common;
            if (id is null)
                return false;
            switch (id.Trim().ToLowerInvariant())
            {
                case "common": rarity = Rarity.Common; return true;
                case "uncommon": rarity = Rarity.Uncommon; return true;
                case "rare": rarity = Rarity.Rare; return true;
                case "epic": rarity = Rarity.Epic; return true;
                case "legendary": rarity = Rarity.Legendary; return true;
                case "cosmic": rarity = Rarity.Cosmic; return true;
                default: return false;
            }
        }
    }
}
