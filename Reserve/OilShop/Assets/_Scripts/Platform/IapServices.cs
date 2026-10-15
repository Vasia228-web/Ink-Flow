using System;
using System.Collections.Generic;
using UnityEngine;

// ЗАПАС (з 2026-10-15): покупка нафти за реальні гроші поза грою. Цей файл зібрано з двох файлів гри:
// типи стору й інтерфейс — з Assets/_Scripts/Platform/PlatformServices.cs (namespace InkFlow.Platform,
// поруч з IAdsService), Null/Fake-реалізації — з Assets/_Scripts/Platform/Null/NullServices.cs.
// Повертаючи, можна покласти файл цілим у Assets/_Scripts/Platform/ — збірка та сама (InkFlow.Platform).
// Увага: CoreTestRunner.csproj компілює PlatformServices.cs явним шляхом; NullServices.cs він не бачить
// (там UnityEngine.Debug), тож у headless-тестах ці типи були лише через PlatformServices.cs.

namespace InkFlow.Platform
{
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

}
