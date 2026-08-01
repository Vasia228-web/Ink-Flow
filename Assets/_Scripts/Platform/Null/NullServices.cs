using System;
using System.Collections.Generic;
using UnityEngine;

namespace InkFlow.Platform
{
    /// <summary>
    /// Null-реалізації всіх платформних сервісів (§11).
    /// Правило: гра має бути повністю грабельною без жодного SDK — це і швидкість
    /// ітерацій, і страховка на випадок, коли черговий SDK ламає білд.
    /// Реальні реалізації (Android/iOS) підмінюються в GameBootstrap на Фазі 5.
    /// </summary>
    public sealed class NullHaptics : IHapticService
    {
        public void Light() { }
        public void Medium() { }
        public void Heavy() { }
        public void Chain(int depth) { }
    }

    /// <summary>Аналітика в консоль — видно всі події ще до підключення реального SDK.</summary>
    public sealed class LogAnalytics : IAnalyticsService
    {
        public void Track(string evt, IDictionary<string, object>? props = null)
        {
            if (props == null || props.Count == 0)
            {
                Debug.Log($"[analytics] {evt}");
                return;
            }

            var parts = new List<string>(props.Count);
            foreach (var pair in props)
                parts.Add($"{pair.Key}={pair.Value}");
            Debug.Log($"[analytics] {evt} {{{string.Join(", ", parts)}}}");
        }
    }

    public sealed class NullAds : IAdsService
    {
        public bool IsRewardedReady => false;

        public void ShowRewarded(Action<bool> done) => done?.Invoke(false);
    }

    public sealed class FakeIap : IIapService
    {
        public void Buy(string productId, Action<PurchaseResult> done)
        {
            Debug.Log($"[iap] fake purchase '{productId}' — реального білінгу ще немає");
            done?.Invoke(new PurchaseResult(false, productId, "IAP не підключено"));
        }
    }

    public sealed class NullReview : IReviewService
    {
        public void RequestReview() { }
    }

    public sealed class NullNotifications : INotificationService
    {
        public void Schedule(string id, string title, string body, TimeSpan delay) { }
        public void CancelAll() { }
    }
}
