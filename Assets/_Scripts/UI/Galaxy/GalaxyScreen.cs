using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Огляд галактики: шапка, карусель планет, назва у фокусі й кнопка «Відкрити».
    ///
    /// Один екран на два випадки. При <see cref="GalaxyArgs.ReadOnly"/> ховаються
    /// кнопка й пагінація — так само виглядатиме перегляд чужої галактики з
    /// Рейтингів, і другого екрана для цього не буде.
    ///
    /// Галактик нескінченно, і завершені — вітрина, а не минуле: стрілки в шапці гортають
    /// цикли від першого до поточного. Без них усе, що гравець зібрав у Галактиці I, зникало б
    /// з гри тієї ж миті, як ожила її остання планета.
    ///
    /// Чужа галактика поки мокова: екран знає лише <see cref="GalaxyProgress"/>.
    /// </summary>
    public sealed class GalaxyScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text galaxyName;
        [SerializeField] private TMP_Text galaxyProgress;
        [SerializeField] private Button prevGalaxyButton;
        [SerializeField] private TMP_Text prevGalaxyLabel;
        [SerializeField] private Button nextGalaxyButton;
        [SerializeField] private TMP_Text nextGalaxyLabel;
        [SerializeField] private CurrencyWidget currency;

        [Header("Карусель")]
        [SerializeField] private PlanetCarousel carousel;
        [SerializeField] private TMP_Text nextGalaxyName;
        [SerializeField] private TMP_Text nextGalaxyHint;

        [Header("Низ")]
        [SerializeField] private RectTransform doneBadge;
        [SerializeField] private TMP_Text planetName;
        [SerializeField] private TMP_Text planetZones;
        [SerializeField] private Button paintButton;
        [SerializeField] private GradientImage paintButtonFill;
        [SerializeField] private TMP_Text paintButtonLabel;
        [SerializeField] private RectTransform pagination;
        [SerializeField] private Image[] dots = System.Array.Empty<Image>();

        [Header("Мокові дані")]
        [SerializeField] private int mockOtherPlanetsDone = 5;

        private GalaxyProgress? _galaxy;
        private bool _readOnly;
        private bool _own = true;
        private int _galaxyIndex;
        private bool _keepGalaxy;

        /// <summary>Куди веде «‹». Поки заглушка — зв'яже композиційний корінь.</summary>
        public System.Action? BackRequested;

        /// <summary>Гравець натиснув «Відкрити»: індекс галактики (цикл) і планети — екран планети-вітрини (§12).</summary>
        public System.Action<int, int>? OpenRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        /// <summary>Тести: кнопка «Відкрити».</summary>
        public Button? PreviewOpenButton => paintButton;

        /// <summary>Тести: чи видно пагінацію.</summary>
        public bool PreviewPaginationShown => pagination != null && pagination.gameObject.activeSelf;

        /// <summary>Тести: чи видно стрілки гортання галактик.</summary>
        public bool PreviewArrowsShown => prevGalaxyButton != null && prevGalaxyButton.gameObject.activeSelf;

        /// <summary>Тести: карусель.</summary>
        public PlanetCarousel? PreviewCarousel => carousel;

        /// <summary>Тести: галактика на екрані.</summary>
        public GalaxyProgress? PreviewGalaxy => _galaxy;

        /// <summary>Тести: гортання галактик, як стрілками в шапці.</summary>
        public void PreviewShiftGalaxy(int delta) => ShiftGalaxy(delta);
