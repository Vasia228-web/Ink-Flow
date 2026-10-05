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

    /// <summary>Товар зі стору: ідентифікатор і ЛОКАЛІЗОВАНА ціна рядком — її показують, не рахують (§13).</summary>
    public readonly struct StoreProduct
    {
        public StoreProduct(string id, string localizedPrice)
        {
            Id = id;
            LocalizedPrice = localizedPrice;
        }

        public string Id { get; }
        public string LocalizedPrice { get; }
    }

    public readonly struct PurchaseResult
    {
        public bool Success { get; }
        public string ProductId { get; }
        public string? Error { get; }

        public PurchaseResult(bool success, string productId, string? error = null)
        {
            Success = success;
            ProductId = productId;
            Error = error;
        }
    }

    /// <summary>
    /// Покупки за реальні гроші (майстер-док §13). Ціна на кнопці магазину — з <see cref="Query"/>,
    /// не з гри: стор знає валюту й податки гравця, гра — ні. Без стору (<c>IsAvailable</c> false)
    /// магазин показує пакети без цін і з неактивними кнопками — гра лишається грабельною.
    /// </summary>
    public interface IIapService
    {
        /// <summary>Чи підключений стор. false — Null-реалізація або SDK не ініціалізувався.</summary>
        bool IsAvailable { get; }

        /// <summary>Ціни товарів зі стору. Невідомі ідентифікатори у відповіді відсутні; без стору — порожній список.</summary>
        void Query(IReadOnlyList<string> productIds, Action<IReadOnlyList<StoreProduct>> done);

        void Buy(string productId, Action<PurchaseResult> done);
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
