using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Огляд галактики: шапка, карусель планет, назва у фокусі й кнопка «Фарбувати».
    ///
    /// Один екран на два випадки. При <see cref="GalaxyArgs.ReadOnly"/> ховаються
    /// кнопка й пагінація — так само виглядатиме перегляд чужої галактики з
    /// Рейтингів, і другого екрана для цього не буде.
    ///
    /// Дані поки мокові: екран знає лише <see cref="GalaxyProgress"/>.
    /// </summary>
    public sealed class GalaxyScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text galaxyName;
        [SerializeField] private TMP_Text galaxyProgress;
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

        /// <summary>Куди веде «‹». Поки заглушка — зв'яже композиційний корінь.</summary>
        public System.Action? BackRequested;

        /// <summary>Гравець натиснув «Відкрити»: індекс галактики (цикл) і планети — екран планети-вітрини (§12).</summary>
        public System.Action<int, int>? OpenRequested;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);

            var galaxyArgs = args as GalaxyArgs ?? GalaxyArgs.Own;
            _readOnly = galaxyArgs.ReadOnly;
            _galaxy = galaxyArgs.Owner.IsSelf
                // Своя галактика — з реального збереження (поточний цикл); чужа лишається
                // моковою до Фази 7 (вітрина з хмари).
                ? (State != null ? GalaxyProgress.FromSave(State.Galaxy, State.Layout) : GalaxyProgress.CreateMock())
                : GalaxyProgress.CreateMockForOther(mockOtherPlanetsDone);

            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            _galaxy ??= State != null ? GalaxyProgress.FromSave(State.Galaxy, State.Layout) : GalaxyProgress.CreateMock();

            // Розміри з макета: 13 / 11 / 25 / 13 / 18 / 16 / 12 px.
            ApplyFont(galaxyName, design.FontSizeGalaxyTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingGalaxyTitle);
            ApplyFont(galaxyProgress, design.FontSizeSmall, design.TextFaint, FontStyles.Normal, 0f);
            ApplyFont(planetName, design.FontSizePlanetName, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(planetZones, design.FontSizeGalaxyTitle, design.TextMuted, FontStyles.Normal, 0f);
            ApplyFont(paintButtonLabel, design.FontSizePaintButton, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(nextGalaxyName, design.FontSizeNextGalaxy, design.TextDim, FontStyles.Bold, 0f);
            ApplyFont(nextGalaxyHint, design.FontSizeLabel, design.TextFaint, FontStyles.Normal, 0f);

            if (galaxyName != null)
            {
                galaxyName.text = _galaxy.Name;
                // Назва галактики довша за вільний проміжок шапки: не переносимо (перенесений рядок
                // лягав на «N / M планет»), а стискаємо шрифт до читабельного мінімуму.
                galaxyName.textWrappingMode = TextWrappingModes.NoWrap;
                galaxyName.overflowMode = TextOverflowModes.Ellipsis;
                galaxyName.enableAutoSizing = true;
                galaxyName.fontSizeMax = design.FontSizeGalaxyTitle;
                galaxyName.fontSizeMin = design.FontSizeGalaxyTitle * 0.7f;
            }
            if (galaxyProgress != null)
                galaxyProgress.text = $"{_galaxy.DoneCount} / {_galaxy.Planets.Count} планет";
            if (nextGalaxyName != null) nextGalaxyName.text = _galaxy.NextName;
            if (nextGalaxyHint != null)
                nextGalaxyHint.text = "Заверши цю галактику,\nщоб відкрити наступну";

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

            // Прев'ю наступної галактики: низ порожній, кнопки немає — фарбувати
            // там нічого.
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
                    PlanetState.Done => $"Усі {planet.TotalSlots} слотів заповнені",
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
            paintButton.onClick.AddListener(() => OpenRequested?.Invoke(galaxyIndex, captured));
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
        }
    }
}
