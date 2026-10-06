using System;
using System.Collections.Generic;

namespace InkFlow.Meta
{
    /// <summary>
    /// Зовнішні посилання й локалі застосунку (майстер-док §15, «Інші налаштування»): політика
    /// приватності, пошта підтримки, доступні мови. POCO без Unity: екран налаштувань отримує його
    /// з конфігу через композиційний корінь. Порожнє посилання — рядок «скоро», а не мертва кнопка.
    ///
    /// Рішення Сесії 1 щодо мови: мова одна, тож рядка мови на екрані немає — він з'являється лише
    /// коли локалей у конфігу дві й більше. Місце під мову зарезервоване кодом, не екраном.
    /// </summary>
    public sealed class AppLinks
    {
        public AppLinks(string? privacyPolicyUrl = null, string? supportEmail = null, IReadOnlyList<string>? locales = null)
        {
            PrivacyPolicyUrl = privacyPolicyUrl ?? string.Empty;
            SupportEmail = supportEmail ?? string.Empty;
            Locales = locales is { Count: > 0 } ? locales : new[] { DefaultLocale };
        }

        public const string DefaultLocale = "uk";

        /// <summary>Повна адреса політики приватності; порожньо — ще немає.</summary>
        public string PrivacyPolicyUrl { get; }

        /// <summary>Пошта підтримки; порожньо — ще немає.</summary>
        public string SupportEmail { get; }

        /// <summary>Коди локалей, якими говорить гра. Одна — рядка мови в налаштуваннях немає.</summary>
        public IReadOnlyList<string> Locales { get; }

        public bool HasPrivacyPolicy => PrivacyPolicyUrl.Length > 0;
        public bool HasSupport => SupportEmail.Length > 0;

        /// <summary>Чи показувати вибір мови: лише коли є з чого вибирати.</summary>
        public bool HasLanguageChoice => Locales.Count >= 2;

        /// <summary>Посилання «написати в підтримку» для <c>Application.OpenURL</c>.</summary>
        public string SupportMailto => HasSupport ? $"mailto:{SupportEmail}" : string.Empty;

        public static AppLinks Default { get; } = new AppLinks();
    }
}
