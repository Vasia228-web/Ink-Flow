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
        public RankPlayer(string id, string nick, InkColor avatar, bool incognito, long value, int rank)
        {
            Id = id ?? string.Empty;
            Nick = nick ?? string.Empty;
            Avatar = avatar;
            Incognito = incognito;
            Value = value;
            Rank = rank;
        }

        public string Id { get; }
        public string Nick { get; }

        /// <summary>Аватар із набору (§14): той самий колір, що в профілі гравця.</summary>
        public InkColor Avatar { get; }

        /// <summary>Гравець сховав профіль (§15): «Гравець-інкогніто», тап нічого не робить.</summary>
        public bool Incognito { get; }

        /// <summary>Значення метрики за період сторінки.</summary>
        public long Value { get; }

        /// <summary>Місце, 1 — перше; 0 — невідоме (картка «Ти» без відповіді сервера).</summary>
        public int Rank { get; }

        public bool IsYou { get; set; }

        public PlanetType Planet => IsYou ? PlanetType.Earth : (PlanetType)(Math.Max(0, Rank - 1) % PlanetTypes.Count);

        /// <summary>Картка «Ти» з РЕАЛЬНИХ чисел гравця (§16): локальний файл — правда.</summary>
        public static RankPlayer You(PlayerState state, string playerId, RankMetric metric, RankPeriod period, DateTime utcNow) =>
            new RankPlayer(string.IsNullOrEmpty(playerId) ? "you" : playerId, state.Nick, AvatarSet.InkOf(state.AvatarId),
                state.Settings.ProfileHidden, state.RankValue(metric, period, utcNow), 0) { IsYou = true };
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
                var player = new RankPlayer(entry.PlayerId, entry.Nick, AvatarSet.InkOf(entry.AvatarId), entry.Incognito, entry.Value, entry.Rank)
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

            GapToNext = 0;
            if (YourPosition > 1)
                foreach (var player in _players)
                    if (player.Rank == YourPosition - 1)
                        GapToNext = Math.Max(0, player.Value - you.Value);
        }

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
