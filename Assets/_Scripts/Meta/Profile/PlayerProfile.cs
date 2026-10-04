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
    /// Профіль гравця: візитка, драбина звань, статистика, вітрина й досягнення.
    /// Драбина, статистика, вітрина й бейджі зникнуть у Фазі 6 (майстер-док §14) — лишаться
    /// аватар, нік і вітринна картинка.
    /// </summary>
    public sealed class PlayerProfile
    {
        public PlayerProfile(string nick, string rankTitle, long oil,
            IReadOnlyList<RankStep> ladder, IReadOnlyList<ProfileStat> stats,
            IReadOnlyList<ShowcasePlanet> showcase, IReadOnlyList<Achievement> achievements)
        {
            Nick = nick;
            RankTitle = rankTitle;
            Oil = oil;
            Ladder = ladder;
            Stats = stats;
            Showcase = showcase;
            Achievements = achievements;
            FavouriteIndex = 0;
        }

        public string Nick { get; }
        public string RankTitle { get; }
        public long Oil { get; }
        public IReadOnlyList<RankStep> Ladder { get; }
        public IReadOnlyList<ProfileStat> Stats { get; }
        public IReadOnlyList<ShowcasePlanet> Showcase { get; }
        public IReadOnlyList<Achievement> Achievements { get; }

        /// <summary>Яка планета зараз у вітрині. Міняється тапом по мініатюрі.</summary>
        public int FavouriteIndex { get; set; }

        public ShowcasePlanet? Favourite =>
            FavouriteIndex >= 0 && FavouriteIndex < Showcase.Count ? Showcase[FavouriteIndex] : null;

        private static Rgb Hex(string hex) => Rgb.FromHex(hex);

        /// <summary>
        /// Профіль із РЕАЛЬНОГО стану гравця.
        ///
        /// Драбина звань, статистика й вітрина рахуються, а не зберігаються:
        /// це похідні від слотів планет і колекції. Окреме поле «звання» у файлі
        /// рано чи пізно розійшлося б із фактичною кількістю ожилих планет.
        /// </summary>
        public static PlayerProfile FromState(PlayerState state)
        {
            var planetsDone = GalaxyState.CompletedPlanets(state.Galaxy, state.Layout);
            var galaxiesDone = GalaxyState.CompletedGalaxies(state.Galaxy, state.Layout);
            var rankIndex = PlayerRanks.IndexFor(planetsDone);

            var ladder = new List<RankStep>(PlayerRanks.Ladder.Length);
            for (var i = 0; i < PlayerRanks.Ladder.Length; i++)
            {
                var step = PlayerRanks.Ladder[i];
                if (i < rankIndex)
                    ladder.Add(new RankStep(step.Title, RankStepState.Achieved));
                else if (i == rankIndex)
                    ladder.Add(new RankStep(step.Title, RankStepState.Current, "Твоє звання зараз"));
                else if (i == rankIndex + 1)
                    ladder.Add(new RankStep(step.Title, RankStepState.Next,
                        $"ще {PlayerRanks.PlanetsToNext(planetsDone)} планет"));
                else
                    ladder.Add(new RankStep(step.Title, RankStepState.Locked));
            }

            var stats = new List<ProfileStat>
            {
                new ProfileStat(planetsDone.ToString(), "Планет ожило", Hex("#00D9C0")),
                new ProfileStat(galaxiesDone.ToString(), "Галактик завершено", Hex("#9D4DFF")),
                new ProfileStat(state.Progress.EndlessRecord.ToString("N0"),
                    "Рекорд · Нескінченний", Hex("#FFB300")),
                // §8: головний рекорд нового ядра — картинки в колекції.
                new ProfileStat(state.Collection.Distinct.ToString(),
                    "Картинок у колекції", Hex("#9BE636"), star: true)
            };

            // Вітрина — ожилі планети поточної галактики. Порожня, поки жодної не закінчено:
            // показувати там незароблене означало б брехати гравцю про прогрес.
            var galaxy = GalaxyProgress.FromSave(state.Galaxy, state.Layout);
            var showcase = new List<ShowcasePlanet>();
            for (var i = 0; i < galaxy.Planets.Count && showcase.Count < 3; i++)
            {
                var planet = galaxy.Planets[i];
                if (planet.State == PlanetState.Done)
                    showcase.Add(new ShowcasePlanet($"p{i}", planet.Name, planet.Type));
            }

            var achievements = BuildAchievements(state, planetsDone);

            return new PlayerProfile(
                state.Nick, PlayerRanks.TitleFor(planetsDone), state.Wallet.OilDrops,
                ladder, stats, showcase, achievements);
        }

        private static List<Achievement> BuildAchievements(PlayerState state, int planetsDone)
        {
            return new List<Achievement>
            {
                new Achievement("a_gal", "Перша планета", Hex("#00D9C0"), planetsDone >= 1,
                    "Заповни всі слоти своєї першої планети"),
                new Achievement("a_slot", "Перша вітрина", Hex("#FFB300"),
                    GalaxyState.TotalFilled(state.Galaxy) >= 1,
                    "Постав картинку в слот планети"),
                new Achievement("a_ten", "Колекціонер", Hex("#9D4DFF"), state.Collection.Distinct >= 10,
                    "Збери десять різних картинок"),
                new Achievement("a_pic", "Перша картинка", Hex("#9BE636"), state.Collection.Distinct >= 1,
                    "Домалюй першу картинку в забігу")
            };
        }

        /// <summary>Профіль із мокових скріншотів — лише для сцени-майстерні.</summary>
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
                new ProfileStat("23", "Планет ожило", Hex("#00D9C0")),
                new ProfileStat("2", "Галактик завершено", Hex("#9D4DFF")),
                new ProfileStat("8 420", "Рекорд · Нескінченний", Hex("#FFB300")),
                new ProfileStat("127", "Картинок у колекції", Hex("#9BE636"), star: true)
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
                new Achievement("a_slot", "Перша вітрина", Hex("#FFB300"), true,
                    "Постав картинку в слот планети"),
                new Achievement("a_ten", "Колекціонер", Hex("#9D4DFF"), true,
                    "Збери десять різних картинок"),
                new Achievement("a_top", "Топ-100 тижня", Hex("#3B7BFF"), false,
                    "Увійди в сотню найкращих за тиждень")
            };

            return new PlayerProfile(
                "Нова", "Художниця галактик", 1250,
                ladder, stats, showcase, achievements);
        }
    }
}
