using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>За чим міряємось (майстер-док §16): планети, що ожили, і завершені галактики. Метрики «Колекція» немає.</summary>
    public enum RankMetric
    {
        Planets = 0,
        Galaxies = 1
    }

    /// <summary>За який період (§16): цей тиждень або за весь час.</summary>
    public enum RankPeriod
    {
        Week = 0,
        AllTime = 1
    }

    /// <summary>Чим закінчився запит таблиці.</summary>
    public enum LeaderboardStatus
    {
        Ok = 0,

        /// <summary>Мережі немає — рейтинги показують «немає з'єднання», гра працює далі (§16).</summary>
        NoConnection = 1,

        /// <summary>Сервіси не налаштовані (Null-реалізація або SDK не ініціалізувався).</summary>
        NotConfigured = 2,

        /// <summary>Сервер відповів помилкою.</summary>
        Failed = 3
    }

    /// <summary>Рядок таблиці лідерів, як його віддає сервіс: лише те, що потрібно екрану.</summary>
    public sealed class LeaderboardEntry
    {
        public LeaderboardEntry(string playerId, string nick, int avatarId, bool incognito, long value, int rank)
        {
            PlayerId = playerId ?? string.Empty;
            Nick = nick ?? string.Empty;
            AvatarId = avatarId;
            Incognito = incognito;
            Value = value;
            Rank = rank;
        }

        public string PlayerId { get; }
        public string Nick { get; }
        public int AvatarId { get; }
        public bool Incognito { get; }
        public long Value { get; }

        /// <summary>Місце, 1 — перше.</summary>
        public int Rank { get; }
    }

    /// <summary>Сторінка таблиці для однієї метрики й періоду плюс місце самого гравця (0 — невідоме).</summary>
    public sealed class LeaderboardPage
    {
        public LeaderboardPage(RankMetric metric, RankPeriod period, LeaderboardStatus status,
            IReadOnlyList<LeaderboardEntry>? entries, int yourRank, long yourValue, string? error = null)
        {
            Metric = metric;
            Period = period;
            Status = status;
            Entries = entries ?? Array.Empty<LeaderboardEntry>();
            YourRank = yourRank;
            YourValue = yourValue;
            Error = error;
        }

        public RankMetric Metric { get; }
        public RankPeriod Period { get; }
        public LeaderboardStatus Status { get; }
        public IReadOnlyList<LeaderboardEntry> Entries { get; }
        public int YourRank { get; }
        public long YourValue { get; }
        public string? Error { get; }

        public bool IsOk => Status == LeaderboardStatus.Ok;

        public static LeaderboardPage Unavailable(RankMetric metric, RankPeriod period, LeaderboardStatus status, string? error = null) =>
            new LeaderboardPage(metric, period, status, null, 0, 0, error);
    }

    /// <summary>Зайнятий слот планети у публічній вітрині: планета — назвою типу, картинка — назвою з бібліотеки.</summary>
    public readonly struct ShowcaseSlot
    {
        public ShowcaseSlot(string planetId, int slot, string pictureId)
        {
            PlanetId = planetId ?? string.Empty;
            Slot = slot;
            PictureId = pictureId ?? string.Empty;
        }

        public string PlanetId { get; }
        public int Slot { get; }
        public string PictureId { get; }
    }

    /// <summary>
    /// Публічна вітрина гравця (§16) — єдине, що йде в хмару: нік, аватар, лічильники, галактика
    /// з картинками в слотах і вітринна картинка. Локальний файл — правда; це його публічний зріз.
    /// Прихований профіль (§15) віддає лише прапорець і лічильники — ні ніка, ні картинок.
    /// </summary>
    public sealed class PublicShowcase
    {
        public PublicShowcase(string playerId, string nick, int avatarId, bool incognito, string? showcasePictureId,
            int planetsDone, int galaxiesDone, int galaxy, IReadOnlyList<ShowcaseSlot>? slots, string updatedUtc)
        {
            PlayerId = playerId ?? string.Empty;
            Nick = nick ?? string.Empty;
            AvatarId = avatarId;
            Incognito = incognito;
            ShowcasePictureId = string.IsNullOrEmpty(showcasePictureId) ? null : showcasePictureId;
            PlanetsDone = planetsDone;
            GalaxiesDone = galaxiesDone;
            Galaxy = galaxy;
            Slots = slots ?? Array.Empty<ShowcaseSlot>();
            UpdatedUtc = updatedUtc ?? string.Empty;
        }

        public string PlayerId { get; }
        public string Nick { get; }
        public int AvatarId { get; }
        public bool Incognito { get; }
        public string? ShowcasePictureId { get; }
        public int PlanetsDone { get; }
        public int GalaxiesDone { get; }

        /// <summary>Індекс циклу галактики, яку показуємо (поточна з картинками або остання завершена).</summary>
        public int Galaxy { get; }

        public IReadOnlyList<ShowcaseSlot> Slots { get; }
        public string UpdatedUtc { get; }

        /// <summary>Вітрина прихованого профілю: лише факт і лічильники.</summary>
        public static PublicShowcase Hidden(string playerId, int planetsDone, int galaxiesDone, string updatedUtc) =>
            new PublicShowcase(playerId, string.Empty, 0, true, null, planetsDone, galaxiesDone, 0, null, updatedUtc);
    }

    /// <summary>
    /// Вигадана таблиця для редактора, майстерень і тестів: вісімнадцять гравців із макета, числа
    /// підібрані так, щоб топ-3 тижня «Планети» збігався з еталонним скріншотом (Вега 56, Гелій 52,
    /// Квазар 49). Детерміновано — без випадковості, щоб знімки й тести були стабільні.
    /// </summary>
    public static class MockRankings
    {
        private static readonly string[] Nicks =
        {
            "Вега", "Гелій", "Квазар", "Зоряр", "Нейтрон", "Оріон",
            "Плутон", "Андромеда", "Пульсар", "Сіріус", "Небула", "Комета",
            "Фотон", "Метеор", "Титан", "Галакто", "Ліра", "Веста"
        };

        private static readonly long[] PlanetsWeek =
        {
            56, 52, 49, 48, 43, 42, 39, 36, 34, 31, 29, 27, 24, 21, 18, 16, 13, 9
        };

        private static readonly int[] Incognito = { 6, 13 };

        public static int Count => Nicks.Length;

        public static string PlayerIdOf(int index) => $"mock-{index}";

        /// <summary>Значення гравця з номером <paramref name="index"/> за метрикою й періодом — як у макеті.</summary>
        public static long ValueOf(int index, RankMetric metric, RankPeriod period)
        {
            var week = PlanetsWeek[index];
            return metric switch
            {
                RankMetric.Planets => period == RankPeriod.Week ? week : 72 + week * 6,
                _ => period == RankPeriod.Week ? Math.Max(0, 7 - index / 3) : 34 - index
            };
        }

        /// <summary>Сторінка таблиці з місцем гравця, порахованим від його значення <paramref name="yourValue"/>.</summary>
        public static LeaderboardPage Page(RankMetric metric, RankPeriod period, long yourValue, int limit = int.MaxValue)
        {
            var rows = new List<(int index, long value)>(Nicks.Length);
            for (var i = 0; i < Nicks.Length; i++)
                rows.Add((i, ValueOf(i, metric, period)));
            // Рівні значення розводимо ніком — інакше порядок стрибав би між перемиканнями без причини.
            rows.Sort((a, b) =>
            {
                var byValue = b.value.CompareTo(a.value);
                return byValue != 0 ? byValue : string.CompareOrdinal(Nicks[a.index], Nicks[b.index]);
            });

            var entries = new List<LeaderboardEntry>(Math.Min(limit, rows.Count));
            var yourRank = 1;
            for (var r = 0; r < rows.Count; r++)
            {
                var (index, value) = rows[r];
                if (value > yourValue)
                    yourRank++;
                if (entries.Count < limit)
                    entries.Add(new LeaderboardEntry(PlayerIdOf(index), Nicks[index], index % 6,
                        Array.IndexOf(Incognito, index) >= 0, value, r + 1));
            }
            return new LeaderboardPage(metric, period, LeaderboardStatus.Ok, entries, yourRank, yourValue);
        }

        /// <summary>
        /// Вітрина мокового гравця: планети ожили по порядку; слоти зайняті заглушками-назвами (бібліотеки
        /// тут не знаємо) — для каруселі галактики важливо лише, що слот зайнятий.
        /// </summary>
        public static PublicShowcase ShowcaseFor(string playerId, IReadOnlyList<string> planetIds, IReadOnlyList<int> planetSlots)
        {
            var index = 0;
            if (playerId != null && playerId.StartsWith("mock-", StringComparison.Ordinal))
                int.TryParse(playerId.Substring(5), out index);
            if (index < 0 || index >= Nicks.Length)
                index = 0;

            var planetsDone = 3 + index % 5;
            var id = playerId ?? PlayerIdOf(index);
            // Прихований профіль і в моку прихований: ні ніка, ні слотів — як справжня вітрина.
            if (Array.IndexOf(Incognito, index) >= 0)
                return PublicShowcase.Hidden(id, planetsDone, 0, string.Empty);

            var slots = new List<ShowcaseSlot>();
            var planets = Math.Min(planetIds?.Count ?? 0, planetSlots?.Count ?? 0);
            for (var p = 0; p < planets && p < planetsDone; p++)
                for (var s = 0; s < planetSlots![p]; s++)
                    slots.Add(new ShowcaseSlot(planetIds![p], s, $"mock-{p}-{s}"));
            return new PublicShowcase(id, Nicks[index], index % 6, false, null, planetsDone, 0, 0, slots, string.Empty);
        }
    }
}