#endif

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);

            var galaxyArgs = args as GalaxyArgs ?? GalaxyArgs.Own;
            _readOnly = galaxyArgs.ReadOnly;
            _own = galaxyArgs.Owner.IsSelf;

            // Повернення з планети лишає гравця в тій галактиці, яку він гортав; новий вхід із хаба —
            // завжди в поточній.
            var current = State?.CurrentGalaxy ?? 0;
            _galaxyIndex = _keepGalaxy ? Mathf.Clamp(_galaxyIndex, 0, current) : current;
            _keepGalaxy = false;

            _galaxy = BuildGalaxy();
            Apply();
        }

        private GalaxyProgress BuildGalaxy()
        {
            // Своя галактика — з реального збереження; чужа лишається моковою до Фази 7 (вітрина з хмари).
            if (!_own)
                return GalaxyProgress.CreateMockForOther(mockOtherPlanetsDone);
            return State != null
                ? GalaxyProgress.FromSave(State.Galaxy, State.Layout, _galaxyIndex)
                : GalaxyProgress.CreateMock();
        }

        private void ShiftGalaxy(int delta)
        {
            if (_readOnly || State == null)
                return;
            var target = Mathf.Clamp(_galaxyIndex + delta, 0, State.CurrentGalaxy);
            if (target == _galaxyIndex)
                return;
            _galaxyIndex = target;
            _galaxy = BuildGalaxy();
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            _galaxy ??= BuildGalaxy();

            // Розміри з макета: 13 / 11 / 25 / 13 / 18 / 16 / 12 px.
            ApplyFont(galaxyName, design.FontSizeGalaxyTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingGalaxyTitle);
            ApplyFont(galaxyProgress, design.FontSizeSmall, design.TextFaint, FontStyles.Normal, 0f);
            ApplyFont(planetName, design.FontSizePlanetName, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(planetZones, design.FontSizeGalaxyTitle, design.TextMuted, FontStyles.Normal, 0f);
            ApplyFont(paintButtonLabel, design.FontSizePaintButton, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(nextGalaxyName, design.FontSizeNextGalaxy, design.TextDim, FontStyles.Bold, 0f);
            ApplyFont(nextGalaxyHint, design.FontSizeLabel, design.TextFaint, FontStyles.Normal, 0f);
            ApplyFont(prevGalaxyLabel, design.FontSizePlanetName, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(nextGalaxyLabel, design.FontSizePlanetName, design.TextPrimary, FontStyles.Bold, 0f);

            if (galaxyName != null)
            {
                galaxyName.text = _galaxy.Name;
                // Назва галактики довша за вільний проміжок шапки: не переносимо (перенесений рядок
                // лягав на «N / M планет»), а стискаємо шрифт до читабельного мінімуму. Між стрілками
                // циклів місця ще менше — там розрядка заголовка знімається.
                galaxyName.textWrappingMode = TextWrappingModes.NoWrap;
                galaxyName.overflowMode = TextOverflowModes.Ellipsis;
                galaxyName.enableAutoSizing = true;
                galaxyName.fontSizeMax = design.FontSizeGalaxyTitle;
                galaxyName.fontSizeMin = design.FontSizeGalaxyTitle * design.GalaxyTitleMinScale;
                galaxyName.characterSpacing = Pageable ? 0f : design.LetterSpacingGalaxyTitle;
            }
            if (galaxyProgress != null)
                galaxyProgress.text = $"{_galaxy.DoneCount} / {_galaxy.Planets.Count} планет";
            if (nextGalaxyName != null) nextGalaxyName.text = _galaxy.NextName;
            if (nextGalaxyHint != null)
                nextGalaxyHint.text = _galaxy.IsComplete
                    ? "Галактика завершена —\nце вітрина"
                    : "Заверши цю галактику,\nщоб відкрити наступну";

            ApplyGalaxyArrows();

            // Пагінація і кнопка — єдина різниця між своєю галактикою і чужою.
            if (pagination != null)
                pagination.gameObject.SetActive(!_readOnly);
            if (paintButton != null)
                paintButton.gameObject.SetActive(!_readOnly);

            currency?.Apply();

            if (carousel != null)
            {
                carousel.FocusChanged -= OnFocusChanged;
                carousel.FocusChanged += OnFocusChanged;
                var start = Mathf.Max(0, _galaxy.CurrentIndex);
                carousel.Bind(_galaxy, _readOnly, start);
                OnFocusChanged(carousel.Focus < 0 ? start : carousel.Focus);
            }
        }

        /// <summary>
        /// Стрілки циклів видно лише тоді, коли є що гортати: завершена хоч одна галактика і це своя.
        /// Назва галактики займає проміжок між стрілками, а без них — усю їхню ширину.
        /// </summary>
        /// <summary>Є що гортати: своя галактика, не перегляд, і завершений хоч один цикл.</summary>
        private bool Pageable => !_readOnly && _own && State != null && State.CurrentGalaxy > 0;

        private void ApplyGalaxyArrows()
        {
            var pageable = Pageable;
            Toggle(prevGalaxyButton, pageable);
            Toggle(nextGalaxyButton, pageable);
            if (prevGalaxyButton == null || nextGalaxyButton == null)
                return;

            if (pageable)
            {
                var current = State!.CurrentGalaxy;
                prevGalaxyButton.interactable = _galaxyIndex > 0;
                nextGalaxyButton.interactable = _galaxyIndex < current;
                if (prevGalaxyLabel != null) prevGalaxyLabel.color = _galaxyIndex > 0 ? design.TextPrimary : design.TextDim;
                if (nextGalaxyLabel != null) nextGalaxyLabel.color = _galaxyIndex < current ? design.TextPrimary : design.TextDim;
            }

            if (galaxyName == null)
                return;
            var left = (RectTransform)prevGalaxyButton.transform;
            var right = (RectTransform)nextGalaxyButton.transform;
            var from = pageable ? left.anchoredPosition.x + left.sizeDelta.x * 0.5f : left.anchoredPosition.x - left.sizeDelta.x * 0.5f;
            var to = pageable ? right.anchoredPosition.x - right.sizeDelta.x * 0.5f : right.anchoredPosition.x + right.sizeDelta.x * 0.5f;
            var rect = galaxyName.rectTransform;
            rect.anchoredPosition = new Vector2((from + to) * 0.5f, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(to - from, rect.sizeDelta.y);
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.FocusChanged -= OnFocusChanged;
        }

        private void OnFocusChanged(int index)
        {
            if (_galaxy == null || design == null)
                return;

            ApplyDots(index);

            // Прев'ю наступної галактики: низ порожній, кнопки немає — відкривати там нічого.
            if (index >= _galaxy.Planets.Count)
            {
                if (planetName != null) planetName.text = string.Empty;
                if (planetZones != null) planetZones.text = string.Empty;
                Toggle(doneBadge, false);
                Toggle(paintButton, false);
                return;
            }

            var planet = _galaxy.Planets[index];
            if (planetName != null) planetName.text = planet.Name;
            Toggle(doneBadge, planet.State == PlanetState.Done);

            if (planetZones != null)
                planetZones.text = planet.State switch
                {
                    PlanetState.Done => $"Усі {Plural.Count(planet.TotalSlots, "слот", "слоти", "слотів")} заповнені",
                    PlanetState.Current => $"{planet.FilledSlots} / {planet.TotalSlots} слотів",
                    _ => "Заверши попередню планету"
                };
            if (paintButtonLabel != null)
                paintButtonLabel.text = "Відкрити";

            if (_readOnly || paintButton == null)
                return;

            // На замкненій планеті кнопка лишається на місці, але тьмяна й неклікабельна:
            // прибирати її зовсім — значить смикати розкладку на кожному свайпі.
            var unlocked = planet.State != PlanetState.Locked;
            Toggle(paintButton, true);
            paintButton.interactable = unlocked;

            if (paintButtonFill != null)
            {
                // Кнопка — градієнт маджента → бурштин. Неактивна не «сіріє»
                // прозорістю, а стає рівною тьмяною плашкою: напівпрозорий градієнт
                // на зоряному фоні читався б як брудна пляма.
                if (unlocked)
                    paintButtonFill.SetGradient(design.AccentPrimary, design.AccentGold);
                else
                    paintButtonFill.SetGradient(design.ButtonDisabledFill, design.ButtonDisabledFill);
                paintButtonFill.color = Color.white;
            }

            if (paintButtonLabel != null)
                paintButtonLabel.color = unlocked ? design.TextPrimary : design.TextDim;

            paintButton.onClick.RemoveAllListeners();
            var captured = index;
            var galaxyIndex = _galaxy.Index;
            paintButton.onClick.AddListener(() =>
            {
                // Повернення з планети — у цю ж галактику, а не стрибком у поточну.
                _keepGalaxy = true;
                OpenRequested?.Invoke(galaxyIndex, captured);
            });
        }

        private void ApplyDots(int focus)
        {
            if (_galaxy == null || design == null)
                return;

            for (var i = 0; i < dots.Length; i++)
            {
                var dot = dots[i];
                if (dot == null)
                    continue;

                var used = i < _galaxy.Planets.Count;
                Toggle(dot, used);
                if (!used)
                    continue;

                var on = i == focus;
                dot.rectTransform.sizeDelta = new Vector2(
                    on ? design.PaginationDotActiveWidth : design.PaginationDotSize,
                    design.PaginationDotSize);
                dot.color = on ? design.TextPrimary : design.PaginationDotInactive;
            }
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

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (prevGalaxyButton != null)
                prevGalaxyButton.onClick.AddListener(() => ShiftGalaxy(-1));
            if (nextGalaxyButton != null)
                nextGalaxyButton.onClick.AddListener(() => ShiftGalaxy(+1));
        }
    }
}
