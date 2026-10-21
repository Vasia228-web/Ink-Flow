using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Профіль (майстер-док §14, U1Glaze S10Profile_01): підекран з головного (тап по аватару) — «‹», капсула,
    /// шестерня; далі скрол із трьома картками: візитка (аватар 96, нік h1 з олівцем, «Нік і аватар бачать інші
    /// гравці»), «АВАТАР» (8 спрайтів-аватарів 4 × 2, обраний — у кільці) і «ВІТРИНА» (картинка в рамці рідкості,
    /// назва, бейдж рідкості, «Обрати з колекції»). Нік редагує діалог <see cref="NickPrompt"/> (через роутер).
    /// Звань, статистики, палітри й бейджів немає — §14.
    /// </summary>
    public sealed class ProfileScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private CurrencyWidget currency;

        [Header("Візитка")]
        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text nickLabel;
        [SerializeField] private Button editNickButton;

        [Header("Аватари")]
        [SerializeField] private Button[] avatarButtons = System.Array.Empty<Button>();
        [SerializeField] private Image[] avatarRings = System.Array.Empty<Image>();
        [SerializeField] private Sprite[] avatarSprites = System.Array.Empty<Sprite>();

        [Header("Вітрина")]
        [SerializeField] private RarityFrameView showcaseFrame;
        [SerializeField] private PictureView showcasePicture;
        [SerializeField] private GameObject showcaseEmpty;
        [SerializeField] private TMP_Text showcaseName;
        [SerializeField] private RarityBadgeView showcaseBadge;
        [SerializeField] private CandyButton showcaseButton;

        [Header("Мокові дані")]
        [SerializeField] private string mockNick = "Гравець";

        public System.Action? BackRequested;

        public System.Action? NickEditRequested;

        /// <summary>«Обрати з колекції» — колекція в режимі вибору вітрини.</summary>
        public System.Action? ShowcasePickRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        public string PreviewNick => nickLabel != null ? nickLabel.text : string.Empty;
        public Sprite? PreviewAvatar => avatar != null ? avatar.sprite : null;
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
        public string PreviewShowcaseName => showcaseName != null && showcaseName.gameObject.activeSelf ? showcaseName.text : string.Empty;
        public bool PreviewShowcaseEmpty => showcaseEmpty != null && showcaseEmpty.activeSelf;
        public bool PreviewCanPick => showcaseButton != null && showcaseButton.Interactable;
        public PixelPicture? PreviewShowcasePicture => showcasePicture != null && showcasePicture.gameObject.activeSelf ? showcasePicture.Picture : null;
        public void PreviewTapAvatar(int index) => OnAvatarTapped(index);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
            if (editNickButton != null)
                editNickButton.onClick.AddListener(() => NickEditRequested?.Invoke());
            if (showcaseButton != null)
                showcaseButton.Button.onClick.AddListener(() => ShowcasePickRequested?.Invoke());
            for (var i = 0; i < avatarButtons.Length; i++)
            {
                var index = i;
                avatarButtons[i]?.onClick.AddListener(() => OnAvatarTapped(index));
            }
        }

        public override void BindState(PlayerState state)
        {
            base.BindState(state);
            currency?.Bind(state.Wallet);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Apply();
        }

        /// <summary>Перечитати стан (нік змінено діалогом) — роутер кличе після збереження.</summary>
        public void Refresh() => Apply();

        public void Apply()
        {
            if (design == null)
                return;
            currency?.Apply();
            ApplyIdentity();
            ApplyAvatars(State?.AvatarId ?? 0);
            ApplyShowcase();
        }

        private Sprite? AvatarSprite(int id) =>
            avatarSprites.Length == 0 ? null : avatarSprites[id >= 0 && id < avatarSprites.Length ? id : 0];

        private void ApplyIdentity()
        {
            if (avatar != null)
                avatar.sprite = AvatarSprite(State?.AvatarId ?? 0) ?? avatar.sprite;
            if (nickLabel != null)
            {
                // Нік — завжди один рядок: 16 широких літер стискаються, а не переносяться.
                nickLabel.enableAutoSizing = true;
                nickLabel.fontSizeMax = design.UiH1;
                nickLabel.fontSizeMin = design.UiH1 * design.NickMinScale;
                nickLabel.text = State?.Nick ?? mockNick;
            }
        }

        private void ApplyAvatars(int selected)
        {
            for (var i = 0; i < avatarButtons.Length; i++)
            {
                var exists = i < AvatarSet.Count;
                Toggle(avatarButtons[i], exists);
                if (i < avatarRings.Length)
                    Toggle(avatarRings[i], exists && i == selected);
            }
        }

        /// <summary>
        /// Вітрина: обрана гравцем картинка, інакше остання домальована (<see cref="PlayerState.ShowcasePictureId"/>);
        /// без жодної зібраної — порожня рамка з поясненням і вимкнена кнопка.
        /// </summary>
        private void ApplyShowcase()
        {
            var id = State?.ShowcasePictureId;
            var library = State?.Library ?? PictureLibrary.Fallback;
            var picture = id != null ? library.Find(id) : State == null && library.Count > 0 ? library[0] : null;

            // Та сама картинка — не перераховуємо полотно на кожен вхід чи Refresh.
            var same = showcasePicture != null && showcasePicture.gameObject.activeSelf && showcasePicture.Picture == picture;
            Toggle(showcasePicture, picture != null);
            if (picture != null && showcasePicture != null && !same)
                showcasePicture.ShowCompleted(picture, string.Empty);
            Toggle(showcaseEmpty, picture == null);
            if (showcaseFrame != null)
                showcaseFrame.SetRarity(picture?.Rarity ?? Rarity.Common);

            Toggle(showcaseName, true);
            if (showcaseName != null)
                showcaseName.text = picture != null ? picture.Name : "Домалюй першу картинку — вона стане вітриною";
            Toggle(showcaseBadge, picture != null);
            if (picture != null)
                showcaseBadge?.SetRarity(picture.Rarity);

            var canPick = State != null ? State.Collection.Distinct > 0 : picture != null;
            if (showcaseButton != null)
                showcaseButton.Interactable = canPick;
        }

        private void OnAvatarTapped(int index)
        {
            if (index < 0 || index >= AvatarSet.Count)
                return;
            State?.SetAvatar(index);
            // Лише те, що змінилось: аватар на візитці й кільця.
            if (avatar != null)
                avatar.sprite = AvatarSprite(index) ?? avatar.sprite;
            ApplyAvatars(index);
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
