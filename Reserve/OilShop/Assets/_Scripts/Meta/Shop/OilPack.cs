using System;

namespace InkFlow.Meta
{
    /// <summary>
    /// Пакет нафти за реальні гроші (майстер-док §13). Чотири пакети живуть у `EconomyConfig`;
    /// ціни тут НЕМАЄ — вона приходить локалізованим рядком зі стору через `IIapService`, і
    /// зашита в гру ціна рано чи пізно розійшлася б із тим, що стор справді списує.
    /// </summary>
    public sealed class OilPack
    {
        public OilPack(string id, long amount, string? badge = null, bool hot = false)
        {
            if (id is null || id.Length == 0)
                throw new ArgumentException("Порожній ідентифікатор товару.", nameof(id));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Id = id;
            Amount = amount;
            Badge = badge is { Length: > 0 } ? badge : null;
            Hot = hot;
        }

        /// <summary>Ідентифікатор товару в сторі: oil_1000, oil_10000, oil_100000, oil_500000.</summary>
        public string Id { get; }

        /// <summary>Скільки нафти дає покупка.</summary>
        public long Amount { get; }

        /// <summary>Напис на бейджі («Популярне», «Найвигідніше»); null — без бейджа.</summary>
        public string? Badge { get; }

        /// <summary>«Гарячий» пакет світиться теплим і має теплий бейдж.</summary>
        public bool Hot { get; }

        /// <summary>Чотири пакети майстер-доку §13 — стан нового проєкту, якщо асет загубився.</summary>
        public static OilPack[] Defaults() => new[]
        {
            new OilPack("oil_1000", 1_000),
            new OilPack("oil_10000", 10_000, "Популярне", hot: true),
            new OilPack("oil_100000", 100_000, "Найвигідніше"),
            new OilPack("oil_500000", 500_000)
        };
    }
}
