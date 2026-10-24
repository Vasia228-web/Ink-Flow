using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Галактика U1Glaze (S03Galaxy): своя — вкладка нижньої навігації (шапка «Галактика · нафта · шестерня»,
    /// без «‹»), під шапкою рядок «ГАЛАКТИКА I · ПЕРВІСНА / 2 з 9 планет», карусель планет із кільцем міток
    /// комірок, назва й підпис планети у фокусі, коралова «Відкрити», пагінація й навігація.
    ///
    /// Один екран на два випадки (<see cref="GalaxyArgs"/>): чужа галактика з Рейтингів (§16, S09) — «‹» замість
    /// назви, картка гостя під шапкою, без «Відкрити» й навігації (перегляд планети гостя — крок 7).
    ///
    /// Галактик нескінченно, і завершені — вітрина, а не минуле: стрілки обабіч рядка галактики гортають цикли
    /// від першого до поточного. Ожила остання планета — діалог «Галактику завершено» (S03_04), «Далі» —
    /// екран нової галактики (S03_05), «Летіти» — у карусель нової. Це похідне з кількості завершених галактик
    /// (пам'ять екрана, у файл нічого не пишеться): те, що було завершено до запуску, не святкується.
    /// </summary>
    public sealed class GalaxyScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CurrencyWidget currency;
        [SerializeField] private Button settingsButton;

        [Header("Гість (§16, S09)")]
        [SerializeField] private RectTransform visitorCard;
        [SerializeField] private Image visitorAvatar;
        [SerializeField] private TMP_Text visitorNick;
        [SerializeField] private TMP_Text visitorHint;
        [Tooltip("Аватари avatar_1…8 і інкогніто — ті самі спрайти, що на головному.")]
        [SerializeField] private Sprite[] avatarSprites = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite? incognitoAvatar;

        [Header("Рядок галактики")]
        [SerializeField] private TMP_Text galaxyName;
        [SerializeField] private TMP_Text galaxyProgress;
        [SerializeField] private TMP_Text galaxyReward;
        [SerializeField] private Button prevGalaxyButton;
        [SerializeField] private Button nextGalaxyButton;

        [Header("Карусель")]
        [SerializeField] private RectTransform column;
        [SerializeField] private RectTransform carouselArea;
        [SerializeField] private PlanetCarousel carousel;

        [Header("Низ")]
        [SerializeField] private TMP_Text planetName;
        [SerializeField] private TMP_Text planetZones;
        [SerializeField] private RectTransform openRow;
        [SerializeField] private CandyButton openButton;

        [Tooltip("Тіло кнопки: своя галактика — коралова «Відкрити», чужа — скляна «Подивитись планету» (S09_01).")]
        [SerializeField] private Sprite? openCoralSprite;
        [SerializeField] private Sprite? openGlassSprite;
        [SerializeField] private RectTransform pagination;
        [SerializeField] private Image[] dots = System.Array.Empty<Image>();
        [SerializeField] private Sprite? dotSprite;
        [SerializeField] private Sprite? pillSprite;
        [SerializeField] private NavBar navBar;

        [Header("Галактику завершено (S03_04)")]
        [SerializeField] private DialogView completeDialog;
        [SerializeField] private RectTransform completeArc;
        [SerializeField] private PlanetDisc[] arcPlanets = System.Array.Empty<PlanetDisc>();
        [SerializeField] private TMP_Text completeKicker;
        [SerializeField] private TMP_Text completeBody;
        [SerializeField] private CandyButton completeNext;

        [Header("Нова галактика (S03_05)")]
        [SerializeField] private RectTransform reveal;
        [SerializeField] private Button revealBack;
        [SerializeField] private CurrencyWidget revealCurrency;
        [SerializeField] private Button revealSettings;
        [SerializeField] private TMP_Text revealTitle;
        [SerializeField] private TMP_Text revealSubtitle;
        [SerializeField] private RectTransform revealArea;
        [SerializeField] private RectTransform revealHero;
        [SerializeField] private PlanetView revealPlanet;
        [SerializeField] private CandyButton revealFly;

        private enum Overlay { None, Completed, Reveal }

        private GalaxyProgress? _galaxy;
        private bool _readOnly;
        private bool _own = true;
        private PublicShowcase? _visitor;
        private int _galaxyIndex;
        private ScreenArgs? _enteredWith;
        private Overlay _overlay;
        private int _completedGalaxy;
        private bool _layoutDirty;
        private bool _wired;
        // Де стояла карусель: повернення через Pop (з планети, з налаштувань) ставить фокус туди ж, а не на поточну —
        // інакше в завершеній галактиці після п'ятої планети гравець опинявся на першій.
        private int _focusGalaxy = -1;
        private int _focusPlanet = -1;
        private bool _restoreFocus;

        /// <summary>
        /// Скільки галактик уже відсвятковано (−1 — ще не знаємо). Ставиться з поточного стану при першій прив'язці,
        /// тож завершене до запуску діалогу не викликає; діалог — коли поточна галактика стала більшою за це число.
        /// </summary>
        private int _celebrated = -1;

        /// <summary>«‹» гостя й нової галактики: роутер робить Pop (своя галактика — вкладка без «назад»).</summary>
        public System.Action? BackRequested;

        /// <summary>Гравець натиснув «Відкрити»: індекс галактики (цикл) і планети — екран планети-вітрини (§12).</summary>
        public System.Action<int, int>? OpenRequested;

        /// <summary>«Подивитись планету» в чужій галактиці (S09_01): вітрина гравця й номер планети.</summary>
        public System.Action<PublicShowcase, int>? VisitorPlanetRequested;

        /// <summary>Вкладка нижньої навігації: <see cref="NavBar.Play"/> / galaxy / collection / ranks.</summary>
        public System.Action<string>? TabRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        /// <summary>Тести: кнопка «Відкрити».</summary>
        public Button? PreviewOpenButton => openButton != null ? openButton.Button : null;
        public CandyButton? PreviewOpenCandy => openButton;

        /// <summary>Тести: чи видно пагінацію.</summary>
        public bool PreviewPaginationShown => pagination != null && pagination.gameObject.activeInHierarchy;

        /// <summary>Тести: чи видно стрілки гортання галактик (обабіч рядка галактики).</summary>
        public bool PreviewArrowsShown => prevGalaxyButton != null && prevGalaxyButton.gameObject.activeSelf;

        /// <summary>Тести: стрілка «›» до новішої галактики (і «‹» до старішої).</summary>
        public Button? PreviewNextGalaxyButton => nextGalaxyButton;
        public Button? PreviewPrevGalaxyButton => prevGalaxyButton;

        /// <summary>Тести: карусель.</summary>
        public PlanetCarousel? PreviewCarousel => carousel;

        /// <summary>Тести: галактика на екрані.</summary>
        public GalaxyProgress? PreviewGalaxy => _galaxy;

        public NavBar? PreviewNavBar => navBar != null && navBar.gameObject.activeInHierarchy ? navBar : null;
        public bool PreviewBackShown => backButton != null && backButton.gameObject.activeInHierarchy;
        public string PreviewTitle => title != null ? title.text : string.Empty;

        /// <summary>Тести: картка гостя видима.</summary>
        public bool PreviewVisitorShown => visitorCard != null && visitorCard.gameObject.activeSelf;
        public string PreviewVisitorNick => visitorNick != null ? visitorNick.text : string.Empty;
        public string PreviewGalaxyTitle => galaxyName != null ? galaxyName.text : string.Empty;
        public int PreviewFocus => carousel != null ? carousel.Focus : -1;
        public string PreviewVisitorHint => visitorHint != null ? visitorHint.text : string.Empty;
        public Sprite? PreviewVisitorAvatar => visitorAvatar != null ? visitorAvatar.sprite : null;
        public string PreviewPlanetName => planetName != null ? planetName.text : string.Empty;
        public string PreviewPlanetCaption => planetZones != null ? planetZones.text : string.Empty;

        /// <summary>Тести: гортання галактик, як стрілками.</summary>
        public void PreviewShiftGalaxy(int delta) => ShiftGalaxy(delta);

        /// <summary>Знімки й тести: планета в центрі (S03_02 — ожила, S03_03 — замкнена).</summary>
        public void PreviewSetFocus(int index)
        {
            if (carousel != null && _galaxy != null)
                carousel.Bind(_galaxy, _readOnly, index, PictureColor);
            Layout();
            OnFocusChanged(carousel != null ? carousel.Focus : index);
        }

        /// <summary>
        /// Знімки й тести: «щойно ожила остання планета». Стенд прив'язує стан на кожному вході, і пам'ять екрана
        /// вважає завершене вже відсвяткованим — тут вона відкочується на одну галактику, і вхід покаже діалог.
        /// </summary>
        public void PreviewGalaxyJustCompleted(ScreenArgs args)
        {
            if (State == null || State.CurrentGalaxy < 1)
                return;
            _celebrated = State.CurrentGalaxy - 1;
            PreviewEnter(args);
        }

        public bool PreviewCompletedShown => completeDialog != null && completeDialog.gameObject.activeSelf;
        public bool PreviewRevealShown => reveal != null && reveal.gameObject.activeSelf;
        public string PreviewRevealTitle => revealTitle != null ? revealTitle.text : string.Empty;
        public string PreviewRevealSubtitle => revealSubtitle != null ? revealSubtitle.text : string.Empty;
        public string PreviewCompletedBody => completeBody != null ? completeBody.text : string.Empty;
        public int PreviewArcPlanets
        {
            get
            {
                var n = 0;
                foreach (var planet in arcPlanets)
                    if (planet != null && planet.gameObject.activeSelf)
                        n++;
                return n;
            }
        }

        /// <summary>Тести: «Далі» в діалозі, «Летіти» й «‹» на екрані нової галактики — як пальцем.</summary>
        public void PreviewNext() => OnNextClicked();
        public void PreviewFly() => OnFlyClicked();
        public void PreviewRevealBack() => OnFlyClicked();
#endif

        private void Awake() => WireOnce();

        /// <summary>
        /// Підписки кнопок — один раз. Окремим методом, бо в Edit Mode (стенди, тести) Awake не біжить, а Apply —
        /// так: без цього «Відкрити» в тестах мовчала б.
        /// </summary>
        private void WireOnce()
        {
            if (_wired)
                return;
            _wired = true;
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
            WireSettingsButton(revealSettings);
            if (prevGalaxyButton != null)
                prevGalaxyButton.onClick.AddListener(() => ShiftGalaxy(-1));
            if (nextGalaxyButton != null)
                nextGalaxyButton.onClick.AddListener(() => ShiftGalaxy(+1));
            if (openButton != null)
                openButton.Button.onClick.AddListener(OnOpenClicked);
            if (navBar != null)
                navBar.TabSelected += id => TabRequested?.Invoke(id);
            if (completeNext != null)
                completeNext.Button.onClick.AddListener(OnNextClicked);
            if (revealFly != null)
                revealFly.Button.onClick.AddListener(OnFlyClicked);
            if (revealBack != null)
                revealBack.onClick.AddListener(OnFlyClicked);
        }

        /// <summary>
        /// Капсули показують справжній гаманець (ревізія кроку 0): і в шапці, і на екрані нової галактики. Пам'ять
        /// «що відсвятковано» — з поточного стану: завершене до запуску діалогу не викликає.
        /// </summary>
        public override void BindState(PlayerState state)
        {
            base.BindState(state);
            currency?.Bind(state.Wallet);
            revealCurrency?.Bind(state.Wallet);
            _celebrated = state.CurrentGalaxy;
            // Новий стан (дев-панель скинула збереження) не успадковує діалог, галактику й фокус старого гравця:
            // інакше «Галактику завершено» висіло б над порожньою грою.
            _overlay = Overlay.None;
            _completedGalaxy = 0;
            _enteredWith = null;
            _focusGalaxy = _focusPlanet = -1;
            _restoreFocus = false;
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);

            var galaxyArgs = args as GalaxyArgs ?? GalaxyArgs.Own;
            // Повернення (Pop із планети, налаштувань) приносить ТІ САМІ аргументи — гравець лишається в галактиці,
            // яку гортав; новий вхід (вкладка, Рейтинги) — нові аргументи й поточна галактика.
            var fresh = !ReferenceEquals(galaxyArgs, _enteredWith);
            _enteredWith = galaxyArgs;
            _restoreFocus = !fresh;
            _readOnly = galaxyArgs.ReadOnly;
            _own = galaxyArgs.Owner.IsSelf;
            _visitor = galaxyArgs.Visitor;

            var current = State?.CurrentGalaxy ?? 0;
            if (_own && State != null)
            {
                // Дев-панель може стерти слоти, і галактик стане менше — пам'ять за ними.
                if (_celebrated < 0 || _celebrated > current)
                    _celebrated = current;
                if (current > _celebrated && _overlay == Overlay.None)
                {
                    _overlay = Overlay.Completed;
                    _completedGalaxy = current - 1;
                }
                // Новий вхід, поки висіло «Нова галактика», — гравець пішов звідти іншим шляхом: просто поточна.
                if (fresh && _overlay == Overlay.Reveal)
                    _overlay = Overlay.None;
            }

            _galaxyIndex = _own && _overlay == Overlay.Completed ? _completedGalaxy
                : fresh ? current
                : Mathf.Clamp(_galaxyIndex, 0, current);

            _galaxy = BuildGalaxy();
            Apply();
        }

        private GalaxyProgress BuildGalaxy()
        {
            var layout = State?.Layout ?? GalaxyLayout.Default;
            // Своя галактика — з реального збереження; чужа — з вітрини (§16): без вітрини показати нічого,
            // тож гість без даних бачить порожню галактику, а не вигадану.
            if (!_own)
                return _visitor != null
                    ? GalaxyProgress.FromShowcase(_visitor, layout)
                    : GalaxyProgress.FromShowcase(PublicShowcase.Hidden(string.Empty, 0, 0, string.Empty), layout);
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

        /// <summary>Колір мітки заповненої комірки: головний колір картинки; невідома бібліотеці — нейтральний.</summary>
        private Color PictureColor(string id)
        {
            var picture = State?.Library.Find(id);
            return picture != null ? DesignSystem.PaletteColor(picture.MainColor) : design.InkDim;
        }

        public void Apply()
        {
            if (design == null)
                return;
            WireOnce();

            _galaxy ??= BuildGalaxy();

            // Шапка: своя — «Галактика» зліва (вкладка), гість — «‹» замість назви.
            Toggle(backButton, !_own);
            if (title != null) title.text = _own ? "Галактика" : string.Empty;
            ApplyVisitor();

            if (galaxyName != null) galaxyName.text = _galaxy.Name;
            if (galaxyProgress != null)
                galaxyProgress.text = $"{_galaxy.FilledCells} / {Plural.Count(_galaxy.TotalCells, "комірка", "комірки", "комірок")} · " +
                                      $"{_galaxy.DoneCount} / {Plural.Count(_galaxy.Planets.Count, "планета", "планети", "планет")}";
            // Нагорода видна заздалегідь (2026-10-23) — лише у своїй галактиці.
            Toggle(galaxyReward, _own && !_readOnly && State != null);
            if (galaxyReward != null && State != null)
                galaxyReward.text = RewardText(State.Layout.Reward);
            ApplyGalaxyArrows();

            // «Відкрити» й навігація — своя галактика; у гостя — скляна «Подивитись планету» (S09_01, крок 7).
            var visiting = _readOnly && _visitor != null;
            Toggle(openRow, !_readOnly || visiting);
            if (openButton != null)
            {
                if (visiting && openGlassSprite != null)
                    openButton.SetTone(CandyButton.Tone.Glass, openGlassSprite);
                else if (!_readOnly && openCoralSprite != null)
                    openButton.SetTone(CandyButton.Tone.Coral, openCoralSprite);
                openButton.Text = visiting ? "Подивитись планету" : "Відкрити";
            }
            Toggle(navBar, _own && !_readOnly);
            navBar?.SetActive(NavBar.Galaxy);

            currency?.Apply();
            revealCurrency?.Apply();

            ApplyOverlays();

            if (carousel != null)
            {
                carousel.FocusChanged -= OnFocusChanged;
                carousel.FocusChanged += OnFocusChanged;
                // Гостю показуємо останню ожилу планету — його досягнення, а не порожню наступну.
                var start = _visitor != null ? Mathf.Max(0, _galaxy.DoneCount - 1) : Mathf.Max(0, _galaxy.CurrentIndex);
                if (_restoreFocus && _focusGalaxy == _galaxy.Index && _focusPlanet >= 0 && _focusPlanet < _galaxy.Planets.Count)
                    start = _focusPlanet;
                carousel.Bind(_galaxy, _readOnly, start, PictureColor);
            }
            _restoreFocus = false;
            Layout();
            OnFocusChanged(carousel != null && carousel.Focus >= 0 ? carousel.Focus : 0);
        }

        // ── Розкладка: планета під висоту каруселі (S12 — на низьких екранах меншає лише вона) ──

        private void OnRectTransformDimensionsChange() => _layoutDirty = true;

        private void Update()
        {
            if (_layoutDirty)
                Layout();
        }

        /// <summary>
        /// Діаметр планети = min(токен galaxyPlanetSide, висота каруселі / (кільце міток + мітка)), як масштаб доріжки
        /// (localScale): шапка, кнопки й навігація сталі, а зайва висота лишається порожнім небом навколо планети.
        /// На подію зміни розміру, не щокадру.
        /// </summary>
        public void Layout()
        {
            _layoutDirty = false;
            if (design == null)
                return;
            if (column != null && column.gameObject.activeInHierarchy)
                LayoutRebuilder.ForceRebuildLayoutImmediate(column);
            var extent = 2f * (design.GalaxyPipRing + design.GalaxyPipSize * 0.5f) * design.PlanetSizeFinale;
            var maxFit = U1Px(design.GalaxyPlanetSide) / Mathf.Max(design.PlanetSize, 1f);
            if (carousel != null && carouselArea != null && carouselArea.rect.height > 1f)
                carousel.SetFit(Mathf.Min(maxFit, carouselArea.rect.height / extent));
            if (revealHero != null && revealArea != null)
            {
                if (reveal != null && reveal.gameObject.activeInHierarchy)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(reveal);
                var heroExtent = design.GalaxyRevealHaze * design.PlanetSize;
                var fit = revealArea.rect.height > 1f ? Mathf.Min(maxFit, revealArea.rect.height / heroExtent) : maxFit;
                revealHero.localScale = new Vector3(fit, fit, 1f);
            }
        }

        private static float U1Px(float mockupPx) => Mathf.Round(mockupPx * DesignSystem.MockupToReference);

        /// <summary>Є що гортати: своя галактика, не перегляд, і завершений хоч один цикл.</summary>
        private bool Pageable => !_readOnly && _own && State != null && State.CurrentGalaxy > 0;

        /// <summary>Стрілки циклів обабіч рядка галактики — лише коли є що гортати; межа — тьмяна й неактивна.</summary>
        private void ApplyGalaxyArrows()
        {
            var pageable = Pageable;
            Toggle(prevGalaxyButton, pageable);
            Toggle(nextGalaxyButton, pageable);
            if (!pageable || State == null)
                return;
            var current = State.CurrentGalaxy;
            SetArrow(prevGalaxyButton, _galaxyIndex > 0);
            SetArrow(nextGalaxyButton, _galaxyIndex < current);
        }

        private void SetArrow(Button? arrow, bool enabled)
        {
            if (arrow == null)
                return;
            arrow.interactable = enabled;
            var icon = arrow.transform.childCount > 0 ? arrow.transform.GetChild(0).GetComponent<Image>() : null;
            if (icon != null)
                icon.color = enabled ? design.InkText : design.InkDim;
        }

        /// <summary>Картка гостя (S09_01): аватар-спрайт, нік (або «Гравець-інкогніто») і «галактика гравця».</summary>
        private void ApplyVisitor()
        {
            var show = !_own;
            Toggle(visitorCard, show);
            if (!show)
                return;

            var hidden = _visitor == null || _visitor.Incognito;
            if (visitorAvatar != null)
                visitorAvatar.sprite = hidden && incognitoAvatar != null ? incognitoAvatar
                    : avatarSprites.Length > 0 ? avatarSprites[AvatarIndex(_visitor?.AvatarId ?? 0)] : visitorAvatar.sprite;
            if (visitorNick != null)
            {
                visitorNick.text = hidden ? "Гравець-інкогніто" : _visitor!.Nick;
                visitorNick.color = hidden ? design.InkMuted : design.InkText;
            }
            if (visitorHint != null)
                visitorHint.text = hidden ? "профіль приховано" : "галактика гравця";
        }

        private int AvatarIndex(int id) => id >= 0 && id < avatarSprites.Length ? id : 0;

        // ── Галактику завершено → нова галактика ──

        private void ApplyOverlays()
        {
            var completed = _own && _overlay == Overlay.Completed && State != null;
            var revealing = _own && _overlay == Overlay.Reveal && State != null;

            if (completeDialog != null)
            {
                if (completed && !completeDialog.IsShown)
                    completeDialog.Show();
                else if (!completed && completeDialog.IsShown)
                    completeDialog.Hide();
            }
            if (completed)
                ShowCompleted();

            // Нова галактика — окремий екран поверх: стовпчик під ним вимкнений (не лише прикритий) — і для
            // бюджету викликів, і щоб навігація не ловила тапів.
            Toggle(reveal, revealing);
            Toggle(column, !revealing);
            if (revealing)
                ShowReveal();
        }

        /// <summary>Підпис нагороди: «У кінці галактики: Епічна+ картинка і 300 нафти».</summary>
        public static string RewardText(GalaxyReward reward) =>
            $"У кінці галактики: {RarityName(reward.MinRarity)}+ картинка і {reward.Oil} нафти";

        private static string RarityName(Rarity rarity) => rarity switch
        {
            Rarity.Common => "Звичайна",
            Rarity.Uncommon => "Незвичайна",
            Rarity.Rare => "Рідкісна",
            Rarity.Epic => "Епічна",
            Rarity.Legendary => "Легендарна",
            _ => "Космічна"
        };

        /// <summary>S03_04: дуга з усіх планет галактики, її назва, «Галактику завершено!», «Усі N планет ожили», нагорода.</summary>
        private void ShowCompleted()
        {
            if (State == null)
                return;
            var layout = State.Layout.For(_completedGalaxy);
            var count = Mathf.Min(layout.Planets.Count, arcPlanets.Length);
            var reward = State.Layout.Reward;
            var prize = $"+{reward.Oil} нафти, а наступна картинка — {RarityName(reward.MinRarity)}+.";
            if (completeKicker != null) completeKicker.text = layout.NameOf(_completedGalaxy);
            if (completeBody != null)
                completeBody.text = layout.Planets.Count == 1
                    ? $"Планета ожила. {prize}"   // «Усі 1 планета ожили» — не по-людськи
                    : $"Усі {Plural.Count(layout.Planets.Count, "планета", "планети", "планет")} ожили. {prize}";

            var radius = U1Px(design.GalaxyArcRadius);
            var side = U1Px(design.GalaxyArcPlanet);
            var spread = design.GalaxyArcSpread;
            // Планети стоять на дузі над центром кола; найнижчі (краї дуги) — на дні контейнера.
            var lowest = Mathf.Sin((90f - spread * 0.5f) * Mathf.Deg2Rad);
            for (var i = 0; i < arcPlanets.Length; i++)
            {
                var planet = arcPlanets[i];
                if (planet == null)
                    continue;
                var used = i < count;
                Toggle(planet, used);
                if (!used)
                    continue;
                // Дуга зліва направо через верх: від 90° + розмах/2 до 90° − розмах/2.
                var angle = (90f + spread * 0.5f - (count > 1 ? spread * i / (count - 1) : spread * 0.5f)) * Mathf.Deg2Rad;
                var rect = (RectTransform)planet.transform;
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, (Mathf.Sin(angle) - lowest) * radius + side * 0.5f);
                rect.sizeDelta = new Vector2(side, side);
                var source = layout.Planets[i];
                planet.Show(source.Type, source.Name);
            }
        }

        /// <summary>S03_05: «ВІДКРИТО», «Галактика II · Туманна», «12 планет · до 12 комірок на планеті», перша планета.</summary>
        private void ShowReveal()
        {
            if (State == null)
                return;
            var next = State.CurrentGalaxy;
            var layout = State.Layout.For(next);
            if (revealTitle != null) revealTitle.text = layout.TitleOf(next);
            if (revealSubtitle != null)
                revealSubtitle.text = $"{Plural.Count(layout.Planets.Count, "планета", "планети", "планет")} · до {Plural.Count(layout.MaxSlots, "комірки", "комірок", "комірок")} на планеті";
            if (revealPlanet != null && layout.Planets.Count > 0)
            {
                // Герой — перша планета нової галактики, показана ожилою: обіцянка, а не порожній сірий макет.
                var first = layout.Planets[0];
                revealPlanet.gameObject.SetActive(true);
                revealPlanet.Bind(new PlanetProgress(first.Type, first.Name, first.Slots)
                {
                    FilledSlots = first.Slots, State = PlanetState.Done, HasMoons = first.HasMoons, HasRing = first.HasRing
                }, focused: true, readOnly: true);
            }
        }

        private void OnNextClicked()
        {
            if (_overlay != Overlay.Completed || State == null)
                return;
            _celebrated = State.CurrentGalaxy;
            _overlay = Overlay.Reveal;
            Apply();
        }

        /// <summary>«Летіти» або «‹» на екрані нової галактики — у карусель нової, фокус на першій відкритій.</summary>
        private void OnFlyClicked()
        {
            if (_overlay != Overlay.Reveal || State == null)
                return;
            _overlay = Overlay.None;
            _galaxyIndex = State.CurrentGalaxy;
            _galaxy = BuildGalaxy();
            Apply();
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.FocusChanged -= OnFocusChanged;
        }

        private void OnOpenClicked()
        {
            if (_galaxy == null || carousel == null)
                return;
            var index = carousel.Focus;
            if (index < 0 || index >= _galaxy.Planets.Count || _galaxy.Planets[index].State == PlanetState.Locked)
                return;
            if (_readOnly)
            {
                // Чужа планета — лише та, у якій гравець щось поставив (замкнена в нього — «Ще не відкрита»).
                if (_visitor != null && _galaxy.Planets[index].FilledSlots > 0)
                    VisitorPlanetRequested?.Invoke(_visitor, index);
                return;
            }
            OpenRequested?.Invoke(_galaxy.Index, index);
        }

        private void OnFocusChanged(int index)
        {
            if (_galaxy == null || design == null || index < 0 || index >= _galaxy.Planets.Count)
                return;
            _focusGalaxy = _galaxy.Index;
            _focusPlanet = index;

            ApplyDots(index);

            var planet = _galaxy.Planets[index];
            if (planetName != null) planetName.text = planet.Name;

            // «комірка» на екрані (SPEC «Правила інтерфейсу»), у коді й доках — «слот».
            var total = planet.TotalSlots;
            if (planetZones != null)
                planetZones.text = planet.State switch
                {
                    PlanetState.Done => total == 1
                        ? "Комірку заповнено"
                        : $"Усі {Plural.Count(total, "комірка", "комірки", "комірок")} заповнені",
                    PlanetState.Current => $"{planet.FilledSlots} з {total} {Plural.Of(total, "комірки", "комірок", "комірок")} заповнено",
                    _ => _visitor != null ? "Ще не відкрита" : "Заверши попередню планету"
                };

            // На замкненій планеті кнопка лишається на місці, але плоска й неклікабельна (button_off, S03_03):
            // прибирати її зовсім — значить смикати розкладку на кожному свайпі.
            if (openButton != null && (!_readOnly || _visitor != null))
                openButton.Interactable = _readOnly ? planet.FilledSlots > 0 : planet.State != PlanetState.Locked;
        }

        /// <summary>Пагінація: крапка на планету, активна — бірюзова пігулка (S03). Розмір — на зміну фокуса, не щокадру.</summary>
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
                dot.sprite = on ? pillSprite : dotSprite;
                dot.color = on ? Color.white : design.GalaxyDotInactive;
                var size = dot.GetComponent<LayoutElement>();
                if (size != null)
                    size.preferredWidth = size.minWidth = U1Px(on ? design.GalaxyDotActive : design.GalaxyDot);
            }
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }
    }
}
