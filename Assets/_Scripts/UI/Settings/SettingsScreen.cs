using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>Аргументи налаштувань: звідки відкрито. З забігу з'являється «Почати заново», а «Додому» питає підтвердження.</summary>
    public sealed class SettingsArgs : ScreenArgs
    {
        public SettingsArgs() { }

        public SettingsArgs(bool inRun) => InRun = inRun;

        public bool InRun { get; }
    }

    /// <summary>
    /// Налаштування (майстер-док §15, U1Glaze S11SettingsSoon_01, S12SmallScreen_11): «‹» і назва по центру;
    /// «ЗВУК І ВІБРАЦІЯ» — картка з трьома перемикачами, що реально керують аудіо й гаптикою і зберігаються одразу;
    /// ряд «Додому» (бірюзова) + «Колекція» (скло), під ним у забігу — «Почати заново»; «ІНШЕ» — приховати профіль
    /// у рейтингах (з поясненням), мова (лише коли локалей ≥ 2 — рішення Сесії 1), політика приватності й підтримка
    /// (сірий бейдж «Скоро», доки адреси в конфігу немає; текст рядка не сіріє — SCREENS.md); версія внизу екрана.
    /// Розкладка — групи розкладки: схований рядок не лишає дірки. Кнопки «Більше ігор» немає й не буде.
    /// </summary>
    public sealed class SettingsScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;

        [Header("Звук")]
        [SerializeField] private SettingsToggle soundToggle;
        [SerializeField] private SettingsToggle musicToggle;
        [SerializeField] private SettingsToggle vibrationToggle;

        [Header("Дії")]
        [SerializeField] private CandyButton homeButton;
        [SerializeField] private CandyButton collectionButton;
        [SerializeField] private CandyButton restartButton;

        [Header("Інше")]
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text othersCaption;
        [SerializeField] private SettingsToggle hideProfileToggle;
        [SerializeField] private Button languageButton;
        [SerializeField] private TMP_Text languageValue;
        [SerializeField] private Button privacyButton;
        [SerializeField] private GameObject privacySoon;
        [SerializeField] private GameObject privacyChevron;
        [SerializeField] private Button supportButton;
        [SerializeField] private GameObject supportSoon;
        [SerializeField] private GameObject supportChevron;
        [SerializeField] private TMP_Text versionLabel;

        private AppLinks _links = AppLinks.Default;
        private bool _inRun;

        /// <summary>Назад туди, звідки відкрили.</summary>
        public System.Action? BackRequested;

        /// <summary>«Додому» — у хаб; з забігу роутер спершу питає підтвердження.</summary>
        public System.Action? HomeRequested;

        /// <summary>«Почати заново» — лише в забігу.</summary>
        public System.Action? RestartRequested;

        /// <summary>«Колекція» — сітка зібраного, без вибору.</summary>
        public System.Action? CollectionRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        public SettingsToggle? PreviewSound => soundToggle;
        public SettingsToggle? PreviewMusic => musicToggle;
        public SettingsToggle? PreviewVibration => vibrationToggle;
        public SettingsToggle? PreviewHideProfile => hideProfileToggle;
        public bool PreviewRestartShown => restartButton != null && restartButton.gameObject.activeSelf;
        public bool PreviewLanguageShown => languageButton != null && languageButton.gameObject.activeSelf;
        public bool PreviewPrivacyEnabled => privacyButton != null && privacyButton.interactable;
        public bool PreviewPrivacySoon => privacySoon != null && privacySoon.activeSelf;
        public string PreviewVersion => versionLabel != null ? versionLabel.text : string.Empty;

        /// <summary>Тестам: верх підпису «ІНШЕ» відносно верху скролу (після перерахунку розкладки).</summary>
        public float PreviewOthersTop
        {
            get
            {
                if (content == null || othersCaption == null)
                    return 0f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                var rect = othersCaption.rectTransform;
                return content.InverseTransformPoint(rect.TransformPoint(new Vector3(0f, rect.rect.yMax, 0f))).y;
            }
        }
#endif

        public void BindLinks(AppLinks links)
        {
            _links = links ?? AppLinks.Default;
        }

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (homeButton != null)
                homeButton.Button.onClick.AddListener(() => HomeRequested?.Invoke());
            if (restartButton != null)
                restartButton.Button.onClick.AddListener(() => RestartRequested?.Invoke());
            if (collectionButton != null)
                collectionButton.Button.onClick.AddListener(() => CollectionRequested?.Invoke());
            if (privacyButton != null)
                privacyButton.onClick.AddListener(() => OpenLink(_links.PrivacyPolicyUrl));
            if (supportButton != null)
                supportButton.onClick.AddListener(() => OpenLink(_links.SupportMailto));
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _inRun = (args as SettingsArgs)?.InRun ?? false;
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            var settings = State?.Settings ?? new SettingsData();
            soundToggle?.Bind("Звук", settings.Sound, on => State?.SetSound(on));
            musicToggle?.Bind("Музика", settings.Music, on => State?.SetMusic(on));
            vibrationToggle?.Bind("Вібрація", settings.Vibration, on => State?.SetVibration(on));
            hideProfileToggle?.Bind("Приховати профіль у рейтингах", settings.ProfileHidden, on => State?.SetProfileHidden(on));

            Toggle(restartButton, _inRun);

            Toggle(languageButton, _links.HasLanguageChoice);
            if (languageValue != null)
                languageValue.text = LanguageName(settings.Language);

            // Посилання без адреси — «Скоро» й неактивний рядок: мертва кнопка гірша за відсутність,
            // а відсутній рядок гірший за чесне «Скоро» — гравець має знати, що воно буде.
            ApplyLink(privacyButton, privacySoon, privacyChevron, _links.HasPrivacyPolicy);
            ApplyLink(supportButton, supportSoon, supportChevron, _links.HasSupport);

            if (versionLabel != null)
                versionLabel.text = $"SpacePaint · версія {Application.version}";
        }

        private static void ApplyLink(Button? row, GameObject? soon, GameObject? chevron, bool configured)
        {
            if (row != null)
                row.interactable = configured;
            Toggle(soon, !configured);
            Toggle(chevron, configured);
        }

        private static string LanguageName(string? code) => code switch
        {
            "en" => "English",
            _ => "Українська"
        };

        private static void OpenLink(string url)
        {
            if (url is { Length: > 0 })
                Application.OpenURL(url);
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private static void Toggle(GameObject? target, bool on)
        {
            if (target != null && target.activeSelf != on)
                target.SetActive(on);
        }
    }
}
