using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>За чим міряємось. Record — рекорд колекції: скільки різних картинок зібрано (майстер-док §8, §10).</summary>
    public enum RankMetric
    {
        Planets = 0,
        Galaxies = 1,
        Record = 2
    }

    /// <summary>За який період.</summary>
    public enum RankPeriod
    {
        Week = 0,
        AllTime = 1
    }

    /// <summary>З ким міряємось.</summary>
    public enum RankScope
    {
        Friends = 0,
        World = 1
    }

    /// <summary>
    /// Гравець у таблиці.
    ///
    /// Тип планети беремо з наявного <see cref="PlanetType"/>, а не заводимо окрему
    /// «тему рейтингу»: палітра для нього вже є в DesignSystem, і планета гравця
    /// малюється тим самим шейдером, що й у Галактиці.
    /// </summary>
    public sealed class RankPlayer
    {
        public RankPlayer(string id, string nick, PlanetType planet, Rgb dropColor)
        {
            Id = id;
            Nick = nick;
            Planet = planet;
            DropColor = dropColor;
        }

        public string Id { get; }
        public string Nick { get; }
        public PlanetType Planet { get; }

        /// <summary>Колір краплі-аватара в рядку.</summary>
        public Rgb DropColor { get; }

        /// <summary>Гравець сховав профіль: нік і планета не показуються, тап нічого не робить.</summary>
        public bool Incognito { get; set; }

        public bool IsFriend { get; set; }

        /// <summary>Скільки планет завершено — потрібно для перегляду його галактики.</summary>
        public int GalaxiesDone { get; set; }

        public bool IsYou { get; set; }

        public long PlanetsWeek { get; set; }
        public long PlanetsAll { get; set; }
        public long GalaxiesWeek { get; set; }
        public long GalaxiesAll { get; set; }
        public long RecordWeek { get; set; }
        public long RecordAll { get; set; }

        public long Value(RankMetric metric, RankPeriod period) => metric switch
        {
            RankMetric.Planets => period == RankPeriod.Week ? PlanetsWeek : PlanetsAll,
            RankMetric.Galaxies => period == RankPeriod.Week ? GalaxiesWeek : GalaxiesAll,
            _ => period == RankPeriod.Week ? RecordWeek : RecordAll
        };
    }

    /// <summary>
    /// Таблиця лідерів: сортування за метрикою й періодом, місце гравця й розрив
    /// до наступної позиції.
    ///
    /// Сортування живе тут, а не в UI: інакше «Планети» і «Рекорд» рано чи пізно
    /// сортувались би по-різному, і побачив би це лише гравець.
    /// </summary>
    public sealed class Leaderboard
    {
        private readonly List<RankPlayer> _players;

        public Leaderboard(IReadOnlyList<RankPlayer> players, RankPlayer you)
        {
            _players = new List<RankPlayer>(players);
            You = you;
        }

        public RankPlayer You { get; }

        public IReadOnlyList<RankPlayer> Players => _players;

        /// <summary>Одиниця виміру для підпису під числом.</summary>
        public static string Unit(RankMetric metric) => metric switch
        {
            RankMetric.Planets => "планет",
            RankMetric.Galaxies => "галактик",
            _ => "картинок"
        };

        /// <summary>
        /// Заповнює <paramref name="into"/> впорядкованим списком.
        ///
        /// Буфер дає викликач, а не ми: перша ж версія повертала спільний
        /// внутрішній список, і два виклики поспіль давали два посилання на
        /// той самий масив — тест зловив це одразу. Повертати щоразу новий
        /// список теж не годиться: список перечитується на кожну зміну фільтра.
        /// </summary>
        public void Ranked(RankScope scope, RankMetric metric, RankPeriod period,
            List<RankPlayer> into)
        {
            into.Clear();
            for (var i = 0; i < _players.Count; i++)
                if (scope == RankScope.World || _players[i].IsFriend)
                    into.Add(_players[i]);

            into.Sort((a, b) =>
            {
                var byValue = b.Value(metric, period).CompareTo(a.Value(metric, period));
                // Рівні значення розводимо ніком — інакше порядок стрибав би між
                // перемиканнями вкладок без жодної причини.
                return byValue != 0 ? byValue : string.CompareOrdinal(a.Nick, b.Nick);
            });
        }

        /// <summary>Місце гравця в загальному заліку. Мокове — сервер дасть справжнє.</summary>
        public int YourPosition(RankMetric metric, RankPeriod period)
        {
            var table = period == RankPeriod.Week ? _weekPositions : _allPositions;
            return table[(int)metric];
        }

        /// <summary>Скільки бракує до наступної позиції.</summary>
        public long GapToNext(RankMetric metric, RankPeriod period)
        {
            var table = period == RankPeriod.Week ? _weekGaps : _allGaps;
            return table[(int)metric];
        }

        private static readonly int[] _weekPositions = { 214, 96, 402 };
        private static readonly int[] _allPositions = { 4821, 2310, 6087 };
        private static readonly long[] _weekGaps = { 2, 1, 120 };
        private static readonly long[] _allGaps = { 38, 5, 640 };

        private static Rgb Hex(string hex) => Rgb.FromHex(hex);

        /// <summary>
        /// Вісімнадцять вигаданих гравців із макета. Числа підібрані так, щоб
        /// топ-3 тижня збігався з еталонним скріншотом: Вега 56, Гелій 52, Квазар 49.
        /// </summary>
        public static Leaderboard CreateMock()
        {
            var drops = new[]
            {
                "#FF2D8A", "#00D9C0", "#FFB300", "#9BE636", "#9D4DFF",
                "#3B7BFF", "#FF8A3C", "#FF5BB0", "#8FE8C0", "#CBB0FF"
            };

            var nicks = new[]
            {
                "Вега", "Гелій", "Квазар", "Зоряр", "Нейтрон", "Оріон",
                "Плутон", "Андромеда", "Пульсар", "Сіріус", "Небула", "Комета",
                "Фотон", "Метеор", "Титан", "Галакто", "Ліра", "Веста"
            };

            var planetsWeek = new long[]
            {
                56, 52, 49, 48, 43, 42, 39, 36, 34, 31, 29, 27, 24, 21, 18, 16, 13, 9
            };

            var friends = new HashSet<int> { 1, 4, 8, 11, 15 };
            var incognito = new HashSet<int> { 6, 13 };

            var players = new List<RankPlayer>(nicks.Length);
            for (var i = 0; i < nicks.Length; i++)
            {
                var week = planetsWeek[i];
                var player = new RankPlayer(
                    $"r{i}", nicks[i],
                    // Тип планети циклом по наявних — сусіди в таблиці ніколи
                    // не отримують однакову поверхню.
                    (PlanetType)(i % PlanetTypes.Count),
                    Hex(drops[i % drops.Length]))
                {
                    Incognito = incognito.Contains(i),
                    IsFriend = friends.Contains(i),
                    GalaxiesDone = 3 + i % 5,
                    PlanetsWeek = week,
                    PlanetsAll = 72 + week * 6,
                    GalaxiesWeek = 7 - i / 3,
                    GalaxiesAll = 34 - i,
                    // Рекорд — картинки в колекції (§8): за тиждень одиниці-десятки, за весь час — десятки.
                    RecordWeek = 19 - i,
                    RecordAll = 64 - i * 3
                };

                if (player.GalaxiesWeek < 0)
                    player.GalaxiesWeek = 0;
                players.Add(player);
            }

            var you = new RankPlayer("you", "Нова", PlanetType.Earth, Hex("#FF2D8A"))
            {
                IsYou = true,
                IsFriend = true,
                GalaxiesDone = 4,
                PlanetsWeek = 12,
                PlanetsAll = 41,
                GalaxiesWeek = 1,
                GalaxiesAll = 3,
                RecordWeek = 3,
                RecordAll = 11
            };

            return new Leaderboard(players, you);
        }

        /// <summary>
        /// Мокова таблиця з РЕАЛЬНОЮ карткою «Ти».
        ///
        /// Світовий список лишається вигаданим — бекенду немає (Фаза 6), і
        /// підміняти його чимось «схожим на правду» було б гірше за чесний мок.
        /// А от свої числа гравець мусить бачити справжні: позиція в таблиці
        /// рахується від них, тож і вона стає чесною.
        /// </summary>
        public static Leaderboard WithRealPlayer(PlayerState state)
        {
            var source = CreateMock();
            // §12/§16: планети, що ожили, і завершені галактики — з усіх циклів.
            var planetsDone = GalaxyState.CompletedPlanets(state.Galaxy, state.Layout);
            var galaxiesDone = GalaxyState.CompletedGalaxies(state.Galaxy, state.Layout);
            // §10: «рекорд колекції → рейтинги». Тижневого зрізу без бекенду немає — той самий лік.
            var record = state.Collection.Distinct;

            var you = new RankPlayer("you", state.Nick, PlanetType.Earth, Hex("#FF2D8A"))
            {
                IsYou = true,
                IsFriend = true,
                // §15: «Приховати профіль у рейтингах» — у таблиці гравець є, але інкогніто.
                Incognito = state.Settings.ProfileHidden,
                GalaxiesDone = galaxiesDone,
                PlanetsWeek = planetsDone,
                PlanetsAll = planetsDone,
                GalaxiesWeek = galaxiesDone,
                GalaxiesAll = galaxiesDone,
                RecordWeek = record,
                RecordAll = record
            };

            return new Leaderboard(source.Players, you);
        }
    }
}
