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
        void ShowRewarded(Action<bool> done);
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

    public interface IIapService
    {
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
