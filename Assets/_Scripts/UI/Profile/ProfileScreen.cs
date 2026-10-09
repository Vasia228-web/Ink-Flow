using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Профіль (майстер-док §14): аватар із набору крапель, нік і вітринна картинка з колекції — те, що
    /// бачать інші гравці. Драбини звань, статистики, палітри, вітрини планет і бейджів немає (Фаза 6
    /// прибрала). Скролиться цілком разом із шапкою — так у макеті. Усе читається зі стану на кожен
    /// вхід; без стану (майстерня) — мокові значення й порожня вітрина.
    /// </summary>
    public sealed class ProfileScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button settingsButton;

        [Header("Візитка")]
        [SerializeField] private RectTransform identityCard;
        [SerializeField] private DropView avatar;
        [SerializeField] private Button editNickButton;
        [SerializeField] private GradientImage editNickFill;
        [SerializeField] private TMP_Text nickLabel;
        [SerializeField] private TMP_Text nickHint;

        [Header("Аватар")]
        [SerializeField] private TMP_Text avatarCaption;
        [Tooltip("Краплі набору — прості градієнтні кола (одна текстура на ряд), не DropView: без LateUpdate і без власного обробника тапу.")]
        [SerializeField] private GradientImage[] avatarOptions = System.Array.Empty<GradientImage>();
        [SerializeField] private Button[] avatarButtons = System.Array.Empty<Button>();
        [SerializeField] private Image[] avatarRings = System.Array.Empty<Image>();

        [Header("Вітрина")]
        [SerializeField] private TMP_Text showcaseCaption;
        [SerializeField] private PictureView showcasePicture;
        [SerializeField] private RectTransform showcaseEmpty;
        [SerializeField] private Image showcaseEmptyFrame;
        [SerializeField] private TMP_Text showcaseEmptyGlyph;
        [SerializeField] private TMP_Text showcaseHint;
        [SerializeField] private Button showcaseButton;
        [SerializeField] private GradientImage showcaseButtonFill;
        [SerializeField] private TMP_Text showcaseButtonLabel;

        [Header("Мокові дані")]
        [SerializeField] private string mockNick = "Нова";

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        /// <summary>Гравець хоче змінити нік — олівець біля аватара.</summary>
        public System.Action? NickEditRequested;

        /// <summary>«Обрати з колекції» — колекція в режимі вибору вітринної картинки.</summary>
        public System.Action? ShowcasePickRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        public InkColor PreviewAvatarInk => avatar != null ? avatar.Ink : InkColor.None;
        public bool PreviewAvatarShowsNumber => avatar != null && avatar.ShowsNumber;
        public string PreviewNick => nickLabel != null ? nickLabel.text : string.Empty;

        /// <summary>Тестам: скільки рядків зайняв нік після розкладки (має бути один).</summary>
        public int PreviewNickLineCount
        {
            get
            {
                if (nickLabel == null)
                    return 0;
                nickLabel.ForceMeshUpdate();
                return nickLabel.textInfo.lineCount;
            }
        }

        /// <summary>Тестам: зсунути краплю-аватар на <paramref name="dy"/> одиниць (перевірка, що сторож перекриття бачить зсув).</summary>
        public void PreviewShiftAvatar(float dy)
        {
            if (avatar == null)
                return;
            var rect = (RectTransform)avatar.transform;
            rect.anchoredPosition += new Vector2(0f, dy);
        }
        public string? PreviewShowcaseId => showcasePicture != null && showcasePicture.gameObject.activeSelf ? showcasePicture.Picture?.Id : null;
        public bool PreviewCanPickShowcase => showcaseButton != null && showcaseButton.interactable;
        public int PreviewSelectedAvatar
        {
            get
            {
                for (var i = 0; i < avatarRings.Length; i++)
                    if (avatarRings[i] != null && avatarRings[i].gameObject.activeSelf)
                        return i;
                return -1;
            }
        }

        /// <summary>Тестам: тап по аватару з набору, як пальцем.</summary>
        public void PreviewPickAvatar(int index) => OnAvatarTapped(index);

        /// <summary>
        /// Тестам: на скільки ЛОКАЛЬНИХ одиниць картки верх краплі-аватара стирчить над верхом візитки
        /// (додатне — обрізається маскою скролу; старий баг «аватар обрізаний згори карткою»). Світові одиниці
        /// стенда тут не годяться: на канвасі Screen Space – Camera пів одиниці — це ~100 px.
        /// </summary>
        public float PreviewAvatarOverflow
        {
            get
            {
                if (avatar == null || identityCard == null)
                    return 0f;
                var corners = new Vector3[4];
                ((RectTransform)avatar.transform).GetWorldCorners(corners);
                var avatarTopLocal = identityCard.InverseTransformPoint(corners[1]).y;
                return avatarTopLocal - identityCard.rect.yMax;
            }
        }
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
            if (editNickButton != null)
                editNickButton.onClick.AddListener(() => NickEditRequested?.Invoke());
            if (showcaseButton != null)
                showcaseButton.onClick.AddListener(() => ShowcasePickRequested?.Invoke());

            for (var i = 0; i < avatarButtons.Length; i++)
            {
                var index = i;
                avatarButtons[i]?.onClick.AddListener(() => OnAvatarTapped(index));
            }
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Apply();
        }

        /// <summary>Перечитати стан без повторного входу на екран (після зміни ніка).</summary>
        public void Refresh() => Apply();

        public void Apply()
        {
            if (design == null)
                return;

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary, FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "ПРОФІЛЬ";
            Caption(avatarCaption, "АВАТАР");
            Caption(showcaseCaption, "ВІТРИНА");

            ApplyIdentity();
            ApplyAvatars();
            ApplyShowcase();
        }

        private void ApplyIdentity()
        {
            var avatarId = State?.AvatarId ?? 0;
            // 0 — без числа густоти: з префаба крапля приходить із «1» (старий баг на аватарі).
            avatar?.Show(AvatarSet.InkOf(avatarId), 0);

            if (editNickFill != null)
                editNickFill.SetGradient(design.AccentTeal, design.AccentBlue);

            ApplyFont(nickLabel, design.FontSizeProfileNick, design.TextPrimary, FontStyles.Bold, 0f);
            if (nickLabel != null)
            {
                // Нік — завжди один рядок: 16 широких літер стискаються, а не переносяться на підказку.
                nickLabel.enableAutoSizing = true;
                nickLabel.fontSizeMax = design.FontSizeProfileNick;
                nickLabel.fontSizeMin = design.FontSizeProfileNick * design.NickMinScale;
                nickLabel.text = State?.Nick ?? mockNick;
            }

            ApplyFont(nickHint, design.FontSizeLabel, design.TextMuted, FontStyles.Normal, 0f);
            if (nickHint != null) nickHint.text = "Нік і аватар бачать інші гравці";
        }

        private void ApplyAvatars()
        {
            var selected = State?.AvatarId ?? 0;
            for (var i = 0; i < avatarOptions.Length; i++)
            {
                var exists = i < AvatarSet.Count;
                if (i < avatarButtons.Length)
                    Toggle(avatarButtons[i], exists);
                if (exists && avatarOptions[i] != null)
                {
                    design.AvatarGradient(AvatarSet.InkOf(i), false, out var from, out var to);
                    avatarOptions[i].SetGradient(from, to);
                }
                if (i < avatarRings.Length && avatarRings[i] != null)
                {
                    // Обраний обведено — інакше незрозуміло, який із шести зараз на візитці.
                    avatarRings[i].color = design.TextPrimary;
                    Toggle(avatarRings[i], exists && i == selected);
                }
            }
        }

        private void ApplyShowcase()
        {
            var id = State?.ShowcasePictureId;
            var picture = id != null && State != null ? State.Library.Find(id) : null;

            // Та сама картинка — не перераховуємо маску й гало на кожен вхід чи Refresh.
            var same = showcasePicture != null && showcasePicture.gameObject.activeSelf && showcasePicture.Picture == picture;
            Toggle(showcasePicture, picture != null);
            if (picture != null && showcasePicture != null && !same)
                showcasePicture.ShowCompleted(picture, string.Empty);

            // Без картинки — порожня рамка зі знаком питання на тому ж місці: картка не западає, а чекає.
            Toggle(showcaseEmpty, picture == null);
            if (showcaseEmptyFrame != null)
                showcaseEmptyFrame.color = design.GlassStroke;
            ApplyFont(showcaseEmptyGlyph, design.FontSizeProfileNick, design.TextDim, FontStyles.Bold, 0f);
            if (showcaseEmptyGlyph != null) showcaseEmptyGlyph.text = "?";

            // Гравець має знати, обрав він вітрину сам чи її підставила гра (остання домальована).
            ApplyFont(showcaseHint, design.FontSizeLabel, design.TextMuted, FontStyles.Normal, 0f);
            if (showcaseHint != null)
                showcaseHint.text = picture == null
                    ? "Домалюй першу картинку в забігу — вона стане вітриною"
                    : State != null && State.IsShowcaseChosen
                        ? $"{picture.Name} · {RarityNames.Of(picture.Rarity)} — твоя вітрина, її бачать інші гравці"
                        : $"{picture.Name} · {RarityNames.Of(picture.Rarity)} — остання домальована, поки ти не обереш свою";

            var canPick = State != null && State.Collection.Distinct > 0;
            if (showcaseButton != null)
                showcaseButton.interactable = canPick;
            if (showcaseButtonFill != null)
            {
                if (canPick) showcaseButtonFill.SetGradient(design.AccentTeal, design.AccentBlue);
                else showcaseButtonFill.SetGradient(design.GlassFill, design.GlassFill);
            }
            ApplyFont(showcaseButtonLabel, design.FontSizeSubtitle, canPick ? design.TextPrimary : design.TextDim, FontStyles.Bold, 0f);
            if (showcaseButtonLabel != null) showcaseButtonLabel.text = "Обрати з колекції";
        }

        private void OnAvatarTapped(int index)
        {
            if (index < 0 || index >= AvatarSet.Count)
                return;
            State?.SetAvatar(index);
            // Лише те, що змінилось: крапля на візитці й два кільця, а не весь екран.
            avatar?.Show(AvatarSet.InkOf(index), 0);
            for (var i = 0; i < avatarRings.Length; i++)
                Toggle(avatarRings[i], i == index);
            avatar?.PlayLand();
        }

        private void Caption(TMP_Text? label, string text)
        {
            if (label == null)
                return;
            label.text = text;
            ApplyFont(label, design.FontSizeSmall, design.TextFaint, FontStyles.Bold, design.LetterSpacingWide);
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
