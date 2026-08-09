using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>Стан сходинки в драбині звань.</summary>
    public enum RankStepState
    {
        /// <summary>Пройдене — кружок із галочкою, приглушений.</summary>
        Achieved = 0,

        /// <summary>Поточне — зірка й світіння.</summary>
        Current = 1,

        /// <summary>Наступне — темний кружок і жовтий підпис, скільки бракує.</summary>
        Next = 2,

        /// <summary>Далі — найтьмяніше.</summary>
        Locked = 3
    }

    /// <summary>Одна сходинка драбини звань.</summary>
    public sealed class RankStep
    {
        public RankStep(string title, RankStepState state, string? subtitle = null)
        {
            Title = title;
            State = state;
            Subtitle = subtitle;
        }

        public string Title { get; }
        public RankStepState State { get; }

        /// <summary>«Твоє звання зараз» або «ще 3 планети». null — підпису немає.</summary>
        public string? Subtitle { get; }
    }

    /// <summary>Плитка статистики: число, підпис і колір числа.</summary>
    public sealed class ProfileStat
    {
        public ProfileStat(string value, string label, Rgb color, bool star = false)
        {
            Value = value;
            Label = label;
            Color = color;
            Star = star;
        }

        public string Value { get; }
        public string Label { get; }
        public Rgb Color { get; }

        /// <summary>Перед числом стоїть ★ — так у макеті виглядають зірки рівнів.</summary>
        public bool Star { get; }
    }

    /// <summary>Досягнення: кольоровий бейдж або темний силует із замком.</summary>
    public sealed class Achievement
    {
        public Achievement(string id, string name, Rgb color, bool unlocked, string requirement)
        {
            Id = id;
            Name = name;
            Color = color;
            Unlocked = unlocked;
            Requirement = requirement;
        }

        public string Id { get; }
        public string Name { get; }
        public Rgb Color { get; }
        public bool Unlocked { get; }

        /// <summary>Умова отримання — показується тапом по замкненому бейджу.</summary>
        public string Requirement { get; }
    }

    /// <summary>Планета у вітрині: показується великою або мініатюрою.</summary>
    public sealed class ShowcasePlanet
    {
        public ShowcasePlanet(string id, string name, PlanetType type)
        {
            Id = id;
            Name = name;
            Type = type;
        }

        public string Id { get; }
        public string Name { get; }
        public PlanetType Type { get; }
    }

    /// <summary>
    /// Профіль гравця: візитка, драбина звань, статистика, палітра, вітрина
    /// й досягнення.
    ///
    /// Запас фарб бере готовий <see cref="PaintStock"/> — той самий, що на екрані
    /// фарбування. Другого джерела літрів у грі бути не може.
    /// </summary>
    public sealed class PlayerProfile
    {
        public PlayerProfile(string nick, string rankTitle, long oil, PaintStock paints,
            IReadOnlyList<RankStep> ladder, IReadOnlyList<ProfileStat> stats,
            IReadOnlyList<ShowcasePlanet> showcase, IReadOnlyList<Achievement> achievements,
            float litersSpent)
        {
            Nick = nick;
            RankTitle = rankTitle;
            Oil = oil;
            Paints = paints;
            Ladder = ladder;
            Stats = stats;
            Showcase = showcase;
            Achievements = achievements;
            LitersSpent = litersSpent;
            FavouriteIndex = 0;
        }

        public string Nick { get; }
        public string RankTitle { get; }
        public long Oil { get; }
        public PaintStock Paints { get; }
        public IReadOnlyList<RankStep> Ladder { get; }
        public IReadOnlyList<ProfileStat> Stats { get; }
        public IReadOnlyList<ShowcasePlanet> Showcase { get; }
        public IReadOnlyList<Achievement> Achievements { get; }

        /// <summary>Скільки літрів витрачено за весь час.</summary>
        public float LitersSpent { get; }

        /// <summary>Яка планета зараз у вітрині. Міняється тапом по мініатюрі.</summary>
        public int FavouriteIndex { get; set; }

        public ShowcasePlanet? Favourite =>
            FavouriteIndex >= 0 && FavouriteIndex < Showcase.Count ? Showcase[FavouriteIndex] : null;

        /// <summary>Порядок фарб у палітрі — той самий, що на екрані фарбування.</summary>
        public static readonly PaintKind[] PaletteOrder =
        {
            PaintKind.Ocean, PaintKind.Teal, PaintKind.Forest, PaintKind.Ice,
            PaintKind.Sand, PaintKind.Lava, PaintKind.Berry, PaintKind.Violet
        };

        private static Rgb Hex(string hex) => Rgb.FromHex(hex);

        /// <summary>Профіль рівно з еталонних скріншотів.</summary>
        public static PlayerProfile CreateMock()
        {
            var ladder = new List<RankStep>
            {
                new RankStep("Учень", RankStepState.Achieved),
                new RankStep("Колорист", RankStepState.Achieved),
                new RankStep("Художниця галактик", RankStepState.Current, "Твоє звання зараз"),
                new RankStep("Майстер кольору", RankStepState.Next, "ще 3 планети"),
                new RankStep("Легенда", RankStepState.Locked)
            };

            var stats = new List<ProfileStat>
            {
                new ProfileStat("23", "Планет розфарбовано", Hex("#00D9C0")),
                new ProfileStat("2", "Галактик завершено", Hex("#9D4DFF")),
                new ProfileStat("8 420", "Рекорд · Нескінченний", Hex("#FFB300")),
                new ProfileStat("127", "Зірок у рівнях", Hex("#9BE636"), star: true)
            };

            var showcase = new List<ShowcasePlanet>
            {
                new ShowcasePlanet("p0", "Аквіла", PlanetType.Ocean),
                new ShowcasePlanet("p1", "Рудокам", PlanetType.Rocky),
                new ShowcasePlanet("p2", "Кріос", PlanetType.Ice)
            };

            var achievements = new List<Achievement>
            {
                new Achievement("a_gal", "Перша галактика", Hex("#00D9C0"), true,
                    "Заверши свою першу галактику"),
                new Achievement("a_lit", "100 літрів", Hex("#FFB300"), true,
                    "Витрать 100 літрів фарби"),
                new Achievement("a_col", "Колекціонер", Hex("#9D4DFF"), true,
                    "Збери всі базові фарби"),
                new Achievement("a_top", "Топ-100 тижня", Hex("#3B7BFF"), false,
                    "Увійди в сотню найкращих за тиждень")
            };

            return new PlayerProfile(
                "Нова", "Художниця галактик", 1250, PaintStock.CreateMock(),
                ladder, stats, showcase, achievements, 340f);
        }
    }
}
