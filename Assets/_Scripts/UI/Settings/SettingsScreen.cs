using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>Аргументи налаштувань: звідки відкрито. З забігу з'являється «Заново», а «Додому» питає підтвердження.</summary>
    public sealed class SettingsArgs : ScreenArgs
    {
        public SettingsArgs() { }

        public SettingsArgs(bool inRun) => InRun = inRun;

        public bool InRun { get; }
    }

    /// <summary>
    /// Налаштування (майстер-док §15): перемикачі звуку, музики й вібрації, що реально керують
    /// аудіо й гаптикою і зберігаються одразу; «Додому», «Заново» (лише в забігу), «Колекція»;
    /// «Інші налаштування» — приховати профіль у рейтингах, мова (лише коли локалей ≥ 2 — рішення
    /// Сесії 1), політика приватності й підтримка (поки «скоро», доки конфіг порожній), версія.
    /// Кнопки «Більше ігор» немає й не буде.
    /// </summary>
    public sealed class SettingsScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;

        [Header("Звук")]
        [SerializeField] private TMP_Text soundCaption;
        [SerializeField] private SettingsToggle soundToggle;
        [SerializeField] private SettingsToggle musicToggle;
        [SerializeField] private SettingsToggle vibrationToggle;

        [Header("Дії")]
        [SerializeField] private Button homeButton;
        [SerializeField] private TMP_Text homeLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private TMP_Text restartLabel;
        [SerializeField] private Button collectionButton;
        [SerializeField] private TMP_Text collectionLabel;

        [Header("Інші налаштування")]
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform othersCard;
        [SerializeField] private TMP_Text othersCaption;
        [SerializeField] private SettingsToggle hideProfileToggle;
        [SerializeField] private Button languageButton;
        [SerializeField] private TMP_Text languageLabel;
        [SerializeField] private TMP_Text languageValue;
        [SerializeField] private Button privacyButton;
        [SerializeField] private TMP_Text privacyLabel;
        [SerializeField] private TMP_Text privacyHint;
        [SerializeField] private Button supportButton;
        [SerializeField] private TMP_Text supportLabel;
        [SerializeField] private TMP_Text supportHint;
        [SerializeField] private TMP_Text versionLabel;

        private AppLinks _links = AppLinks.Default;
        private bool _inRun;

        /// <summary>Назад туди, звідки відкрили.</summary>
        public System.Action? BackRequested;

        /// <summary>«Додому» — у хаб; з забігу роутер спершу питає підтвердження.</summary>
        public System.Action? HomeRequested;

        /// <summary>«Заново» — лише в забігу.</summary>
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

        /// <summary>Тестам: відстань від низу «Додому» до наступної видимої кнопки — схований «Заново» не лишає дірки.</summary>
        public float PreviewGapBelowHome =>
            homeButton == null || collectionButton == null || restartButton == null
                ? 0f
                : GapBetween((RectTransform)homeButton.transform, (RectTransform)(_inRun ? restartButton : collectionButton).transform);

        /// <summary>Тестам: відстань від низу «Колекції» до підпису «Інші налаштування».</summary>
        public float PreviewGapAboveOthers =>
            collectionButton == null || othersCaption == null
                ? 0f
                : GapBetween((RectTransform)collectionButton.transform, othersCaption.rectTransform);

        private static float GapBetween(RectTransform above, RectTransform below) => above.offsetMin.y - below.offsetMax.y;
        public bool PreviewLanguageShown => languageButton != null && languageButton.gameObject.activeSelf;
        public bool PreviewPrivacyEnabled => privacyButton != null && privacyButton.interactable;
        public string PreviewVersion => versionLabel != null ? versionLabel.text : string.Empty;
