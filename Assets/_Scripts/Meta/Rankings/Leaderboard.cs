using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Гравець у таблиці — те, що малює рядок, подіум і картка «Ти».
    ///
    /// Тип планети беремо з наявного <see cref="PlanetType"/> циклом по місцю, а не заводимо окрему
    /// «тему рейтингу»: палітра для нього вже є в DesignSystem, і планета гравця малюється тим самим
    /// шейдером, що й у Галактиці; сусіди в таблиці ніколи не отримують однакову поверхню.
    /// </summary>
    public sealed class RankPlayer
    {
        public RankPlayer(string id, string nick, bool incognito, long value, int rank, int avatarId = 0,
            int galaxies = 0, long bestScore = 0)
        {
            Galaxies = galaxies;
            BestScore = bestScore;
            Id = id ?? string.Empty;
            Nick = nick ?? string.Empty;
            AvatarId = avatarId;
            Incognito = incognito;
            Value = value;
            Rank = rank;
        }

        public string Id { get; }
        public string Nick { get; }

        /// <summary>Номер аватара з набору U1Glaze (avatar_1…8 — 0…7): той самий, що в шапці головного й на картці гостя.</summary>
        public int AvatarId { get; }

        /// <summary>Гравець сховав профіль (§15): «Гравець-інкогніто», тап нічого не робить.</summary>
        public bool Incognito { get; }

        /// <summary>Значення таблиці — планет, що ожили, за весь час (сортування, 2026-10-24).</summary>
        public long Value { get; }

        /// <summary>Завершених галактик і рекорд забігу — показуються поруч, на місце не впливають.</summary>
        public int Galaxies { get; }
        public long BestScore { get; }

        /// <summary>Місце, 1 — перше; 0 — невідоме (картка «Ти» без відповіді сервера).</summary>
        public int Rank { get; }

        public bool IsYou { get; set; }

        public PlanetType Planet => IsYou ? PlanetType.Earth : (PlanetType)(Math.Max(0, Rank - 1) % PlanetTypes.Count);

        /// <summary>Картка «Ти» з РЕАЛЬНИХ чисел гравця (§16): локальний файл — правда.</summary>
        public static RankPlayer You(PlayerState state, string playerId, RankMetric metric, RankPeriod period, DateTime utcNow) =>
            new RankPlayer(string.IsNullOrEmpty(playerId) ? "you" : playerId, state.Nick,
                state.Settings.ProfileHidden, state.RankValue(metric, period, utcNow), 0, state.AvatarId,
                state.GalaxiesDone, state.Progress.EndlessRecord) { IsYou = true };
    }

    /// <summary>
    /// Таблиця лідерів на екрані: одна сторінка (метрика + період) від сервісу плюс картка «Ти».
    /// Сортування й місця — з сервера; тут лише перевірка порядку, місце гравця і розрив до наступного.
    /// Без мережі таблиця порожня зі статусом — екран каже «немає з'єднання», гра працює далі.
    /// </summary>
    public sealed class Leaderboard
    {
        private readonly List<RankPlayer> _players;

        public Leaderboard(LeaderboardPage page, RankPlayer you)
        {
            if (page is null) throw new ArgumentNullException(nameof(page));
            Metric = page.Metric;
            Period = page.Period;
            Status = page.Status;
            Error = page.Error;
            You = you ?? throw new ArgumentNullException(nameof(you));

            _players = new List<RankPlayer>(page.Entries.Count);
            foreach (var entry in page.Entries)
            {
                var player = new RankPlayer(entry.PlayerId, entry.Nick, entry.Incognito, entry.Value, entry.Rank, entry.AvatarId,
                    entry.Galaxies, entry.BestScore)
                {
                    IsYou = entry.PlayerId == you.Id
                };
                _players.Add(player);
            }
            // Сервер віддає за місцем, але порядок — наша відповідальність перед екраном.
            _players.Sort((a, b) => a.Rank.CompareTo(b.Rank));

            YourPosition = page.YourRank;
            if (YourPosition <= 0)
                foreach (var player in _players)
                    if (player.IsYou)
                        YourPosition = player.Rank;

            // Сусід над гравцем — зі сторінки, а коли гравець за її межами — зі значення, яке дав сервер окремо.
            GapToNext = 0;
            if (YourPosition > 1)
            {
                var found = false;
                foreach (var player in _players)
                    if (player.Rank == YourPosition - 1)
                    {
                        GapToNext = Math.Max(0, player.Value - you.Value);
                        found = true;
                    }
                if (!found && page.AheadValue >= 0)
                    GapToNext = Math.Max(0, page.AheadValue - you.Value);
            }
        }

        /// <summary>
        /// Смужка прогресу на картці «Ти» (рішення автора, U1Glaze S08): своє / (своє + скільки бракує до наступного).
        /// Перше місце (чи нічия з сусідом) — повна; місце невідоме — порожня.
        /// </summary>
        public float ProgressToNext =>
            YourPosition <= 0 ? 0f
            : YourPosition == 1 || GapToNext <= 0 ? 1f
            : You.Value <= 0 ? 0f
            : (float)You.Value / (You.Value + GapToNext);

        public RankMetric Metric { get; }
        public RankPeriod Period { get; }
        public LeaderboardStatus Status { get; }
        public string? Error { get; }
        public bool IsOk => Status == LeaderboardStatus.Ok;

        public RankPlayer You { get; }

        /// <summary>Гравці за місцем, 1 — перший.</summary>
        public IReadOnlyList<RankPlayer> Players => _players;

        /// <summary>Місце гравця; 0 — невідоме (сервер не відповів або гравець ще не надсилав значення).</summary>
        public int YourPosition { get; }

        /// <summary>Скільки бракує до наступного місця; 0 — невідомо або перше місце.</summary>
        public long GapToNext { get; }

        /// <summary>Таблиця без даних — лише статус і картка «Ти».</summary>
        public static Leaderboard Unavailable(RankMetric metric, RankPeriod period, LeaderboardStatus status, RankPlayer you, string? error = null) =>
            new Leaderboard(LeaderboardPage.Unavailable(metric, period, status, error), you);

        /// <summary>Одиниця виміру для підпису під числом.</summary>
        public static string Unit(RankMetric metric) => metric == RankMetric.Planets ? "планет" : "галактик";

        /// <summary>Назва метрики на сегменті.</summary>
        public static string Title(RankMetric metric) => metric == RankMetric.Planets ? "Планети" : "Галактики";
    }
}
