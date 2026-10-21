using System.Collections;
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
    /// Аргументи екрана планети: яку планету якої галактики відкрили в огляді. Галактика — явно, не
    /// «поточна»: щойно заповнена остання планета робить поточною НАСТУПНУ галактику, а гравець
    /// ще стоїть на цій і має побачити, як вона ожила.
    /// </summary>
    public sealed class PlanetArgs : ScreenArgs
    {
        public PlanetArgs() { }

        public PlanetArgs(int galaxy, int planetIndex)
        {
            Galaxy = galaxy;
            PlanetIndex = planetIndex;
        }

        public int Galaxy { get; }
        public int PlanetIndex { get; }

        /// <summary>Чужа планета з рейтингів (U1Glaze S09_02/03): вітрина гравця, лише перегляд; null — своя.</summary>
        public PublicShowcase? Visitor { get; private set; }

        /// <summary>Планета гравця з його вітрини: галактика — та, що у вітрині.</summary>
        public static PlanetArgs ForVisitor(PublicShowcase showcase, int planetIndex) =>
            new PlanetArgs(showcase?.Galaxy ?? 0, planetIndex) { Visitor = showcase };
    }

    /// <summary>
    /// Планета-вітрина (майстер-док §12, U1Glaze S04/S05): до порогу комірок — «Сфера» (<see cref="PlanetGlobe"/>:
    /// куля, що крутиться, кільце міток навколо), більше — «Горизонт» (<see cref="PlanetHorizon"/>: велика планета
    /// внизу, комірки рядами, мітки рядком під назвою). Поріг — повзунок <c>sphereMaxSlots</c>.
    ///
    /// Дії ті самі: порожня комірка й «Поставити картинку» ведуть у колекцію (вибір для цієї комірки), зайнята —
    /// лист «Замінити / Повернути в колекцію / Скасувати». Усі комірки заповнені — планета ожила: гало 0 → 1,
    /// іскри, панель «… ожила!» з «До галактики» (лише на переході в ожилу, повернувшись із колекції).
    /// Що стоїть у слотах, вирішує збереження (<see cref="GalaxyState"/>); екран лише показує.
    /// </summary>
    public sealed class PlanetScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private CurrencyWidget currency;
        [SerializeField] private TMP_Text planetName;
        [SerializeField] private TMP_Text galaxyLabel;

        [Header("«Горизонт»: мітки рядком і лічильник угорі")]
        [SerializeField] private RectTransform horizonPipRow;
        [SerializeField] private Image[] horizonPips = System.Array.Empty<Image>();
        [SerializeField] private Image[] horizonPipShines = System.Array.Empty<Image>();
        [SerializeField] private Sprite pipEmpty;
        [SerializeField] private Sprite pipFilled;
        [SerializeField] private RectTransform horizonCounter;
        [SerializeField] private TMP_Text horizonCount;
        [SerializeField] private TMP_Text horizonCountWord;

        [Header("Планета")]
        [Tooltip("Стовпчик екрана: перебудовується перед заміром ділянки, щоб масштаб кулі брався з нової висоти.")]
        [SerializeField] private RectTransform column;
        [SerializeField] private RectTransform area;
        [Tooltip("Обгортка кулі для наближення з огляду (масштаб), усередині — куля з масштабом під ділянку.")]
        [SerializeField] private RectTransform stage;
        [SerializeField] private PlanetGlobe globe;
        [SerializeField] private PlanetHorizon horizon;

        [Header("Низ: лічильник «Сфери», дія, підказка")]
        [SerializeField] private RectTransform sphereCounter;
        [SerializeField] private TMP_Text sphereCount;
        [SerializeField] private TMP_Text sphereCountWord;
        [SerializeField] private RectTransform actionGap;
        [SerializeField] private RectTransform actionRow;
        [SerializeField] private CandyButton placeButton;
        [SerializeField] private RectTransform hintGap;
        [SerializeField] private TMP_Text hint;

        [Header("«Планета ожила»")]
        [SerializeField] private RectTransform alivePanel;
        [SerializeField] private CanvasGroup alivePanelGroup;
        [SerializeField] private TMP_Text aliveKicker;
        [SerializeField] private TMP_Text aliveTitle;
        [SerializeField] private TMP_Text aliveBody;
        [SerializeField] private CandyButton aliveButton;

        [Header("Низ чужої планети (S09_02/03)")]
        [SerializeField] private RectTransform visitorFooter;
        [SerializeField] private TMP_Text visitorTitle;
        [SerializeField] private CandyButton visitorButton;

        [Header("Лист зайнятої комірки")]
        [SerializeField] private RectTransform sheet;
        [SerializeField] private RarityFrameView sheetFrame;
        [SerializeField] private PictureView sheetPicture;
        [SerializeField] private TMP_Text sheetName;
        [SerializeField] private RarityBadgeView sheetBadge;
        [SerializeField] private CandyButton replaceButton;
        [SerializeField] private CandyButton removeButton;
        [SerializeField] private Button cancelButton;

        private readonly SlotAtlas _atlas = new SlotAtlas();
        private readonly List<PixelPicture?> _atlasPictures = new List<PixelPicture?>(16);

        private PlanetSurface? _surface;
        private PublicShowcase? _visitor;
        private int _galaxy;
        private int _planetIndex;
        private PlanetLook _look;
        private PlanetSlot? _sheetSlot;
        private bool _alive;
        private bool _alivePanelShown;
        private bool _completeBeforeCollection;
        private bool _returningFromCollection;
        // Аргументи, з якими зайшли, і з якими пішли в колекцію: Pop повертає ТІ САМІ (налаштування, колекція), новий
        // вхід із галактики — нові. Так «повернення з колекції» не переживає виходу іншим шляхом (шестерня → «Додому»),
        // а наближення не грає вдруге після налаштувань.
        private ScreenArgs? _enteredWith;
        private ScreenArgs? _collectionArgs;
        private bool _layoutDirty;
        private bool _wired;
        private Coroutine? _animation;

        /// <summary>Назад в огляд галактики — не в хаб.</summary>
        public System.Action? BackRequested;

        /// <summary>Відкрити колекцію для вибору картинки в комірку (аргументи кажуть, у яку).</summary>
        public System.Action<CollectionArgs>? CollectionRequested;

        /// <summary>«До галактики» з панелі «планета ожила»: огляд галактики на цій самій планеті.</summary>
        public System.Action? NextPlanetRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake() => WireOnce();

        /// <summary>Підписки — один раз; окремо від Awake, бо в Edit Mode (стенди, тести) Awake не біжить, а Apply — так.</summary>
        private void WireOnce()
        {
            if (_wired)
                return;
            _wired = true;
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
            if (placeButton != null)
                placeButton.Button.onClick.AddListener(OnPlaceClicked);
            if (aliveButton != null)
                aliveButton.Button.onClick.AddListener(() => NextPlanetRequested?.Invoke());
            if (visitorButton != null)
                visitorButton.Button.onClick.AddListener(() => BackRequested?.Invoke());
            if (replaceButton != null)
                replaceButton.Button.onClick.AddListener(OnReplace);
            if (removeButton != null)
                removeButton.Button.onClick.AddListener(OnRemove);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(CloseSheet);
            if (globe != null)
            {
                globe.SlotTapped += OnSlotTapped;
            }
            if (horizon != null)
                horizon.SlotTapped += OnSlotTapped;
        }

        private GalaxyLayout Layout => State?.Layout ?? GalaxyLayout.Default;
        private string PlanetId => _surface != null ? GalaxyState.PlanetId(_surface.Type) : string.Empty;
        private bool Editable => _visitor == null && (State == null || State.CanEditPlanet(_galaxy, PlanetId));

        /// <summary>
        /// Капсула в шапці показує справжній гаманець: без цього вона назавжди лишалась із числом-заглушкою
        /// з префаба (ревізія кроку 0; доти правильний баланс показував лише магазин, а він тепер у запасі).
        /// </summary>
        public override void BindState(PlayerState state)
        {
            base.BindState(state);
            currency?.Bind(state.Wallet);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            var layout = Layout;
            _visitor = (args as PlanetArgs)?.Visitor;
            if (args is PlanetArgs planetArgs)
            {
                _planetIndex = Mathf.Clamp(planetArgs.PlanetIndex, 0, layout.Planets.Count - 1);
                _galaxy = Mathf.Max(0, planetArgs.Galaxy);
            }
            else
                _galaxy = State?.CurrentGalaxy ?? 0;

            _surface = PlanetSurface.For(layout.Planets[_planetIndex]);
            // Чужа планета — зі слотів вітрини гравця (§16), своя — зі збереження.
            if (_visitor != null)
                GalaxyState.Apply(_surface, GalaxyProgress.DataOf(_visitor), _galaxy);
            else
            {
                GalaxyState.Apply(_surface, State?.Galaxy, _galaxy);
                if (State == null)
                    MockFill(_surface);
            }

            // Повернення з колекції після того, як поставлено останню картинку, — і є момент «планета ожила».
            // Вхід на вже ожилу планету не святкує вдруге.
            var fresh = !ReferenceEquals(args, _enteredWith);
            var returning = !fresh && _returningFromCollection && ReferenceEquals(args, _collectionArgs);
            var celebrate = returning && _surface.IsComplete && !_completeBeforeCollection;
            // Свято, що вже йшло, переживає похід у налаштування й назад (ті самі аргументи).
            var keepAlive = !fresh && !returning && _alive && _surface.IsComplete;
            _enteredWith = args;
            _returningFromCollection = false;
            _collectionArgs = null;
            _alive = false;
            _alivePanelShown = false;

            Apply();
            CloseSheet();
            StopAnimation();
            if (celebrate)
                ShowAlive(animated: !SkipEnterAnimations);
            else if (keepAlive)
                ShowAlive(animated: false);
            else if (fresh && _look == PlanetLook.Sphere && !SkipEnterAnimations)
                _animation = StartCoroutine(ApproachRoutine());
            else if (stage != null)
                stage.localScale = Vector3.one;
        }

        public void Apply()
        {
            if (design == null)
                return;
            WireOnce();

            if (_surface == null)
            {
                _surface = PlanetSurface.For(Layout.Planets[Mathf.Clamp(_planetIndex, 0, Layout.Planets.Count - 1)]);
                if (State == null)
                    MockFill(_surface);
            }

            _look = PlanetLooks.For(_surface.Slots.Count, design.SphereMaxSlots);
            var sphere = _look == PlanetLook.Sphere;

            if (planetName != null) planetName.text = _surface.Name;
            if (galaxyLabel != null) galaxyLabel.text = Layout.NameOf(_galaxy);
            currency?.Apply();

            Toggle(globe, sphere);
            Toggle(horizon, !sphere);
            Toggle(horizonPipRow, !sphere);
            Toggle(horizonCounter, !sphere);
            Toggle(sphereCounter, sphere);

            if (sphere && globe != null)
                globe.Bind(_surface);
            else if (!sphere && horizon != null)
                horizon.Bind(_surface);

            RefreshSlots();
            RefreshProgress();
            ApplyAlive();
            _layoutDirty = true;
            LayoutPlanet();
            if (!Application.isPlaying)
                Toggle(sheet, false);
        }

        /// <summary>Картинки слотів — в атлас, комірки — на картинки або порожні лунки, мітки — кольором картинок.</summary>
        private void RefreshSlots()
        {
            if (_surface == null || design == null)
                return;

            var library = State?.Library ?? PictureLibrary.Fallback;
            _atlasPictures.Clear();
            for (var i = 0; i < _surface.Slots.Count; i++)
            {
                var id = _surface.Slots[i].PictureId;
                _atlasPictures.Add(id is null ? null : library.Find(id));
            }
            _atlas.Build(_atlasPictures);

            for (var i = 0; i < _surface.Slots.Count; i++)
            {
                var slot = _surface.Slots[i];
                var picture = _atlasPictures[i];
                var cell = CellFor(slot);
                if (cell != null)
                {
                    if (!slot.IsFilled)
                        cell.ShowEmpty(readOnly: _visitor != null);
                    else if (picture == null || _atlas.Texture == null)
                        // У збереженні картинка є, у бібліотеці — ні: комірка зайнята й видима, звільнити її можна,
                        // а порожньою вона не прикидається.
                        cell.ShowUnknown(design.InkDim);
                    else
                        cell.ShowPicture(_atlas.Texture, _atlas.UvOf(i), design.RarityColor(picture.Rarity));
                }

                var color = picture != null ? DesignSystem.PaletteColor(picture.MainColor) : design.InkDim;
                if (_look == PlanetLook.Sphere)
                    globe?.SetPip(i, slot.IsFilled, color);
                else
                    SetHorizonPip(i, slot.IsFilled, color);
            }
            for (var i = _surface.Slots.Count; i < horizonPips.Length; i++)
                Toggle(horizonPips[i], false);
            if (_look == PlanetLook.Horizon)
                SizeHorizonPips(_surface.Slots.Count);
        }

        private SlotCell? CellFor(PlanetSlot slot) =>
            _look == PlanetLook.Sphere ? globe?.CellFor(slot) : horizon?.CellFor(slot);

        private void SetHorizonPip(int index, bool filled, Color color)
        {
            if (index >= horizonPips.Length || horizonPips[index] == null)
                return;
            Toggle(horizonPips[index], true);
            horizonPips[index].sprite = filled ? pipFilled : pipEmpty;
            horizonPips[index].color = filled ? color : Color.white;
            if (index < horizonPipShines.Length && horizonPipShines[index] != null)
                Toggle(horizonPipShines[index], filled);
        }

        /// <summary>
        /// Рядок міток «Горизонту» вміщається в найбільшу ширину: 12 — по 16 з проміжком 3 (S05_01), 20 — дрібніші
        /// (S05_02: 14 / 2). Розмір — на подію, не щокадру.
        /// </summary>
        private void SizeHorizonPips(int count)
        {
            if (design == null || horizonPipRow == null || count < 1)
                return;
            var size = design.HorizonPipSize;
            var gap = design.HorizonPipGap;
            var width = count * size + (count - 1) * gap;
            if (width > design.HorizonPipRowMax)
            {
                var f = design.HorizonPipRowMax / width;
                size *= f;
                gap *= f;
            }
            var group = horizonPipRow.GetComponent<HorizontalLayoutGroup>();
            if (group != null)
                group.spacing = DesignSystem.FromMockupPx(gap);
            for (var i = 0; i < count && i < horizonPips.Length; i++)
            {
                if (horizonPips[i] == null)
                    continue;
                var element = horizonPips[i].GetComponent<LayoutElement>();
                if (element != null)
                    element.preferredWidth = element.preferredHeight = element.minWidth = element.minHeight = DesignSystem.FromMockupPx(size);
            }
        }

        private void RefreshProgress()
        {
            if (_surface == null)
                return;
            var total = _surface.Slots.Count;
            var count = $"{_surface.FilledCount} / {total}";
            // «2 / 4 комірки заповнено», «5 / 8 комірок заповнено» (S04, S05): слово — за загальною кількістю.
            var word = $"{Plural.Of(total, "комірку", "комірки", "комірок")} заповнено";
            if (sphereCount != null) sphereCount.text = count;
            if (sphereCountWord != null) sphereCountWord.text = word;
            if (horizonCount != null) horizonCount.text = count;
            if (horizonCountWord != null) horizonCountWord.text = word;

            var editable = Editable;
            if (placeButton != null)
                placeButton.Interactable = editable && !_surface.IsComplete;

            // Чужа планета (S09_02/03): замість лічильника, дії й підказки — панель «Планета гравця … · N / M»,
            // «Тут можна лише дивитися» і «До галактики гравця».
            Toggle(visitorFooter, _visitor != null);
            if (_visitor != null)
            {
                if (visitorTitle != null)
                    visitorTitle.text = $"Планета гравця {_visitor.Nick} · {_surface.FilledCount} / {total}";
                Toggle(hint, false);
                Toggle(hintGap, false);
                return;
            }

            // Підказка: на «Сфері» — як крутити; там, де нічого не зміниш, — чому (мовчати не можна: тап без відповіді
            // читається як зламана кнопка). «Горизонт» без підказки (S05), окрім пояснення.
            string? text = null;
            if (!editable && State != null)
                text = _galaxy < State.CurrentGalaxy
                    ? "Ця галактика завершена — це вітрина"
                    : "Ця планета ще замкнена — заверши попередню";
            else if (_look == PlanetLook.Sphere)
                text = "Крути планету пальцем і тапни на порожню комірку";
            if (hint != null)
                hint.text = text ?? string.Empty;
            Toggle(hint, text != null && !_alivePanelShown);
            Toggle(hintGap, text != null && !_alivePanelShown);
        }

        /// <summary>Стан «ожила»: гало й іскри на ожилій планеті; панель — лише під час свята (і лише після гало).</summary>
        private void ApplyAlive()
        {
            var complete = _surface != null && _surface.IsComplete;
            SetAliveGlow(complete ? 1f : 0f);
            var panel = _alive && _alivePanelShown;
            var visitor = _visitor != null;
            Toggle(alivePanel, panel);
            Toggle(sphereCounter, _look == PlanetLook.Sphere && !panel && !visitor);
            Toggle(actionGap, !panel && !visitor);
            Toggle(actionRow, !panel && !visitor);
            if (panel)
            {
                Toggle(hint, false);
                Toggle(hintGap, false);
            }
        }

        private void SetAliveGlow(float amount)
        {
            if (_look == PlanetLook.Sphere)
                globe?.SetAlive(amount);
            else
                horizon?.SetAlive(amount);
        }

        private void ShowAlive(bool animated)
        {
            if (_surface == null || design == null)
                return;
            _alive = true;
            if (aliveKicker != null) aliveKicker.text = "УСІ КОМІРКИ ЗАПОВНЕНІ";
            if (aliveTitle != null) aliveTitle.text = $"{_surface.Name} ожила!";
            if (aliveBody != null)
            {
                var planets = Layout.Planets;
                aliveBody.text = _planetIndex + 1 < planets.Count
                    ? $"Відкрито наступну планету: {planets[_planetIndex + 1].Name}."
                    : "Це остання планета галактики.";
            }
            if (aliveButton != null) aliveButton.Text = "До галактики";
            if (stage != null)
                stage.localScale = Vector3.one;
            if (animated)
            {
                // SPEC: спершу гало й іскри, потім панель знизу — поки гало розгорається, низ лишається як був.
                _alivePanelShown = false;
                ApplyAlive();
                SetAliveGlow(0f);
                _animation = StartCoroutine(AliveRoutine());
                return;
            }
            ShowAlivePanel();
            if (alivePanelGroup != null)
                alivePanelGroup.alpha = 1f;
        }

        /// <summary>Панель «ожила» замінює низ: ділянка вища, куля піднімається (S04_05).</summary>
        private void ShowAlivePanel()
        {
            _alivePanelShown = true;
            ApplyAlive();
            LayoutPlanet();
        }

        /// <summary>
        /// «Планета ожила» (SPEC): гало й іскри 0 → 1 за planetAliveDuration, потім панель виїжджає знизу й
        /// проявляється (localPosition і CanvasGroup.alpha за dialogAppearDuration).
        /// </summary>
        private IEnumerator AliveRoutine()
        {
            if (design == null)
                yield break;
            var duration = design.PlanetAliveDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                SetAliveGlow(design.CurveEaseInOut.Evaluate(t / duration));
                yield return null;
            }
            SetAliveGlow(1f);

            ShowAlivePanel();
            if (alivePanel != null && alivePanelGroup != null)
            {
                var rest = alivePanel.localPosition;
                var offset = alivePanel.rect.height * 0.5f;
                var appear = design.DialogAppearDuration;
                for (var t = 0f; t < appear; t += Time.deltaTime)
                {
                    var k = design.CurveEaseInOut.Evaluate(t / appear);
                    alivePanel.localPosition = rest - Vector3.up * ((1f - k) * offset);
                    alivePanelGroup.alpha = k;
                    yield return null;
                }
                alivePanel.localPosition = rest;
                alivePanelGroup.alpha = 1f;
            }
            _animation = null;
        }

        private void Update()
        {
            if (_layoutDirty)
                LayoutPlanet();
        }

        private void OnRectTransformDimensionsChange() => _layoutDirty = true;

        /// <summary>
        /// Планета під ділянку, на зміну розкладки (не щокадру): «Сфера» — діаметр кулі (S04: частка ширини; на
        /// низьких — менше, щоб уміщались дуга й мітки) масштабом; «Горизонт» — розкладка рядів під розмір ділянки.
        /// Стовпчик спершу перебудовується: інакше висота ділянки була б ще стара (сховали лічильник, показали панель).
        /// </summary>
        private void LayoutPlanet()
        {
            _layoutDirty = false;
            if (design == null || area == null)
                return;
            if (column != null && column.gameObject.activeInHierarchy)
                LayoutRebuilder.ForceRebuildLayoutImmediate(column);
            if (_look == PlanetLook.Horizon)
            {
                horizon?.Layout();
                return;
            }
            if (globe == null)
                return;
            var height = area.rect.height;
            var screenWidth = ((RectTransform)transform).rect.width;
            if (height <= 0f || screenWidth <= 0f || globe.BaseDiameter <= 0f)
                return;
            var diameter = Mathf.Min(screenWidth * design.SphereWidthShare, height / design.SphereExtent);
            globe.SetFit(diameter / globe.BaseDiameter);
        }

        private void OnSlotTapped(PlanetSlot slot)
        {
            if (_surface == null || _alive || _visitor != null)
                return;
            // Завершена галактика — вітрина, яку більше не редагують; замкнена планета — теж ні. Пояснення — у підказці.
            if (!Editable)
            {
                RefreshProgress();
                return;
            }
            if (!slot.IsFilled)
            {
                OpenCollection(slot);
                return;
            }
            OpenSheet(slot);
        }

        private void OpenSheet(PlanetSlot slot)
        {
            _sheetSlot = slot;
            SetSelected(slot);
            var library = State?.Library ?? PictureLibrary.Fallback;
            var picture = slot.PictureId is null ? null : library.Find(slot.PictureId);
            if (sheetName != null)
                sheetName.text = picture != null ? picture.Name : "Невідома картинка";
            var rarity = picture != null ? picture.Rarity : Rarity.Common;
            sheetFrame?.SetRarity(rarity);
            sheetBadge?.SetRarity(rarity);
            Toggle(sheetBadge, picture != null);
            if (sheetPicture != null)
            {
                Toggle(sheetPicture, picture != null);
                if (picture != null)
                    sheetPicture.ShowCompleted(picture, string.Empty);
            }
            if (replaceButton != null) replaceButton.Text = "Замінити";
            if (removeButton != null) removeButton.Text = "Повернути в колекцію";
            Toggle(sheet, true);
        }

        private void SetSelected(PlanetSlot? slot)
        {
            if (_look == PlanetLook.Sphere)
                globe?.SetSelected(slot);
            else
                horizon?.SetSelected(slot);
        }

        /// <summary>«Поставити картинку»: колекція для першої порожньої комірки.</summary>
        private void OnPlaceClicked()
        {
            if (_surface == null || !Editable)
                return;
            foreach (var slot in _surface.Slots)
                if (!slot.IsFilled)
                {
                    OpenCollection(slot);
                    return;
                }
        }

        private void OpenCollection(PlanetSlot slot)
        {
            if (_surface == null)
                return;
            _completeBeforeCollection = _surface.IsComplete;
            _returningFromCollection = true;
            _collectionArgs = _enteredWith;
            CloseSheet();
            CollectionRequested?.Invoke(new CollectionArgs(_galaxy, PlanetId, slot.Index));
        }

        private void OnReplace()
        {
            if (_sheetSlot != null)
                OpenCollection(_sheetSlot);
        }

        private void OnRemove()
        {
            if (_sheetSlot == null || _surface == null)
                return;
            var slot = _sheetSlot;
            CloseSheet();
            if (State != null)
            {
                if (!State.ClearSlot(_galaxy, PlanetId, slot.Index, System.DateTime.UtcNow))
                    return;
                GalaxyState.Apply(_surface, State.Galaxy, _galaxy);
            }
            else
                slot.PictureId = null;

            RefreshSlots();
            RefreshProgress();
            // Планета, з якої забрали картинку, більше не ожила: гало гасне.
            ApplyAlive();
        }

        private void CloseSheet()
        {
            _sheetSlot = null;
            SetSelected(null);
            Toggle(sheet, false);
        }

        private void StopAnimation()
        {
            if (_animation == null)
                return;
            StopCoroutine(_animation);
            _animation = null;
            if (alivePanelGroup != null)
                alivePanelGroup.alpha = 1f;
        }

