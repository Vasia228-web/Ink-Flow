using System;
using System.Collections.Generic;

namespace InkFlow.App
{
    /// <summary>
    /// Найпростіший реєстр сервісів (§8). Це НЕ DI-контейнер: реєстрація відбувається
    /// в одному місці (GameBootstrap), а далі залежності передаються конструкторами
    /// й серіалізованими полями. Пошук через локатор дозволений лише на межі сцени —
    /// у геймплеї FindObjectOfType і резолв у Update заборонені.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            Services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var service))
                return (T)service;
            throw new InvalidOperationException(
                $"Сервіс {typeof(T).Name} не зареєстровано — його має створити GameBootstrap.");
        }

        public static bool TryGet<T>(out T? service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        public static void Clear() => Services.Clear();
    }

    /// <summary>
    /// Перемикачі незавершених фіч (§17). SocialEnabled лишається false до Фази 6:
    /// код може існувати, але гравець не побачить напівготової соціалки.
    /// </summary>
    public static class FeatureFlags
    {
        public static bool SocialEnabled { get; set; }
        public static bool AdsEnabled { get; set; }
        public static bool IapEnabled { get; set; }
        public static bool AnalyticsEnabled { get; set; } = true;
    }
}
