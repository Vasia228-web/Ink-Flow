using System;
using System.Collections.Generic;
using InkFlow.Core;

// Рейтингові Null/Fake-реалізації живуть окремо від решти й НЕ тягнуть UnityEngine: їх разом із
// інтерфейсами та RankingsSync компілює headless-раннер (Tools/CoreTestRunner), тож поведінка
// синхронізації й мока перевіряється без редактора.
namespace InkFlow.Platform
{
    /// <summary>Без хмари: гравець не ввійшов, рейтинги кажуть «не підключено», вітрина нікуди не йде.</summary>
    public sealed class NullIdentity : IIdentityService
    {
        public bool IsSignedIn => false;
        public string PlayerId => string.Empty;
        public void SignIn(Action<bool> done) => done?.Invoke(false);
    }

    public sealed class NullLeaderboards : ILeaderboardService
    {
        public bool IsAvailable => false;

        public void Fetch(RankMetric metric, RankPeriod period, int limit, Action<LeaderboardPage> done) =>
            done?.Invoke(LeaderboardPage.Unavailable(metric, period, LeaderboardStatus.NotConfigured));

        public void Submit(RankMetric metric, RankPeriod period, long value, Action<bool>? done = null) => done?.Invoke(false);
    }

    public sealed class NullShowcase : IShowcaseService
    {
        public bool IsAvailable => false;
        public void Publish(PublicShowcase showcase, Action<bool>? done = null) => done?.Invoke(false);
        public void Fetch(string playerId, Action<PublicShowcase?> done) => done?.Invoke(null);
    }

    /// <summary>Тотожність «навмання» для редактора й dev-збірок: завжди ввійшов, id сталий.</summary>
    public sealed class FakeIdentity : IIdentityService
    {
        public FakeIdentity(string playerId = "local-player") => PlayerId = playerId;

        public bool IsSignedIn => true;
        public string PlayerId { get; }
        public void SignIn(Action<bool> done) => done?.Invoke(true);
    }

    /// <summary>
    /// Таблиці «навмання» для редактора й dev-збірок: вісімнадцять гравців макета (<see cref="MockRankings"/>),
    /// місце гравця рахується від надісланого значення ЦЬОГО періоду; <see cref="Offline"/> імітує
    /// відсутність мережі — так екран «немає з'єднання» можна пройти руками й у тестах. Колбеки синхронні.
    /// </summary>
    public sealed class FakeLeaderboards : ILeaderboardService
    {
        private readonly Dictionary<(RankMetric, RankPeriod), long> _submitted = new Dictionary<(RankMetric, RankPeriod), long>();

        /// <summary>Імітація «немає мережі».</summary>
        public bool Offline { get; set; }

        public bool IsAvailable => true;

        public int Submissions { get; private set; }

        /// <summary>Останнє надіслане значення за метрикою й періодом; null — не надсилалось.</summary>
        public long? Submitted(RankMetric metric, RankPeriod period) =>
            _submitted.TryGetValue((metric, period), out var value) ? value : (long?)null;

        public void Fetch(RankMetric metric, RankPeriod period, int limit, Action<LeaderboardPage> done)
        {
            if (Offline)
            {
                done?.Invoke(LeaderboardPage.Unavailable(metric, period, LeaderboardStatus.NoConnection));
                return;
            }
            _submitted.TryGetValue((metric, period), out var yours);
            done?.Invoke(MockRankings.Page(metric, period, yours, limit));
        }

        public void Submit(RankMetric metric, RankPeriod period, long value, Action<bool>? done = null)
        {
            if (Offline)
            {
                done?.Invoke(false);
                return;
            }
            _submitted[(metric, period)] = value;
            Submissions++;
            done?.Invoke(true);
        }
    }

    /// <summary>Вітрини «навмання»: опубліковані лежать у пам'яті, невідомі гравці — мокові з планетами по порядку.</summary>
    public sealed class FakeShowcase : IShowcaseService
    {
        private readonly Dictionary<string, PublicShowcase> _published = new Dictionary<string, PublicShowcase>(StringComparer.Ordinal);
        private readonly IReadOnlyList<string> _planetIds;
        private readonly IReadOnlyList<int> _planetSlots;

        /// <param name="planetIds">Назви планет розкладки (для мокових вітрин).</param>
        /// <param name="planetSlots">Слоти кожної планети розкладки.</param>
        public FakeShowcase(IReadOnlyList<string>? planetIds = null, IReadOnlyList<int>? planetSlots = null)
        {
            _planetIds = planetIds ?? Array.Empty<string>();
            _planetSlots = planetSlots ?? Array.Empty<int>();
        }

        public bool Offline { get; set; }
        public bool IsAvailable => true;
        public int Publications { get; private set; }

        /// <summary>Остання опублікована вітрина (тестам).</summary>
        public PublicShowcase? LastPublished { get; private set; }

        public void Publish(PublicShowcase showcase, Action<bool>? done = null)
        {
            if (Offline || showcase == null)
            {
                done?.Invoke(false);
                return;
            }
            _published[showcase.PlayerId] = showcase;
            LastPublished = showcase;
            Publications++;
            done?.Invoke(true);
        }

        public void Fetch(string playerId, Action<PublicShowcase?> done)
        {
            if (Offline || string.IsNullOrEmpty(playerId))
            {
                done?.Invoke(null);
                return;
            }
            if (_published.TryGetValue(playerId, out var stored))
            {
                done?.Invoke(stored);
                return;
            }
            done?.Invoke(MockRankings.ShowcaseFor(playerId, _planetIds, _planetSlots));
        }
    }
}
