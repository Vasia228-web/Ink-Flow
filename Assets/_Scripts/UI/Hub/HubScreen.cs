using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Головний екран U1Glaze (S01Home): шапка «аватар + нік | капсула нафти | шестерня», лого SpacePaint
    /// з підписом, заставка (планета з орбітою блоків — стискається першою на малих екранах), картка
    /// «Нескінченний» (бейджі «N КАРТИНОК» і «РЕКОРД», мініатюра, коралова «Грати») і нижня навігація.
    /// Розкладку тримають групи (VerticalLayoutGroup зверху вниз), числа — токени й px макета у збирачі.
    ///
    /// Другої картки режиму немає: режим у запасі (Reserve/LevelsMode). «+» на капсулі теж у запасі.
    /// Екран не знає, куди ведуть кнопки: «Грати» — EndlessRequested, вкладка — TabRequested, аватар з ніком
    /// (одна кнопка на весь блок) — ProfileRequested, шестерня — SettingsRequested базового екрана.
    /// </summary>
    public sealed class HubScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Image avatar;

        [Tooltip("Аватари avatar_1…8 з набору U1Glaze; номер — ProfileData.AvatarId (поза набором — перший).")]
        [SerializeField] private Sprite[] avatarSprites = System.Array.Empty<Sprite>();

        [SerializeField] private TMP_Text playerName;

        /// <summary>Увесь блок «аватар + нік» — кнопка в Профіль.</summary>
        [SerializeField] private Button profileButton;

        [SerializeField] private CurrencyWidget currency;
        [SerializeField] private Button settingsButton;

        [Header("Нескінченний")]
        [SerializeField] private TMP_Text picturesBadge;
        [SerializeField] private TMP_Text recordBadge;
        [SerializeField] private CandyButton playButton;

        [Tooltip("Мініатюра праворуч у картці: картинка поточного забігу, інакше вітринна, інакше нічого.")]
        [SerializeField] private RectTransform thumbnail;
        [SerializeField] private RarityFrameView thumbnailFrame;
        [SerializeField] private PictureView thumbnailPicture;

        [Header("Навігація")]
        [SerializeField] private NavBar navBar;

        [Header("Мокові дані")]
        // Мокові числа лишаються ТІЛЬКИ для сцени-майстерні Hub.unity: там стану гравця немає, а порожній
        // екран нічого не показав би про розкладку. У грі всі вони перекриваються реальними.
        [SerializeField] private string mockName = "Гравець";
        [SerializeField] private long mockOil = 1240;
        [SerializeField] private int mockRecord = 7980;
        [SerializeField] private int mockPictures = 14;

        /// <summary>«Грати» на картці «Нескінченний».</summary>
        public System.Action? EndlessRequested;

        /// <summary>Вкладка нижньої навігації: <see cref="NavBar.Play"/> / galaxy / collection / ranks.</summary>
        public System.Action<string>? TabRequested;

        /// <summary>Тап на блок «аватар + нік» у шапці.</summary>
        public System.Action? ProfileRequested;

        private PixelPicture? _thumbnailShown;
        private string _thumbnailKey = string.Empty;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        /// <summary>Тести: навігація екрана.</summary>
        public NavBar? PreviewNavBar => navBar;

        /// <summary>Тести: «Грати», як пальцем.</summary>
        public void PreviewPlay() => playButton?.GetComponent<Button>()?.onClick.Invoke();

        /// <summary>Тести: тап по блоку профілю.</summary>
        public void PreviewProfile() => profileButton?.onClick.Invoke();

        public string PreviewPicturesBadge => picturesBadge != null ? picturesBadge.text : string.Empty;
        public string PreviewRecordBadge => recordBadge != null ? recordBadge.text : string.Empty;
        public TMP_Text? PreviewNick => playerName;
        public RectTransform? PreviewAvatar => avatar != null ? avatar.rectTransform : null;
        public RectTransform? PreviewProfileButton => profileButton != null ? (RectTransform)profileButton.transform : null;

        /// <summary>Тести: яку картинку показує мініатюра (null — схована).</summary>
        public PixelPicture? PreviewThumbnail => thumbnail != null && thumbnail.gameObject.activeSelf ? _thumbnailShown : null;
