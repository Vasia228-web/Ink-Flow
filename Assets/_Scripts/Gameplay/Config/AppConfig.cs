using System.Collections.Generic;
using InkFlow.Meta;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// AppConfig.asset — зовнішні посилання й локалі (майстер-док §15): політика приватності, пошта
    /// підтримки, мови. Рядки живуть тут, не в коді: коли автор заведе сторінку політики й скриньку,
    /// він впише їх у асет, і рядки «скоро» на екрані налаштувань стануть живими.
    /// </summary>
    [CreateAssetMenu(fileName = "AppConfig", menuName = "Ink Flow/App Config")]
    public sealed class AppConfig : ScriptableObject
    {
        [Header("Інші налаштування (§15)")]
        [Tooltip("Повна адреса сторінки політики приватності. Порожньо — рядок «скоро».")]
        [SerializeField] private string privacyPolicyUrl = string.Empty;

        [Tooltip("Пошта підтримки. Порожньо — рядок «скоро».")]
        [SerializeField] private string supportEmail = string.Empty;

        [Tooltip("Коди локалей гри. Поки одна — рядка «Мова» в налаштуваннях немає (рішення Сесії 1).")]
        [SerializeField] private List<string> locales = new List<string> { AppLinks.DefaultLocale };

        [Header("Профіль (§14)")]
        [Tooltip("Найкоротший нік, символів.")]
        [SerializeField, Min(1)] private int nickMinLength = 3;

        [Tooltip("Найдовший нік, символів: довший не влізе в шапку хаба.")]
        [SerializeField, Min(1)] private int nickMaxLength = 16;

        [Tooltip("Корені образливих слів: нік, що містить будь-який із них (без регістру, латинські двійники літер і цифри-двійники рахуються), не приймається.")]
        [SerializeField] private List<string> bannedNickWords = new List<string>(NickRules.DefaultBannedWords);

        [Header("Рейтинги (§16, Unity Gaming Services)")]
        [Tooltip("Ідентифікатори таблиць у дашборді UGS Leaderboards: планети за тиждень (зі скиданням щопонеділка 00:00 UTC) і за весь час, галактики так само. Порожньо — таблиці не підключені.")]
        [SerializeField] private string leaderboardPlanetsWeek = "planets_week";
        [SerializeField] private string leaderboardPlanetsAll = "planets_all";
        [SerializeField] private string leaderboardGalaxiesWeek = "galaxies_week";
        [SerializeField] private string leaderboardGalaxiesAll = "galaxies_all";

        [Tooltip("Ключ публічної вітрини в Cloud Save (публічний доступ на читання).")]
        [SerializeField] private string cloudShowcaseKey = "showcase";

        [Tooltip("Скільки місць просить екран рейтингів: подіум плюс список; це й розмір мережевої відповіді.")]
        [SerializeField, Min(3)] private int leaderboardPageSize = 50;

        public string LeaderboardPlanetsWeek => leaderboardPlanetsWeek?.Trim() ?? string.Empty;
        public string LeaderboardPlanetsAll => leaderboardPlanetsAll?.Trim() ?? string.Empty;
        public string LeaderboardGalaxiesWeek => leaderboardGalaxiesWeek?.Trim() ?? string.Empty;
        public string LeaderboardGalaxiesAll => leaderboardGalaxiesAll?.Trim() ?? string.Empty;
        public string CloudShowcaseKey => cloudShowcaseKey?.Trim() ?? string.Empty;
        public int LeaderboardPageSize => Mathf.Max(3, leaderboardPageSize);

        public AppLinks ToAppLinks() => new AppLinks(privacyPolicyUrl?.Trim(), supportEmail?.Trim(), locales);

        public NickRules ToNickRules()
        {
            var rules = new NickRules(Mathf.Max(1, nickMinLength), Mathf.Max(Mathf.Max(1, nickMinLength), nickMaxLength), bannedNickWords);
            // Порожній список — фільтр вимкнено. Це право автора, але не мовчки: §14 фільтр вимагає.
            if (rules.BannedWords.Count == 0)
                Debug.LogWarning("[InkFlow] AppConfig.bannedNickWords порожній — фільтр образливих ніків (§14) вимкнено.");
            return rules;
        }
    }
}
