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
        public bool IsInterstitialReady => false;

        public void ShowRewarded(Action<bool> done) => done?.Invoke(false);
        public void ShowInterstitial(Action done) => done?.Invoke();
    }

    /// <summary>
    /// Реклама «навмання» для редактора й dev-збірок: завжди готова, «додивляється» миттєво.
    /// Так кнопки §9 можна пройти руками до підключення SDK. У релізі — NullAds або справжня.
    /// </summary>
    public sealed class FakeAds : IAdsService
    {
        public bool IsRewardedReady => true;
        public bool IsInterstitialReady => true;

        public void ShowRewarded(Action<bool> done)
        {
            Debug.Log("[ads] fake rewarded — зараховано без ролика");
            done?.Invoke(true);
        }

        public void ShowInterstitial(Action done)
        {
            Debug.Log("[ads] fake interstitial — пропущено");
            done?.Invoke();
        }
    }

    /// <summary>
    /// Стор без SDK (реліз до підключення Unity IAP): цін немає, купити нічого не можна — магазин
    /// показує пакети з неактивними кнопками й каже, що стор недоступний.
    /// </summary>
    public sealed class NullIap : IIapService
    {
        public bool IsAvailable => false;

        public void Query(IReadOnlyList<string> productIds, Action<IReadOnlyList<StoreProduct>> done) =>
            done?.Invoke(Array.Empty<StoreProduct>());

        public void Buy(string productId, Action<PurchaseResult> done) =>
            done?.Invoke(new PurchaseResult(false, productId, "Магазин недоступний"));
    }

    /// <summary>
    /// Стор «навмання» для редактора й dev-збірок: ціни чотирьох пакетів майстер-доку §13 рядками,
    /// покупка вдається миттєво. Так магазин можна пройти руками до підключення SDK; у релізі —
    /// <see cref="NullIap"/> або справжній.
    /// </summary>
    public sealed class FakeIap : IIapService
    {
        private static readonly Dictionary<string, string> Prices = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oil_1000"] = "$5",
            ["oil_10000"] = "$25",
            ["oil_100000"] = "$50",
            ["oil_500000"] = "$100"
        };

        public bool IsAvailable => true;

        public void Query(IReadOnlyList<string> productIds, Action<IReadOnlyList<StoreProduct>> done)
        {
            var products = new List<StoreProduct>(productIds?.Count ?? 0);
            for (var i = 0; productIds != null && i < productIds.Count; i++)
                if (Prices.TryGetValue(productIds[i], out var price))
                    products.Add(new StoreProduct(productIds[i], price));
            done?.Invoke(products);
        }

        public void Buy(string productId, Action<PurchaseResult> done)
        {
            if (!Prices.ContainsKey(productId))
            {
                done?.Invoke(new PurchaseResult(false, productId, "Невідомий товар"));
                return;
            }
            Debug.Log($"[iap] fake purchase '{productId}' — зараховано без білінгу");
            done?.Invoke(new PurchaseResult(true, productId));
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