#if UNITY_EDITOR
        /// <summary>Тестам: чи видно низ чужої планети і що на ньому написано.</summary>
        public bool PreviewVisitorFooterShown => visitorFooter != null && visitorFooter.gameObject.activeSelf;
        public string PreviewVisitorTitle => visitorTitle != null ? visitorTitle.text : string.Empty;
        public bool PreviewActionShown => actionRow != null && actionRow.gameObject.activeSelf;

        /// <summary>Стенд знімків: лист зайнятої комірки, як після тапу.</summary>
        public void PreviewSheet(int slotIndex)
        {
            var slot = _surface?.Find(slotIndex);
            if (slot != null && slot.IsFilled)
                OnSlotTapped(slot);
        }

        /// <summary>Стенд і тести: тап по комірці (з усіма перевірками екрана).</summary>
        public void PreviewTap(int slotIndex)
        {
            var slot = _surface?.Find(slotIndex);
            if (slot != null)
                OnSlotTapped(slot);
        }

        /// <summary>Тести: «Повернути в колекцію» з відкритого листа.</summary>
        public void PreviewRemove() => OnRemove();

        /// <summary>Стенд знімків: «планета ожила» без колекції (кінцевий стан, без анімації).</summary>
        public void PreviewAlive() => ShowAlive(animated: false);

        /// <summary>Стенд знімків: поворот кулі, як на макеті (S04: seed 3, rot 110 / 195 / 40 / 95 / 150).</summary>
        public void PreviewRotation(float degrees)
        {
            if (globe != null)
                globe.Rotation = degrees;
        }

        public PlanetLook PreviewLook => _look;
        public PlanetGlobe? PreviewGlobe => globe;
        public PlanetHorizon? PreviewHorizon => horizon;
        public IReadOnlyList<SlotCell> PreviewCells =>
            _look == PlanetLook.Sphere
                ? (globe != null ? globe.Cells : System.Array.Empty<SlotCell>())
                : (horizon != null ? horizon.Cells : System.Array.Empty<SlotCell>());
        public bool PreviewSheetOpen => sheet != null && sheet.gameObject.activeSelf;
        public bool PreviewAliveShown => alivePanel != null && alivePanel.gameObject.activeSelf;
        public string PreviewAliveBody => aliveBody != null ? aliveBody.text : string.Empty;
        public string PreviewHint => hint != null && hint.gameObject.activeSelf ? hint.text : string.Empty;

        /// <summary>Тести: підказка вміщається без «…» (її пояснення не можна обрізати).</summary>
        public bool PreviewHintFits
        {
            get
            {
                if (hint == null || !hint.gameObject.activeInHierarchy)
                    return true;
                hint.ForceMeshUpdate();
                return !hint.isTextTruncated;
            }
        }

        public Vector3 PreviewStageScale => stage != null ? stage.localScale : Vector3.one;
        public string PreviewCounter =>
            (_look == PlanetLook.Sphere ? sphereCount : horizonCount)?.text + " " +
            (_look == PlanetLook.Sphere ? sphereCountWord : horizonCountWord)?.text;
        public CandyButton? PreviewPlaceButton => placeButton;
        public int PreviewHorizonPips
        {
            get
            {
                var n = 0;
                foreach (var pip in horizonPips)
                    if (pip != null && pip.gameObject.activeInHierarchy)
                        n++;
                return n;
            }
        }
#endif

        /// <summary>Наближення до планети з огляду галактики: масштаб обгортки, не нова сцена.</summary>
        private IEnumerator ApproachRoutine()
        {
            if (stage == null || design == null)
                yield break;
            var duration = design.PaintApproachDuration;
            var from = design.PaintApproachFromScale;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var scale = Mathf.LerpUnclamped(from, 1f, design.CurveBackOut.Evaluate(t / duration));
                stage.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            stage.localScale = Vector3.one;
            _animation = null;
        }

        /// <summary>Сцена-майстерня без стану гравця: кілька слотів із запасною картинкою, щоб бачити розкладку.</summary>
        private static void MockFill(PlanetSurface surface)
        {
            var fallback = PictureLibrary.Fallback;
            if (fallback.Count == 0)
                return;
            for (var i = 0; i < surface.Slots.Count && i < 3; i++)
                surface.Slots[i].PictureId = fallback[0].Id;
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private void OnDestroy() => _atlas.Release();
    }
}
