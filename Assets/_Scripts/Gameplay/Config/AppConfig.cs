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

        public AppLinks ToAppLinks() => new AppLinks(privacyPolicyUrl?.Trim(), supportEmail?.Trim(), locales);
    }
}
