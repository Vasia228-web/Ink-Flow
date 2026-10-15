using System;
using System.Collections.Generic;

namespace InkFlow.Platform
{
    /// <summary>
    /// Тактильний відгук. Handheld.Vibrate() свідомо НЕ використовуємо: на iOS це грубий
    /// «дзиж» замість тактильного відгуку, потрібного для merge/burst (§11).
    /// </summary>
    public interface IHapticService
    {
        void Light();
        void Medium();
        void Heavy();

        /// <summary>Відгук ланки ланцюга — сила росте з глибиною.</summary>
        void Chain(int depth);
    }

    public interface IAnalyticsService
    {
        void Track(string evt, IDictionary<string, object>? props = null);
    }

    public interface IAdsService
    {
        bool IsRewardedReady { get; }

        /// <summary>Ролик за нагороду (майстер-док §9): done(true) — додивився, можна платити.</summary>
        void ShowRewarded(Action<bool> done);

        bool IsInterstitialReady { get; }

        /// <summary>Інтерстиціал між забігами (§9: раз на три-чотири). done — коли закрито.</summary>
        void ShowInterstitial(Action done);
    }

    public interface IReviewService
    {
        void RequestReview();
    }

    public interface INotificationService
    {
        void Schedule(string id, string title, string body, TimeSpan delay);
        void CancelAll();
    }
}

namespace InkFlow.Platform
{
    /// <summary>
    /// Тотожність гравця в хмарі (Unity Gaming Services: Authentication, анонімний вхід). Без мережі
    /// чи без налаштувань — не ввійшов, і рейтинги кажуть «немає з'єднання» (§16).
    /// </summary>
    public interface IIdentityService
    {
        bool IsSignedIn { get; }

        /// <summary>Ідентифікатор гравця в хмарі; порожньо, поки не ввійшов.</summary>
        string PlayerId { get; }

        void SignIn(Action<bool> done);
    }

    /// <summary>
    /// Таблиці лідерів (§16): «Планети» й «Галактики», «цей тиждень» і «за весь час». Гра лише
    /// надсилає свої числа й читає сторінки; сортування й місця — на сервері. Локальний файл — правда.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>false — Null-реалізація або SDK не ініціалізувався: екран показує «не підключено».</summary>
        bool IsAvailable { get; }

        /// <summary>Сторінка топу з місцем гравця. Без мережі — <see cref="InkFlow.Core.LeaderboardStatus.NoConnection"/>, без винятків.</summary>
        void Fetch(InkFlow.Core.RankMetric metric, InkFlow.Core.RankPeriod period, int limit, Action<InkFlow.Core.LeaderboardPage> done);

        /// <summary>
        /// Надіслати своє значення метрики в таблицю періоду: «за весь час» — лічильник, «цей тиждень» —
        /// приріст від бази тижня (тижнева таблиця на сервері скидається за тим самим розкладом).
        /// </summary>
        void Submit(InkFlow.Core.RankMetric metric, InkFlow.Core.RankPeriod period, long value, Action<bool>? done = null);
    }

    /// <summary>
    /// Публічна вітрина гравця в хмарі (Cloud Save, публічний доступ): нік, аватар, планети з картинками,
    /// вітринна картинка. Єдине, що гра пише в хмару; чужу вітрину читаємо для перегляду галактики.
    /// </summary>
    public interface IShowcaseService
    {
        bool IsAvailable { get; }

        void Publish(InkFlow.Core.PublicShowcase showcase, Action<bool>? done = null);

        /// <summary>Вітрина іншого гравця; null — немає, немає мережі або сервіс не налаштований.</summary>
        void Fetch(string playerId, Action<InkFlow.Core.PublicShowcase?> done);
    }
}
