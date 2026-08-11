using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>Тір фарби в магазині: за ним групуються секції.</summary>
    public enum PaintTier
    {
        Basic = 0,
        Rich = 1,
        Gradient = 2,
        Special = 3
    }

    /// <summary>Як виглядає куля фарби. Спец-ефекти отримують яскравішу рамку картки.</summary>
    public enum PaintFinish
    {
        Solid = 0,
        Gradient = 1,
        Metal = 2,
        Pearl = 3,
        Neon = 4,
        Cosmos = 5
    }

    /// <summary>Товар-фарба: ідентичність, ціна за літр і скільки її вже є в гравця.</summary>
    public sealed class PaintProduct
    {
        public PaintProduct(string id, string name, PaintTier tier, PaintFinish finish,
            Rgb primary, Rgb secondary, int pricePerLiter, PaintKind feeds, float ownedLiters = 0f)
        {
            Id = id;
            Name = name;
            Tier = tier;
            Finish = finish;
            Primary = primary;
            Secondary = secondary;
            PricePerLiter = pricePerLiter;
            Feeds = feeds;
            OwnedLiters = ownedLiters;
        }

        public string Id { get; }
        public string Name { get; }
        public PaintTier Tier { get; }
        public PaintFinish Finish { get; }
        public Rgb Primary { get; }

        /// <summary>Другий колір градієнта. Для суцільних дорівнює першому.</summary>
        public Rgb Secondary { get; }

        public int PricePerLiter { get; }

        /// <summary>
        /// Яку фарбу палітри поповнює покупка.
        ///
        /// Товарів у магазині дев'ятнадцять, а зони фарбуються вісьмома
        /// <see cref="PaintKind"/> — без цього зв'язку куплений літр не потрапляв
        /// би НІКУДИ, і цикл «купую → фарбую» існував би лише на словах.
        /// Кілька товарів свідомо ведуть в один тон: у магазині вони різні
        /// назвою й ціною, у палітрі — той самий колір.
        /// </summary>
        public PaintKind Feeds { get; }
        public float OwnedLiters { get; set; }

        /// <summary>Спец-ефекти виділяються рамкою й підсвіткою картки.</summary>
        public bool IsSpecial => Finish >= PaintFinish.Metal;

        public bool IsEmpty => OwnedLiters <= 0.001f;
    }

    /// <summary>Секція магазину: заголовок, пояснення дрібним і товари.</summary>
    public sealed class PaintSection
    {
        public PaintSection(string title, string subtitle, IReadOnlyList<PaintProduct> items)
        {
            Title = title;
            Subtitle = subtitle;
            Items = items;
        }

        public string Title { get; }
        public string Subtitle { get; }
        public IReadOnlyList<PaintProduct> Items { get; }
    }

    /// <summary>Пакет нафти за реальні гроші.</summary>
    public sealed class OilPack
    {
        public OilPack(string id, long amount, string price, float dropSize, string? badge, bool hot)
        {
            Id = id;
            Amount = amount;
            Price = price;
            DropSize = dropSize;
            Badge = badge;
            Hot = hot;
        }

        public string Id { get; }
        public long Amount { get; }

        /// <summary>Ціна рядком: валюта приходить із крамниці платформи, не з гри.</summary>
        public string Price { get; }

        /// <summary>Розмір краплі в px макета: чим більший пакет, тим більша крапля.</summary>
        public float DropSize { get; }

        public string? Badge { get; }

        /// <summary>«Гарячий» пакет світиться теплим і має інший бейдж.</summary>
        public bool Hot { get; }
    }

    /// <summary>Комплект: фарби плюс нафта разом, дешевше.</summary>
    public sealed class ShopBundle
    {
        public ShopBundle(string id, string name, string description, string oldPrice,
            string price, string saving, IReadOnlyList<Rgb> colors)
        {
            Id = id;
            Name = name;
            Description = description;
            OldPrice = oldPrice;
            Price = price;
            Saving = saving;
            Colors = colors;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string OldPrice { get; }
        public string Price { get; }
        public string Saving { get; }
        public IReadOnlyList<Rgb> Colors { get; }
    }

    /// <summary>Фарба тижня: та сама фарба, але зі знижкою й таймером.</summary>
    public sealed class WeeklyOffer
    {
        public WeeklyOffer(PaintProduct paint, int oldPrice, int price, int daysLeft)
        {
            Paint = paint;
            OldPrice = oldPrice;
            Price = price;
            DaysLeft = daysLeft;
        }

        public PaintProduct Paint { get; }
        public int OldPrice { get; }
        public int Price { get; }
        public int DaysLeft { get; }
    }

    /// <summary>
    /// Асортимент магазину й покупка за нафту.
    ///
    /// Знижки за обсяг живуть тут, а не в UI: інакше «−10%» на кнопці й списана
    /// сума розійшлись би, і помітив би це лише гравець.
    /// </summary>
    public sealed class ShopCatalog
    {
        /// <summary>Мензурка повна на цій кількості літрів (CAP у макеті).</summary>
        public const float BeakerCapacity = 10f;

        public ShopCatalog(IReadOnlyList<PaintSection> sections, WeeklyOffer weekly,
            IReadOnlyList<OilPack> oilPacks, IReadOnlyList<ShopBundle> bundles)
        {
            Sections = sections;
            Weekly = weekly;
            OilPacks = oilPacks;
            Bundles = bundles;
        }

        public IReadOnlyList<PaintSection> Sections { get; }
        public WeeklyOffer Weekly { get; }
        public IReadOnlyList<OilPack> OilPacks { get; }
        public IReadOnlyList<ShopBundle> Bundles { get; }

        /// <summary>Щось куплено — екран перечитує картки й гаманець.</summary>
        public event Action<PaintProduct, int>? Purchased;

        /// <summary>Доступні обсяги покупки й знижка за кожен.</summary>
        public static readonly (int Liters, float Discount, string? Badge)[] Quantities =
        {
            (1, 0f, null),
            (5, 0.05f, "−5%"),
            (10, 0.10f, "−10%")
        };

        /// <summary>Скільки коштує стільки-то літрів із урахуванням знижки за обсяг.</summary>
        public static long PriceFor(PaintProduct paint, int liters)
        {
            var discount = 0f;
            for (var i = 0; i < Quantities.Length; i++)
                if (Quantities[i].Liters == liters)
                    discount = Quantities[i].Discount;

            return (long)Math.Round(paint.PricePerLiter * liters * (1f - discount));
        }

        /// <summary>
        /// Купує літри за нафту. Повертає false, якщо не вистачає — і не змінює
        /// нічого: ані гаманця, ані запасу.
        /// </summary>
        /// <summary>
        /// Покупка. Якщо передано запас палітри — літри лягають і в нього:
        /// саме так куплена фарба стає доступною для фарбування зон.
        /// Невдала покупка не змінює НІЧОГО — ані гаманця, ані запасу.
        /// </summary>
        public bool Buy(PaintProduct paint, int liters, Wallet wallet, PaintStock? stock = null)
        {
            if (liters <= 0)
                return false;

            var cost = PriceFor(paint, liters);
            if (!wallet.TrySpend(cost))
                return false;

            paint.OwnedLiters += liters;
            stock?.Set(paint.Feeds, stock[paint.Feeds] + liters);
            Purchased?.Invoke(paint, liters);
            return true;
        }

        public PaintProduct? Find(string id)
        {
            for (var s = 0; s < Sections.Count; s++)
                for (var i = 0; i < Sections[s].Items.Count; i++)
                    if (string.Equals(Sections[s].Items[i].Id, id, StringComparison.Ordinal))
                        return Sections[s].Items[i];
            return null;
        }

        private static Rgb Hex(string hex) => Rgb.FromHex(hex);

        /// <summary>Асортимент рівно з макета (PAINT_SECTIONS, WEEK, OIL_PACKS, BUNDLES).</summary>
        public static ShopCatalog CreateMock()
        {
            var basic = new List<PaintProduct>
            {
                Solid("b_m", "Малина", PaintTier.Basic, "#FF2D8A", 12, PaintKind.Berry),
                Solid("b_t", "Бірюза", PaintTier.Basic, "#00D9C0", 12, PaintKind.Teal),
                Solid("b_a", "Бурштин", PaintTier.Basic, "#FFB300", 12, PaintKind.Sand),
                Solid("b_l", "Лайм", PaintTier.Basic, "#9BE636", 12, PaintKind.Forest),
                Solid("b_v", "Фіолет", PaintTier.Basic, "#9D4DFF", 12, PaintKind.Violet),
                Solid("b_b", "Кобальт", PaintTier.Basic, "#3B7BFF", 12, PaintKind.Ocean)
            };

            var rich = new List<PaintProduct>
            {
                Solid("r_w", "Вино", PaintTier.Rich, "#8E1E4D", 22, PaintKind.Berry),
                Solid("r_d", "Глибінь", PaintTier.Rich, "#0A6B7A", 22, PaintKind.Ocean),
                Solid("r_p", "Слива", PaintTier.Rich, "#6A2B9E", 22, PaintKind.Violet),
                Solid("r_pe", "Персик", PaintTier.Rich, "#FFB894", 20, PaintKind.Sand),
                Solid("r_mi", "М'ята", PaintTier.Rich, "#8FE8C0", 20, PaintKind.Teal),
                Solid("r_li", "Бузок", PaintTier.Rich, "#CBB0FF", 20, PaintKind.Ice)
            };

            var ocean = Gradient("g_oce", "Океан", "#00D9C0", "#3B7BFF", 48, PaintKind.Ocean);
            var gradients = new List<PaintProduct>
            {
                Gradient("g_sun", "Захід сонця", "#FF2D8A", "#FFB300", 48, PaintKind.Lava),
                ocean,
                Gradient("g_neb", "Туманність", "#9D4DFF", "#FF5BB0", 48, PaintKind.Violet)
            };

            var special = new List<PaintProduct>
            {
                new PaintProduct("s_met", "Металік", PaintTier.Special, PaintFinish.Metal,
                    Hex("#D8B25A"), Hex("#F4DD90"), 90, PaintKind.Sand),
                new PaintProduct("s_pea", "Перламутр", PaintTier.Special, PaintFinish.Pearl,
                    Hex("#E9DCFF"), Hex("#CDB6F0"), 90, PaintKind.Ice),
                new PaintProduct("s_neo", "Неон", PaintTier.Special, PaintFinish.Neon,
                    Hex("#00FFC3"), Hex("#00FFC3"), 120, PaintKind.Teal),
                new PaintProduct("s_cos", "Космос", PaintTier.Special, PaintFinish.Cosmos,
                    Hex("#9D4DFF"), Hex("#1C1246"), 200, PaintKind.Lava)
            };

            var sections = new List<PaintSection>
            {
                new PaintSection("Базові", "шість чистих кольорів", basic),
                new PaintSection("Насичені", "глибокі відтінки й пастель", rich),
                new PaintSection("Градієнти", "плавні переливи", gradients),
                new PaintSection("Спец-ефекти", "престиж і сяйво", special)
            };

            var oilPacks = new List<OilPack>
            {
                new OilPack("op1", 500, "$1.99", 46f, null, false),
                new OilPack("op2", 1200, "$3.99", 58f, "Популярне", true),
                new OilPack("op3", 3000, "$8.99", 72f, "+20% бонус", false),
                new OilPack("op4", 6500, "$16.99", 88f, null, false)
            };

            var bundles = new List<ShopBundle>
            {
                new ShopBundle("bd1", "Стартовий набір", "6 базових фарб · по 3 л", "$6.99", "$3.99", "−45%",
                    new[]
                    {
                        Hex("#FF2D8A"), Hex("#00D9C0"), Hex("#FFB300"),
                        Hex("#9BE636"), Hex("#9D4DFF"), Hex("#3B7BFF")
                    }),
                new ShopBundle("bd2", "Набір «Туманність»", "3 градієнти + 500 нафти", "$12.99", "$8.99", "−30%",
                    new[] { Hex("#9D4DFF"), Hex("#00D9C0"), Hex("#FF2D8A") })
            };

            return new ShopCatalog(sections, new WeeklyOffer(ocean, 48, 34, 3), oilPacks, bundles);
        }

        private static PaintProduct Solid(string id, string name, PaintTier tier,
            string hex, int price, PaintKind feeds, float owned = 0f) =>
            new PaintProduct(id, name, tier, PaintFinish.Solid, Hex(hex), Hex(hex), price, feeds, owned);

        private static PaintProduct Gradient(string id, string name,
            string from, string to, int price, PaintKind feeds, float owned = 0f) =>
            new PaintProduct(id, name, PaintTier.Gradient, PaintFinish.Gradient,
                Hex(from), Hex(to), price, feeds, owned);
    }
}