#endif

        /// <summary>Посилання й локалі — з конфігу через композиційний корінь.</summary>
        public void BindLinks(AppLinks links)
        {
            _links = links ?? AppLinks.Default;
        }

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (homeButton != null)
                homeButton.onClick.AddListener(() => HomeRequested?.Invoke());
            if (restartButton != null)
                restartButton.onClick.AddListener(() => RestartRequested?.Invoke());
            if (collectionButton != null)
                collectionButton.onClick.AddListener(() => CollectionRequested?.Invoke());
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

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary, FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "НАЛАШТУВАННЯ";
            ApplyFont(soundCaption, design.FontSizeCaption, design.TextMuted, FontStyles.Bold, design.LetterSpacingWide);
            if (soundCaption != null) soundCaption.text = "ЗВУК І ВІБРАЦІЯ";
            ApplyFont(othersCaption, design.FontSizeCaption, design.TextMuted, FontStyles.Bold, design.LetterSpacingWide);
            if (othersCaption != null) othersCaption.text = "ІНШІ НАЛАШТУВАННЯ";

            var settings = State?.Settings ?? new SettingsData();
            soundToggle?.Bind("Звук", settings.Sound, on => State?.SetSound(on));
            musicToggle?.Bind("Музика", settings.Music, on => State?.SetMusic(on));
            vibrationToggle?.Bind("Вібрація", settings.Vibration, on => State?.SetVibration(on));
            hideProfileToggle?.Bind("Приховати профіль у рейтингах", settings.ProfileHidden, on => State?.SetProfileHidden(on));

            ApplyFont(homeLabel, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (homeLabel != null) homeLabel.text = "Додому";
            ApplyFont(restartLabel, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (restartLabel != null) restartLabel.text = "Заново";
            ApplyFont(collectionLabel, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (collectionLabel != null) collectionLabel.text = "Колекція";

            ApplyRow(languageLabel, languageValue, "Мова", LanguageName(settings.Language), true);
            Toggle(languageButton, _links.HasLanguageChoice);
            if (languageButton != null)
                languageButton.interactable = _links.HasLanguageChoice;

            // Посилання без адреси — «скоро», неактивний рядок: мертва кнопка гірша за відсутність,
            // а відсутній рядок гірший за чесне «скоро» — гравець має знати, що воно буде.
            ApplyRow(privacyLabel, privacyHint, "Політика приватності", _links.HasPrivacyPolicy ? "›" : "скоро", _links.HasPrivacyPolicy);
            if (privacyButton != null) privacyButton.interactable = _links.HasPrivacyPolicy;
            ApplyRow(supportLabel, supportHint, "Написати в підтримку", _links.HasSupport ? "›" : "скоро", _links.HasSupport);
            if (supportButton != null) supportButton.interactable = _links.HasSupport;

            ApplyFont(versionLabel, design.FontSizeCaption, design.TextDim, FontStyles.Normal, 0f);
            if (versionLabel != null)
                versionLabel.text = $"Ink Flow · версія {Application.version}";

            LayoutActions();
            LayoutOthers();
        }

        /// <summary>
        /// «Заново» є лише в забігу: поза ним перезапускати нічого, і «Колекція» з усім, що нижче,
        /// підтягується на його місце — схована кнопка не лишає порожньої смуги. Висота й проміжок —
        /// з префаба («Додому» й «Заново» ніколи не рухаються), відстані до підпису й картки «Інші» —
        /// теж звідти: їх зсуваємо разом із «Колекцією», тож різниці сталі.
        /// </summary>
        private void LayoutActions()
        {
            if (homeButton == null || restartButton == null || collectionButton == null || othersCaption == null || othersCard == null)
                return;
            var home = (RectTransform)homeButton.transform;
            var restart = (RectTransform)restartButton.transform;
            var collection = (RectTransform)collectionButton.transform;
            var caption = othersCaption.rectTransform;
            var height = home.offsetMax.y - home.offsetMin.y;
            var gap = home.offsetMin.y - restart.offsetMax.y;
            var captionGap = collection.offsetMin.y - caption.offsetMax.y;
            var cardGap = caption.offsetMin.y - othersCard.offsetMax.y;

            var y = home.offsetMin.y - gap;
            y = PlaceRow(restartButton, y, height, _inRun);
            if (_inRun)
                y -= gap;
            y = PlaceRow(collectionButton, y, height, true) - captionGap;
            y = PlaceRow(othersCaption, y, caption.offsetMax.y - caption.offsetMin.y, true) - cardGap;
            MoveTop(othersCard, y);
        }

        /// <summary>Зсунути прямокутник так, щоб його верх став на <paramref name="top"/>, зберігши висоту.</summary>
        private static void MoveTop(RectTransform rect, float top)
        {
            var height = rect.offsetMax.y - rect.offsetMin.y;
            rect.offsetMax = new Vector2(rect.offsetMax.x, top);
            rect.offsetMin = new Vector2(rect.offsetMin.x, top - height);
        }

        /// <summary>
        /// Рядки картки «Інші налаштування» лягають один під одним із тих, що видимі: схований рядок мови
        /// не лишає дірки, картка й вміст скролу вкорочуються. Розміри рядків — із префаба (перший рядок),
        /// тут лише порядок; розкладка ставиться при вході, не щокадру.
        /// </summary>
        private void LayoutOthers()
        {
            if (othersCard == null || hideProfileToggle == null)
                return;
            var first = (RectTransform)hideProfileToggle.transform;
            var rowHeight = first.offsetMax.y - first.offsetMin.y;
            var padding = -first.offsetMax.y;
            var y = first.offsetMin.y;
            y = PlaceRow(languageButton, y, rowHeight, _links.HasLanguageChoice);
            y = PlaceRow(privacyButton, y, rowHeight, true);
            y = PlaceRow(supportButton, y, rowHeight, true);
            if (versionLabel != null)
            {
                var rect = versionLabel.rectTransform;
                var height = rect.offsetMax.y - rect.offsetMin.y;
                rect.offsetMax = new Vector2(rect.offsetMax.x, y);
                rect.offsetMin = new Vector2(rect.offsetMin.x, y - height);
                y -= height;
            }
            othersCard.offsetMin = new Vector2(othersCard.offsetMin.x, othersCard.offsetMax.y + y - padding);
            if (content != null)
                content.sizeDelta = new Vector2(content.sizeDelta.x, -othersCard.offsetMin.y + padding * 4f);
        }

        private static float PlaceRow(Component? row, float y, float height, bool shown)
        {
            Toggle(row, shown);
            if (row == null || !shown)
                return y;
            var rect = (RectTransform)row.transform;
            rect.offsetMax = new Vector2(rect.offsetMax.x, y);
            rect.offsetMin = new Vector2(rect.offsetMin.x, y - height);
            return y - height;
        }

        private void ApplyRow(TMP_Text? label, TMP_Text? value, string text, string valueText, bool active)
        {
            ApplyFont(label, design.FontSizeBody, active ? design.TextPrimary : design.TextDim, FontStyles.Bold, 0f);
            if (label != null)
            {
                label.text = text;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.enableAutoSizing = true;
                label.fontSizeMax = design.FontSizeBody;
                label.fontSizeMin = design.FontSizeBody * design.SettingsLabelMinScale;
            }
            ApplyFont(value, design.FontSizeBody, active ? design.TextMuted : design.TextDim, FontStyles.Normal, 0f);
            if (value != null) value.text = valueText;
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

        private void ApplyFont(TMP_Text? label, float size, Color color, FontStyles style, float spacing)
        {
            if (label == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.characterSpacing = spacing;
            if (design.Font != null)
                label.font = design.Font;
        }
    }
}
