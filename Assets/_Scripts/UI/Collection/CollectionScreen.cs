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
    /// Аргументи колекції: подивитись (вкладка або підекран з налаштувань), обрати картинку в слот планети або
    /// на вітрину профілю (§14). Слот описується так само, як у збереженні: галактика, планета, номер.
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

        private CollectionArgs(bool showcase, bool tab, bool avatar = false)
        {
            Showcase = showcase;
            IsTab = tab;
            Avatar = avatar;
        }

        /// <summary>Вибір вітринної картинки профілю: будь-яка зібрана, копії не рахуються.</summary>
        public static CollectionArgs ForShowcase() => new CollectionArgs(showcase: true, tab: false);

        /// <summary>Вибір аватара-картинки (§14, 2026-10-24): будь-яка зібрана, копії не рахуються — як вітрина.</summary>
        public static CollectionArgs ForAvatar() => new CollectionArgs(showcase: true, tab: false, avatar: true);

        /// <summary>Вкладка нижньої навігації: навігація видна, «‹» немає (U1Glaze S06_01).</summary>
        public static CollectionArgs Tab() => new CollectionArgs(showcase: false, tab: true);

        public int Galaxy { get; }
        public string? PlanetId { get; }
        public int Slot { get; }
        public bool Showcase { get; }

        /// <summary>Вибір аватара, а не вітрини: той самий режим «копії не рахуються» (<see cref="Showcase"/>), інша дія.</summary>
        public bool Avatar { get; }
        public bool IsTab { get; }

        /// <summary>Режим вибору: тап обирає картинку, коралова кнопка ставить її в слот (чи на вітрину).</summary>
        public bool IsPick => PlanetId != null || Showcase;
    }

    /// <summary>
    /// Колекція (майстер-док §12, U1Glaze S06): сітка всіх зібраних картинок у рамках рідкості, фільтри за темою й
    /// рідкістю чипами, порожній фільтр карткою з «Грати». Окремий екран, а не шухляда: сюди ведуть вкладка,
    /// планета (вибір у слот), профіль (вітрина) і налаштування (§15).
    ///
    /// Режим вибору — два кроки (рішення автора): тап обирає картинку (бірюзове кільце), коралова «Поставити
    /// «Назва»» підтверджує. Картинка, усі копії якої вже в слотах, притлумлена, і тап пояснює чому.
    ///
    /// Сітка віртуалізована: карток у пулі стільки, скільки влазить у в'юпорт із запасом, перепризначення — з
    /// onValueChanged, поза проходом канваса. Усі пікселі — з одного атласу, зібраного РАЗ на вхід; фільтр лише
    /// вибирає клітинки (інакше кожен тап по чипу перемальовував би колекцію й створював нову текстуру).
    /// </summary>
    public sealed class CollectionScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка вкладки / підекрана")]
        [SerializeField] private GameObject browseHeader;
        [SerializeField] private Button backButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CurrencyWidget currency;
        [SerializeField] private TMP_Text subtitle;

        [Header("Шапка вибору")]
        [SerializeField] private GameObject pickHeader;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text pickTitle;
        [SerializeField] private GameObject pickInfo;
        [SerializeField] private Image pickInfoIcon;
        [SerializeField] private TMP_Text pickInfoText;
        [SerializeField] private Sprite planetIcon;
        [SerializeField] private Sprite showcaseIcon;

        [Header("Фільтри")]
        [SerializeField] private ScrollRect raritiesScroll;
        [SerializeField] private ChipView[] rarityChips = System.Array.Empty<ChipView>();

        [Header("Сітка")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private CollectionCard[] cards = System.Array.Empty<CollectionCard>();

        [Header("Порожньо")]
        [SerializeField] private GameObject emptyCard;
        [SerializeField] private TMP_Text emptyTitle;
        [SerializeField] private TMP_Text emptyBody;
        [SerializeField] private CandyButton emptyPlay;

        [Header("Низ")]
        [SerializeField] private GameObject footer;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private CandyButton placeButton;
        [SerializeField] private Sprite placeSprite;
        [SerializeField] private Sprite offSprite;
        [SerializeField] private NavBar navBar;

        // Усі зібрані картинки — у порядку бібліотеки; з них атлас.
        private readonly List<PixelPicture?> _all = new List<PixelPicture?>(128);
        // Після фільтрів: id, картинка й клітинка атласу — паралельні списки.
        private readonly List<string> _ids = new List<string>(128);
        private readonly List<PixelPicture> _pictures = new List<PixelPicture>(128);
        private readonly List<int> _cells = new List<int>(128);
        private readonly Dictionary<string, int> _placed = new Dictionary<string, int>(System.StringComparer.Ordinal);
        private readonly SlotAtlas _atlas = new SlotAtlas();

        private CollectionArgs _args = new CollectionArgs();
        private int _rarity;     // 0 — усі, далі (int)Rarity + 1
        private string? _selected;
        private int _firstBound = int.MinValue;
        private float _boundWidth = -1f;
        private int[] _boundRow = System.Array.Empty<int>();

        /// <summary>Назад / закрити вибір: на планету, профіль чи туди, звідки відкрили.</summary>
        public System.Action? BackRequested;

        /// <summary>Гравець підтвердив картинку (лише в режимі вибору): аргументи слота й id картинки.</summary>
        public System.Action<CollectionArgs, string>? PicturePicked;

        /// <summary>Вкладка нижньої навігації (лише коли колекція — вкладка).</summary>
        public System.Action<string>? TabRequested;

        /// <summary>«Грати» з порожньої колекції чи порожнього фільтра.</summary>
        public System.Action? PlayRequested;

        /// <summary>Картинки після фільтрів — у порядку бібліотеки (тестам і знімкам).</summary>
        public IReadOnlyList<string> VisibleIds => _ids;

        /// <summary>Картки пулу (тестам).</summary>
        public IReadOnlyList<CollectionCard> Cards => cards;

        /// <summary>Обрана картинка в режимі вибору (тестам); null — ще не обрано.</summary>
        public string? SelectedId => _selected;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        /// <summary>Тести: текстура атласу — щоб упевнитись, що фільтр її не перебудовує.</summary>
        public Texture? PreviewAtlas => _atlas.Texture;

        /// <summary>Тестам: тап по картці з індексом у поточному зрізі, як пальцем.</summary>
        public void PreviewTap(int index) => OnCardTapped(index);

        /// <summary>Тестам і знімкам: коралова «Поставити».</summary>
        public void PreviewPlace() => OnPlaceClicked();

        /// <summary>Тестам: чи видна картка порожнього фільтра, навігація, «‹», низ із кнопкою.</summary>
        public bool PreviewEmptyShown => emptyCard != null && emptyCard.activeSelf;
        public bool PreviewNavBarShown => navBar != null && navBar.gameObject.activeSelf;
        public bool PreviewBackShown => backButton != null && backButton.gameObject.activeSelf;
        public bool PreviewFooterShown => footer != null && footer.activeSelf;
        public bool PreviewPlaceEnabled => placeButton != null && placeButton.Interactable;
        public string PreviewPlaceText => placeButton != null ? placeButton.Text : string.Empty;
        public string PreviewEmptyTitle => emptyTitle != null ? emptyTitle.text : string.Empty;
        public string PreviewHint => hintLabel != null && hintLabel.gameObject.activeSelf ? hintLabel.text : string.Empty;
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (closeButton != null)
                closeButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
            for (var i = 0; i < rarityChips.Length; i++)
            {
                var index = i;
                rarityChips[i]?.Button.onClick.AddListener(() => SetRarity(index));
            }
            for (var i = 0; i < cards.Length; i++)
                cards[i]?.Bind(OnCardTapped);
            if (scroll != null)
                scroll.onValueChanged.AddListener(_ => Recycle());
            if (placeButton != null)
                placeButton.Button.onClick.AddListener(OnPlaceClicked);
            if (emptyPlay != null)
                emptyPlay.Button.onClick.AddListener(() => PlayRequested?.Invoke());
            if (navBar != null)
                navBar.TabSelected += id => TabRequested?.Invoke(id);
        }

        public override void BindState(PlayerState state)
        {
            base.BindState(state);
            currency?.Bind(state.Wallet);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _args = args as CollectionArgs ?? new CollectionArgs();
            _rarity = 0;
            _selected = null;
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
            if (raritiesScroll != null)
                raritiesScroll.horizontalNormalizedPosition = 0f;
            Apply();
        }

        /// <summary>Фільтр за рідкістю: 0 — усі, далі (int)Rarity + 1.</summary>
        public void SetRarity(int index)
        {
            _rarity = Mathf.Clamp(index, 0, Rarities.Count);
            OnFilterChanged();
            Rebuild();
            Reveal(raritiesScroll, _rarity < rarityChips.Length ? rarityChips[_rarity] : null);
        }

        /// <summary>
        /// Новий фільтр — нова сітка з початку (інакше пружний скрол показав би кінець коротшого списку), і вибір
        /// скидається: обрана картинка могла зникнути з сітки, а кнопка далі пропонувала б поставити її (ревізія кроку 5).
        /// </summary>
        private void OnFilterChanged()
        {
            if (scroll != null)
                scroll.StopMovement();
            if (content != null)
                content.anchoredPosition = Vector2.zero;
            if (_selected != null)
            {
                _selected = null;
                ApplyPlaceButton();
            }
            if (hintLabel != null)
                hintLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Докручує ряд чипів так, щоб обраний був видно цілком (S06_03: «Космічна» за правим краєм ряду). Лише на
        /// дію гравця, не щокадру: розкладку ряду перебудовуємо один раз, далі — позиція вмісту.
        /// </summary>
        private static void Reveal(ScrollRect? row, ChipView? chip)
        {
            if (row == null || chip == null || row.content == null || row.viewport == null)
                return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.content);
            var chipRect = (RectTransform)chip.transform;
            var viewWidth = row.viewport.rect.width;
            var contentWidth = row.content.rect.width;
            if (contentWidth <= viewWidth)
                return;
            var left = chipRect.anchoredPosition.x - chipRect.rect.width * chipRect.pivot.x;
            var right = left + chipRect.rect.width;
            var x = -row.content.anchoredPosition.x;          // ліва межа видимого
            if (left < x)
                x = left;
            else if (right > x + viewWidth)
                x = right - viewWidth;
            x = Mathf.Clamp(x, 0f, contentWidth - viewWidth);
            row.StopMovement();
            row.content.anchoredPosition = new Vector2(-x, row.content.anchoredPosition.y);
        }

        public void Apply()
        {
            if (design == null)
                return;

            var pick = _args.IsPick;
            Toggle(browseHeader, !pick);
            Toggle(pickHeader, pick);
            Toggle(subtitle, !pick);
            Toggle(pickInfo, pick);
            Toggle(footer, pick);
            Toggle(navBar, !pick && _args.IsTab);
            Toggle(backButton, !pick && !_args.IsTab);
            if (title != null)
                title.text = "Колекція";
            if (pickTitle != null)
                pickTitle.text = "Обери картинку";
            navBar?.SetActive(NavBar.Collection);
            currency?.Apply();
            if (hintLabel != null)
                hintLabel.gameObject.SetActive(false);

            var library = State?.Library ?? PictureLibrary.Fallback;
            if (pick)
                ApplyPickInfo();

            // Усе зібране — в атлас, один раз на вхід.
            _all.Clear();
            for (var i = 0; i < library.Count; i++)
            {
                var picture = library[i];
                var copies = State != null ? State.Collection.CountOf(picture.Id) : 1;
                if (copies > 0)
                    _all.Add(picture);
            }
            _atlas.Build(_all);

            // Зайняті копії кожної картинки — одним проходом по слотах, а не сканом на кожну картку.
            if (State != null)
                GalaxyState.CountPlacedCopies(State.Galaxy, State.Layout, _placed);
            else
                _placed.Clear();

            ApplyRarityChips();
            ApplyPlaceButton();
            Rebuild();
        }

        private void ApplyPickInfo()
        {
            if (pickInfoText == null)
                return;
            if (_args.Avatar)
            {
                pickInfoText.text = "Аватар · його бачать інші гравці";
                if (pickInfoIcon != null && showcaseIcon != null) pickInfoIcon.sprite = showcaseIcon;
            }
            else if (_args.Showcase)
            {
                pickInfoText.text = "Вітрина · її бачать інші гравці";
                if (pickInfoIcon != null && showcaseIcon != null) pickInfoIcon.sprite = showcaseIcon;
            }
            else
            {
                var planet = (State?.Layout ?? GalaxyLayout.Default).For(_args.Galaxy).Find(_args.PlanetId!);
                pickInfoText.text = $"Комірка {_args.Slot + 1} · планета {(planet != null ? planet.Name : "—")}";
                if (pickInfoIcon != null && planetIcon != null) pickInfoIcon.sprite = planetIcon;
            }
        }

        private void ApplyRarityChips()
        {
            for (var i = 0; i < rarityChips.Length; i++)
            {
                var chip = rarityChips[i];
                if (chip == null)
                    continue;
                var used = i <= Rarities.Count;
                Toggle(chip, used);
                if (!used)
                    continue;
                chip.Text = i == 0 ? "Усі" : RarityNames.Title((Rarity)(i - 1));
                chip.SetMark(i == 0 ? (Color?)null : design.RarityColor((Rarity)(i - 1)));
                chip.SetOn(i == _rarity);
            }
        }

        /// <summary>Перебирає зібране під фільтри й розкладає сітку. Атласу не чіпає.</summary>
        private void Rebuild()
        {
            if (design == null)
                return;

            var library = State?.Library ?? PictureLibrary.Fallback;
            _ids.Clear();
            _pictures.Clear();
            _cells.Clear();
            for (var i = 0; i < _all.Count; i++)
            {
                var picture = _all[i];
                if (picture == null)
                    continue;
                if (_rarity > 0 && (int)picture.Rarity != _rarity - 1)
                    continue;
                _ids.Add(picture.Id);
                _pictures.Add(picture);
                _cells.Add(i);
            }

            ApplyRarityChips();

            if (subtitle != null)
            {
                var distinct = State != null ? State.Collection.Distinct : library.Count;
                var total = State != null ? State.Collection.Total : library.Count;
                subtitle.text = $"Зібрано {distinct} різних · {total} усього";
            }

            ApplyEmpty();
            ResizeContent();
            _firstBound = int.MinValue;
            Recycle();
        }

        /// <summary>
        /// Порожньо: колекція ще порожня, або порожній фільтр (S06_03). «Грати» — лише поза вибором: з вибору
        /// гравець іде назад до планети, а не в забіг.
        /// </summary>
        private void ApplyEmpty()
        {
            var empty = _ids.Count == 0;
            Toggle(emptyCard, empty);
            if (scroll != null)
                Toggle(scroll, !empty);
            if (!empty || emptyTitle == null || emptyBody == null)
                return;

            var nothingCollected = State != null && State.Collection.Distinct == 0;
            if (nothingCollected)
            {
                emptyTitle.text = "Колекція поки порожня";
                emptyBody.text = "Домалюй картинку в Нескінченному — і вона з'явиться тут.";
            }
            else if (_rarity > 0)
            {
                var rarity = (Rarity)(_rarity - 1);
                emptyTitle.text = $"Ще немає {RarityNames.GenitivePlural(rarity)} картинок";
                emptyBody.text = rarity >= Rarity.Legendary
                    ? "Вони трапляються найрідше. Грай далі — і одна з них твоя."
                    : "Грай далі — і вони з'являться тут.";
            }
            else
            {
                emptyTitle.text = "Тут поки нічого немає";
                emptyBody.text = "Спробуй іншу рідкість — або грай далі.";
            }
            Toggle(emptyPlay, !_args.IsPick);
        }

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

        private void ResizeContent()
        {
            if (content == null)
                return;
            var columns = Mathf.Max(1, design.CollectionColumns);
            var rows = Mathf.CeilToInt(_ids.Count / (float)columns);
            content.sizeDelta = new Vector2(0f, rows * RowStep() + design.CollectionGap * CardScale());
            _boundWidth = content.rect.width;
            var poolRows = Mathf.Max(1, cards.Length / columns);
            if (_boundRow.Length != poolRows)
                _boundRow = new int[poolRows];
            for (var i = 0; i < _boundRow.Length; i++)
                _boundRow[i] = -1;
        }

        /// <summary>
        /// Перепризначає картки під положення скролу — з onValueChanged, поза проходом канваса.
        ///
        /// Пул — кільце рядків: рядок сітки r завжди живе в пулі на місці r mod N. Коли вікно
        /// зсувається на один рядок, перепризначається лише рядок, що в'їхав, а не всі картки.
        /// </summary>
        private void Recycle()
        {
            if (design == null || content == null || cards.Length == 0)
                return;

            var columns = Mathf.Max(1, design.CollectionColumns);
            var poolRows = Mathf.Max(1, cards.Length / columns);
            var width = content.rect.width;
            // Ширина змінилась (поворот, Split View, перша розкладка після канваса) — перераховуємо все.
            if (!Mathf.Approximately(width, _boundWidth))
            {
                ResizeContent();
                _firstBound = int.MinValue;
            }

            var scale = CardScale();
            var step = RowStep();
            var totalRows = Mathf.CeilToInt(_ids.Count / (float)columns);
            var first = Mathf.FloorToInt(content.anchoredPosition.y / step);
            first = Mathf.Clamp(first, 0, Mathf.Max(0, totalRows - poolRows));

            if (first == _firstBound)
                return;
            _firstBound = first;

            var cardWidth = design.CollectionCardWidth * scale;
            var gap = design.CollectionGap * scale;
            // Сітка центрується в ширині вмісту: на планшеті картки не прилипають до лівого краю.
            var gridWidth = columns * cardWidth + (columns - 1) * gap;
            var left = Mathf.Max(0f, (width - gridWidth) * 0.5f);

            for (var r = 0; r < poolRows; r++)
            {
                // Єдиний рядок вікна [first, first + poolRows), що лягає в цю комірку пулу.
                var row = first + ((r - first % poolRows) + poolRows) % poolRows;
                if (_boundRow[r] == row)
                    continue;
                _boundRow[r] = row;

                for (var col = 0; col < columns; col++)
                {
                    var card = cards[r * columns + col];
                    if (card == null)
                        continue;
                    var index = row * columns + col;
                    if (index >= _ids.Count)
                    {
                        card.Release();
                        continue;
                    }
                    BindCard(card, index, left + col * (cardWidth + gap), -(gap + row * step), scale);
                }
            }
        }

        private void BindCard(CollectionCard card, int index, float x, float y, float scale)
        {
            var picture = _pictures[index];
            card.Rect.anchoredPosition = new Vector2(x, y);
            card.Rect.localScale = new Vector3(scale, scale, 1f);
            var copies = State != null ? State.Collection.CountOf(picture.Id) : 1;
            _placed.TryGetValue(picture.Id, out var placed);
            // На вітрину йде будь-яка зібрана картинка — копії в слотах тут не рахуються, нічого не тьмяніє.
            var free = _args.Showcase ? copies : copies - placed;
            card.Show(index, picture, _atlas.Texture, _atlas.UvOf(_cells[index]), copies, free,
                _args.IsPick && !_args.Showcase, string.Equals(picture.Id, _selected, System.StringComparison.Ordinal));
        }

        private void OnCardTapped(int index)
        {
            if (index < 0 || index >= _ids.Count || !_args.IsPick)
                return;
            var id = _ids[index];

            if (!_args.Showcase && State != null && State.FreeCopies(id) <= 0)
            {
                // Пояснюємо, а не мовчимо: усі копії вже стоять у слотах — і кажемо де, бо завершену галактику в
                // огляді треба ще знайти стрілками.
                if (hintLabel != null)
                {
                    hintLabel.text = GalaxyState.HasCopyOutside(State.Galaxy, _args.Galaxy, id, State.Layout)
                        ? "Усі копії цієї картинки стоять у слотах інших галактик — домалюй її ще раз"
                        : "Усі копії цієї картинки вже стоять у слотах — домалюй її ще раз";
                    hintLabel.gameObject.SetActive(true);
                }
                return;
            }

            _selected = id;
            if (hintLabel != null)
                hintLabel.gameObject.SetActive(false);
            for (var i = 0; i < cards.Length; i++)
                if (cards[i] != null && cards[i].Index >= 0)
                    cards[i].SetSelected(string.Equals(cards[i].PictureId, _selected, System.StringComparison.Ordinal));
            ApplyPlaceButton();
        }

        /// <summary>Коралова «Поставити «Назва»» — лише коли щось обрано; до того плоска «Обери картинку».</summary>
        private void ApplyPlaceButton()
        {
            if (placeButton == null)
                return;
            PixelPicture? picture = null;
            if (_selected != null)
            {
                var library = State?.Library ?? PictureLibrary.Fallback;
                picture = library.Find(_selected);
            }
            if (picture == null)
            {
                _selected = null;
                if (offSprite != null) placeButton.SetTone(CandyButton.Tone.Off, offSprite);
                placeButton.Interactable = false;
                placeButton.Text = "Обери картинку";
                return;
            }
            if (placeSprite != null) placeButton.SetTone(CandyButton.Tone.Coral, placeSprite);
            placeButton.Interactable = true;
            var name = picture.Name.Replace("­", string.Empty);
            placeButton.Text = _args.Avatar ? $"Аватаром «{name}»" : _args.Showcase ? $"На вітрину «{name}»" : $"Поставити «{name}»";
        }

        private void OnPlaceClicked()
        {
            if (!_args.IsPick || _selected == null)
                return;
            PicturePicked?.Invoke(_args, _selected);
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

        private void OnDestroy() => _atlas.Release();
    }
}