#endif

        private void Awake()
        {
            WireSettingsButton(settingsButton);
            // Хаб не знає, куди ведуть його кнопки — лише повідомляє, що їх натиснули.
            var play = playButton != null ? playButton.GetComponent<Button>() : null;
            if (play != null)
                play.onClick.AddListener(() => EndlessRequested?.Invoke());
            if (navBar != null)
                navBar.TabSelected += id => TabRequested?.Invoke(id);
            if (profileButton != null)
                profileButton.onClick.AddListener(() => ProfileRequested?.Invoke());
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Apply();
        }

        /// <summary>Підв'язати справжній гаманець замість мокових даних.</summary>
        public void Bind(Wallet wallet) => currency?.Bind(wallet);

        public override void BindState(PlayerState state)
        {
            base.BindState(state);
            Bind(state.Wallet);
            StyleRefresh.Schedule(this, Apply);
        }

        /// <summary>Перечитати стан без повторного входу на екран.</summary>
        public void Refresh() => Apply();

        public void Apply()
        {
            if (design == null)
                return;

            if (playerName != null)
                playerName.text = State?.Nick ?? mockName;

            if (avatar != null && avatarSprites.Length > 0)
            {
                var id = State?.AvatarId ?? 0;
                avatar.sprite = avatarSprites[id >= 0 && id < avatarSprites.Length ? id : 0];
            }

            // §8: головний рекорд — скільки різних картинок у колекції; рекорд забігу — числом з нерозривними пробілами.
            var pictures = State != null ? State.Collection.Distinct : mockPictures;
            if (picturesBadge != null)
                picturesBadge.text = Plural.Count(pictures, "КАРТИНКА", "КАРТИНКИ", "КАРТИНОК");
            var record = State != null ? State.Progress.EndlessRecord : mockRecord;
            if (recordBadge != null)
                recordBadge.text = $"РЕКОРД {ScoreFormat.Full(record)}";

            ApplyThumbnail();

            // У грі валюту показує підписка на гаманець (Bind), у майстерні — разове число.
            if (State == null)
                currency?.SetPreviewAmount(mockOil);
            currency?.Apply();
            playButton?.Apply();
            navBar?.SetActive(NavBar.Play);
        }

        /// <summary>
        /// Мініатюра: картинка перерваного забігу з її прогресом («ось що ти малюєш»), інакше вітринна з профілю
        /// цілком, інакше блок схований — текст картки займає місце. Та сама картинка в тому самому стані не
        /// перебудовує маску вдруге.
        /// </summary>
        private void ApplyThumbnail()
        {
            if (thumbnail == null)
                return;
            PixelPicture? picture = null;
            PictureProgress? progress = null;
            var key = string.Empty;
            var run = State?.SavedRun;
            if (run != null && State != null)
            {
                picture = State.Library.Find(run.PictureId);
                if (picture != null)
                {
                    progress = new PictureProgress(picture, State.Library.IndexOf(picture.Id));
                    progress.Restore(run.FilledSteps);
                    key = $"run:{picture.Id}:{progress.FilledCount}";
                }
            }
            if (picture == null && State != null && State.ShowcasePictureId is { } showcaseId)
            {
                picture = State.Library.Find(showcaseId);
                key = picture != null ? $"showcase:{picture.Id}" : string.Empty;
            }

            var visible = picture != null;
            if (thumbnail.gameObject.activeSelf != visible)
                thumbnail.gameObject.SetActive(visible);
            if (picture == null)
            {
                _thumbnailShown = null;
                _thumbnailKey = string.Empty;
                return;
            }

            thumbnailFrame?.SetRarity(picture.Rarity);
            if (key == _thumbnailKey || thumbnailPicture == null)
                return;
            if (progress != null)
                thumbnailPicture.Show(progress);
            else
                thumbnailPicture.ShowCompleted(picture, string.Empty);
            _thumbnailShown = picture;
            _thumbnailKey = key;
        }
    }
}
