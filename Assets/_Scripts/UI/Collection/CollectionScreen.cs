using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Аргументи колекції: просто подивитись (порожні) або обрати картинку в слот планети.
    /// Слот описується так само, як у збереженні: галактика, планета, номер.
    /// </summary>
    public sealed class CollectionArgs : ScreenArgs
    {
        public CollectionArgs() { }

        public CollectionArgs(int galaxy, string planetId, int slot)
        {
            Galaxy = galaxy;
            PlanetId = planetId;
            Slot = slot;
        }

        public int Galaxy { get; }
        public string? PlanetId { get; }
        public int Slot { get; }

        /// <summary>Режим вибору: тап по картинці ставить її в слот і повертає на планету.</summary>
        public bool IsPick => PlanetId != null;
    }

    /// <summary>
    /// Колекція (майстер-док §12): сітка всіх зібраних картинок із рамками рідкості, фільтри за
    /// темою й рідкістю. Окремий екран, а не шухляда: сюди ведуть і планета (вибір у слот), і
    /// налаштування (§15), і друге джерело правди про сітку розійшлося б із першим.
    ///
    /// Сітка віртуалізована, як рядки рейтингу: карток у пулі стільки, скільки влазить у в'юпорт
    /// із запасом, перепризначення — з onValueChanged, поза проходом канваса. Усі пікселі —
    /// з одного атласу, тож сітка з сотні карток малюється кількома викликами.
    /// </summary>
    public sealed class CollectionScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;

        [Header("Фільтри")]
        [SerializeField] private Button[] themeButtons = System.Array.Empty<Button>();
        [SerializeField] private GradientImage[] themeFills = System.Array.Empty<GradientImage>();
        [SerializeField] private TMP_Text[] themeLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] private Button[] rarityButtons = System.Array.Empty<Button>();
        [SerializeField] private Image[] rarityDots = System.Array.Empty<Image>();
        [SerializeField] private Image[] rarityRings = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text rarityAllLabel;

        [Header("Сітка")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private CollectionCard[] cards = System.Array.Empty<CollectionCard>();
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private TMP_Text hintLabel;

        private readonly List<string> _ids = new List<string>(128);
        private readonly List<PixelPicture?> _atlasPictures = new List<PixelPicture?>(128);
        private readonly SlotAtlas _atlas = new SlotAtlas();
        private readonly List<string> _themes = new List<string>(8);

        private CollectionArgs _args = new CollectionArgs();
        private int _theme;      // 0 — усі
        private int _rarity;     // 0 — усі, далі (int)Rarity + 1
        private int _firstBound = int.MinValue;

        /// <summary>Назад: на планету або туди, звідки відкрили.</summary>
        public System.Action? BackRequested;

        /// <summary>Гравець обрав картинку для слота (лише в режимі вибору): аргументи слота й id картинки.</summary>
        public System.Action<CollectionArgs, string>? PicturePicked;

        /// <summary>Картинки після фільтрів — у порядку бібліотеки (тестам і знімкам).</summary>
        public IReadOnlyList<string> VisibleIds => _ids;

        /// <summary>Картки пулу (тестам).</summary>
        public IReadOnlyList<CollectionCard> Cards => cards;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            for (var i = 0; i < themeButtons.Length; i++)
            {
                var index = i;
                themeButtons[i]?.onClick.AddListener(() => SetTheme(index));
            }
            for (var i = 0; i < rarityButtons.Length; i++)
            {
                var index = i;
                rarityButtons[i]?.onClick.AddListener(() => SetRarity(index));
            }
            for (var i = 0; i < cards.Length; i++)
                cards[i]?.Bind(OnCardTapped);
            if (scroll != null)
                scroll.onValueChanged.AddListener(_ => Recycle());
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _args = args as CollectionArgs ?? new CollectionArgs();
            _theme = 0;
            _rarity = 0;
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
            Apply();
        }

        /// <summary>Фільтр за темою: 0 — усі, далі індекс у списку тем бібліотеки.</summary>
        public void SetTheme(int index)
        {
            _theme = Mathf.Clamp(index, 0, _themes.Count);
            Rebuild();
        }

        /// <summary>Фільтр за рідкістю: 0 — усі, далі (int)Rarity + 1.</summary>
        public void SetRarity(int index)
        {
            _rarity = Mathf.Clamp(index, 0, Rarities.Count);
            Rebuild();
        }

        public void Apply()
        {
            if (design == null)
                return;

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary, FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "КОЛЕКЦІЯ";
            ApplyFont(subtitle, design.FontSizeCaption, design.TextMuted, FontStyles.Normal, 0f);
            ApplyFont(emptyLabel, design.FontSizeCaption, design.TextMuted, FontStyles.Bold, 0f);
            ApplyFont(hintLabel, design.FontSizeCaption, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(rarityAllLabel, design.FontSizeCaption, design.TextPrimary, FontStyles.Bold, 0f);
            if (rarityAllLabel != null) rarityAllLabel.text = "Усі";
            for (var i = 0; i < themeLabels.Length; i++)
                ApplyFont(themeLabels[i], design.FontSizeCaption, design.TextPrimary, FontStyles.Bold, 0f);
            if (hintLabel != null)
                hintLabel.gameObject.SetActive(false);

            var library = State?.Library ?? PictureLibrary.Fallback;
            _themes.Clear();
            for (var i = 0; i < library.Themes.Count; i++)
                _themes.Add(library.Themes[i]);
            if (_theme > _themes.Count)
                _theme = 0;

            ApplyThemeChips();
            ApplyRarityChips();
            Rebuild();
        }

        private void ApplyThemeChips()
        {
            for (var i = 0; i < themeButtons.Length; i++)
            {
                var used = i <= _themes.Count;
                Toggle(themeButtons[i], used);
                if (!used)
                    continue;
                if (i < themeLabels.Length && themeLabels[i] != null)
                    themeLabels[i].text = i == 0 ? "Усі теми" : ThemeNames.Of(_themes[i - 1]);
                var on = i == _theme;
                if (i < themeFills.Length && themeFills[i] != null)
                    themeFills[i].SetGradient(on ? design.ShopTabActiveFrom : design.GlassFill, on ? design.ShopTabActiveTo : design.GlassFill);
                if (i < themeLabels.Length && themeLabels[i] != null)
                    themeLabels[i].color = on ? design.TextPrimary : design.TextMuted;
            }
        }

        private void ApplyRarityChips()
        {
            // Кнопка 0 — «Усі» (напис), 1…6 — крапки кольору рідкості; обрана — з кільцем.
            for (var i = 0; i < rarityButtons.Length; i++)
            {
                var used = i <= Rarities.Count;
                Toggle(rarityButtons[i], used);
                if (!used)
                    continue;
                var on = i == _rarity;
                if (i < rarityDots.Length && rarityDots[i] != null)
                {
                    rarityDots[i].color = i == 0
                        ? (on ? design.ShopTabActiveFrom : design.GlassFill)
                        : design.RarityColor((Rarity)(i - 1));
                }
                if (i < rarityRings.Length && rarityRings[i] != null)
                    rarityRings[i].enabled = on;
            }
            if (rarityAllLabel != null)
                rarityAllLabel.color = _rarity == 0 ? design.TextPrimary : design.TextMuted;
        }

        /// <summary>Перебирає колекцію під фільтри, будує атлас і розкладає сітку.</summary>
        private void Rebuild()
        {
            if (design == null)
                return;

            var library = State?.Library ?? PictureLibrary.Fallback;
            _ids.Clear();
            _atlasPictures.Clear();
            for (var i = 0; i < library.Count; i++)
            {
                var picture = library[i];
                var copies = State != null ? State.Collection.CountOf(picture.Id) : 1;
                if (copies <= 0)
                    continue;
                if (_theme > 0 && _theme - 1 < _themes.Count &&
                    !string.Equals(picture.ThemeId, _themes[_theme - 1], System.StringComparison.Ordinal))
                    continue;
                if (_rarity > 0 && (int)picture.Rarity != _rarity - 1)
                    continue;
                _ids.Add(picture.Id);
                _atlasPictures.Add(picture);
            }
            _atlas.Build(_atlasPictures);

            ApplyThemeChips();
            ApplyRarityChips();

            if (subtitle != null)
            {
                if (_args.IsPick)
                {
                    var planet = (State?.Layout ?? GalaxyLayout.Default).Find(_args.PlanetId!);
                    subtitle.text = $"Обери картинку для слота · {(planet != null ? planet.Name : "планета")}";
                }
                else
                {
                    var distinct = State != null ? State.Collection.Distinct : library.Count;
                    var total = State != null ? State.Collection.Total : library.Count;
                    subtitle.text = $"Зібрано {distinct} різних · {total} усього";
                }
            }

            Toggle(emptyLabel, _ids.Count == 0);
            if (emptyLabel != null)
                emptyLabel.text = State != null && State.Collection.Distinct == 0
                    ? "Домалюй картинку в Нескінченному — і вона з'явиться тут"
                    : "Під цей фільтр нічого немає";

            var columns = Mathf.Max(1, design.CollectionColumns);
            var rows = Mathf.CeilToInt(_ids.Count / (float)columns);
            if (content != null)
                content.sizeDelta = new Vector2(0f, rows * RowStep() + design.CollectionGap * CardScale());

            _firstBound = int.MinValue;
            Recycle();
        }

        /// <summary>
        /// Масштаб картки: на вузькому канвасі (телефон 20:9 має ~966 одиниць ширини, не 1080) три
        /// картки токенної ширини не влазять, тож усі картки рівномірно меншають через localScale —
        /// внутрішня розкладка картки лишається тією самою.
        /// </summary>
        private float CardScale()
        {
            if (design == null || content == null)
                return 1f;
            var columns = Mathf.Max(1, design.CollectionColumns);
            var width = content.rect.width;
            if (width <= 1f)
                return 1f;
            var gridWidth = columns * design.CollectionCardWidth + (columns - 1) * design.CollectionGap;
            return Mathf.Min(1f, width / gridWidth);
        }

        private float RowStep() => (design.CollectionCardHeight + design.CollectionGap) * CardScale();

        /// <summary>Перепризначає картки під положення скролу — з onValueChanged, поза проходом канваса.</summary>
        private void Recycle()
        {
            if (design == null || content == null || cards.Length == 0)
                return;

            var columns = Mathf.Max(1, design.CollectionColumns);
            var scale = CardScale();
            var step = RowStep();
            var poolRows = cards.Length / columns;
            var totalRows = Mathf.CeilToInt(_ids.Count / (float)columns);
            var first = Mathf.FloorToInt(content.anchoredPosition.y / step);
            first = Mathf.Clamp(first, 0, Mathf.Max(0, totalRows - poolRows));

            if (first == _firstBound)
                return;
            _firstBound = first;

            var library = State?.Library ?? PictureLibrary.Fallback;
            var width = content.rect.width;
            var cardWidth = design.CollectionCardWidth * scale;
            var gap = design.CollectionGap * scale;
            // Сітка центрується в ширині вмісту: на планшеті картки не прилипають до лівого краю.
            var gridWidth = columns * cardWidth + (columns - 1) * gap;
            var left = Mathf.Max(0f, (width - gridWidth) * 0.5f);

            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null)
                    continue;
                var row = first + i / columns;
                var col = i % columns;
                var index = row * columns + col;
                if (index >= _ids.Count)
                {
                    card.Release();
                    continue;
                }

                var picture = library.Find(_ids[index]);
                if (picture == null)
                {
                    card.Release();
                    continue;
                }

                card.Rect.anchoredPosition = new Vector2(left + col * (cardWidth + gap), -(gap + row * step));
                card.Rect.localScale = new Vector3(scale, scale, 1f);
                var copies = State != null ? State.Collection.CountOf(picture.Id) : 1;
                var free = State != null ? State.FreeCopies(picture.Id) : 1;
                card.Show(index, picture, _atlas.Texture, _atlas.UvOf(index), design.RarityColor(picture.Rarity),
                    copies, free, _args.IsPick, design.CollectionUsedAlpha);
            }
        }

        private void OnCardTapped(int index)
        {
            if (index < 0 || index >= _ids.Count)
                return;
            var id = _ids[index];
            if (!_args.IsPick)
                return;

            if (State != null && State.FreeCopies(id) <= 0)
            {
                // Пояснюємо, а не мовчимо: усі копії цієї картинки вже стоять у слотах.
                if (hintLabel != null)
                {
                    hintLabel.text = "Усі копії цієї картинки вже стоять у слотах — домалюй її ще раз";
                    hintLabel.gameObject.SetActive(true);
                }
                return;
            }

            PicturePicked?.Invoke(_args, id);
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

        private void OnDestroy() => _atlas.Release();
    }
}
