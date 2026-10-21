using InkFlow.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace InkFlow.Style
{
    /// <summary>
    /// ЄДИНЕ джерело візуальних значень гри. Витягнуто з еталона U1Glaze
    /// (docs/StyleRef/U1Glaze, полотно 402×874 px) і перераховано в reference-одиниці UGUI
    /// 1080×1920 з коефіцієнтом 1080/402 ≈ 2.687.
    ///
    /// ПРАВИЛО: жодного кольору, радіуса чи тривалості в коді або префабах —
    /// усе читається звідси. Інакше зміна стилю перетворюється на пошук по всьому проєкту.
    /// Табличне дзеркало цих значень — у CLAUDE.md, розділ «Дизайн-система».
    /// </summary>
    [CreateAssetMenu(fileName = "DesignSystem", menuName = "Ink Flow/Design System")]
    public sealed class DesignSystem : ScriptableObject
    {
        /// <summary>
        /// Ширина полотна макета в px — 402×874 (U1Glaze, SCREENS.md). ЄДИНА константа масштабу
        /// на проєкт: кожен збирач бере <see cref="MockupToReference"/> звідси, а не тримає свою копію.
        /// </summary>
        public const float MockupWidth = 402f;

        /// <summary>Ширина reference-полотна UGUI.</summary>
        public const float ReferenceWidth = 1080f;

        /// <summary>Множник px макета → reference-одиниці UGUI.</summary>
        public const float MockupToReference = ReferenceWidth / MockupWidth;

        /// <summary>
        /// Версія набору токенів. Піднімати щоразу, коли змінюються значення за замовчуванням:
        /// бутстрап порівнює її з тією, що записана в .asset, і переписує асет свіжими
        /// значеннями. Без цього виправлені токени лишались би тільки в коді, а гра
        /// продовжувала б читати старий асет.
        /// </summary>
        public const int CurrentTokenVersion = 21;

        [HideInInspector] [SerializeField] private int tokenVersion = CurrentTokenVersion;

        public int TokenVersion => tokenVersion;

        // ═════════════════════════ U1Glaze (docs/StyleRef/U1Glaze/tokens.json) ═════════════════════════
        // Єдине джерело стилю інтерфейсу SpacePaint. Значення — з tokens.json і SPEC.md; розміри —
        // у reference-одиницях (px макета 402×874 × MockupToReference). Нижче по файлу — токени екрана
        // забігу (K1Candy, W4DarkCanvas, A1Breathe), фону, планет і кривих руху; токени старого стилю
        // «Ink Flow v2» прибрано на кроці 10 U1Glaze.

        [Header("U1Glaze: палітра")]
        [Tooltip("Фон екрана: радіальний градієнт bg-top → space-900.")]
        [SerializeField] private Color spaceTop = Hex("#221C52");
        [SerializeField] private Color spaceBottom = Hex("#070718");
        [Tooltip("Проміжні зупинки фону між spaceTop і spaceBottom (частки змішування для 2…5 кілець радіального " +
                 "градієнта; 1-ше — spaceTop, 6-те — spaceBottom). Шість кілець замість двох — без бандингу.")]
        [SerializeField] private Vector4 spaceGradientMix = new Vector4(0.3f, 0.55f, 0.78f, 0.92f);
        [Tooltip("Панель, картка, діалог: градієнт зверху вниз (запечено в спрайтах panel_*; тут — для тонування й тексту).")]
        [SerializeField] private Color panelTop = Hex("#26234F");
        [SerializeField] private Color panelBottom = Hex("#16142F");
        [SerializeField] private Color panelLowTop = Hex("#1F1C44");
        [SerializeField] private Color panelLowBottom = Hex("#141230");
        [Tooltip("Скло: кнопки-іконки, капсула нафти, другорядна кнопка, чипи.")]
        [SerializeField] private Color glassTop = Hex("#2E2A5E");
        [SerializeField] private Color glassBottom = Hex("#1B1840");
        [Tooltip("Заглиблення: лунки, доріжки перемикачів і сегментів, порожні мітки.")]
        [SerializeField] private Color well = Hex("#0C0B26");
        [Tooltip("Тонкі роздільники рядків у картках (line-700).")]
        [SerializeField] private Color line = Hex("#262645");
        [Tooltip("Затемнення під діалогом (альфа — scrimOpacity).")]
        [SerializeField] private Color scrim = Hex("#05040F");

        [Header("U1Glaze: текст")]
        [Tooltip("Основний текст (ink у tokens.json; тут InkText — «Ink» у проєкті означає чорнило).")]
        [SerializeField] private Color inkText = Hex("#F2F4FF");
        [SerializeField] private Color inkMuted = Hex("#A9ABD0");
        [Tooltip("Лише дрібні допоміжні підписи (4.8:1 на panel-bottom).")]
        [SerializeField] private Color inkDim = Hex("#7F81AB");
        [Tooltip("Текст вимкненої (плоскої) кнопки.")]
        [SerializeField] private Color buttonOffText = Hex("#6F71A0");

        [Header("U1Glaze: акценти")]
        [Tooltip("Головна дія — коралова цукерка (верх/середина/низ запечено в спрайті).")]
        [SerializeField] private Color coral = Hex("#F2566F");
        [SerializeField] private Color coral300 = Hex("#FFA592");
        [SerializeField] private Color coral700 = Hex("#C9345A");
        [Tooltip("Підтвердження, активна вкладка, увімкнений перемикач; текст на бірюзовому — tealInk.")]
        [SerializeField] private Color teal = Hex("#3FD6B8");
        [Tooltip("teal-300: світла грань бірюзи, неонова рамка (кікер «УСІ КОМІРКИ ЗАПОВНЕНІ», кільце вибраної комірки).")]
        [SerializeField] private Color teal300 = Hex("#8FF5DF");
        [SerializeField] private Color teal100 = Hex("#C4FFF3");
        [SerializeField] private Color teal700 = Hex("#138A76");
        [SerializeField] private Color tealInk = Hex("#05332D");
        [Tooltip("Бейдж «Найвигідніше», підкреслення активного табу, рекорд.")]
        [SerializeField] private Color amber = Hex("#FFB35C");
        [SerializeField] private Color amber300 = Hex("#FFE9A0");
        [SerializeField] private Color amberInk = Hex("#4A2400");
        [Tooltip("Бейдж «Популярне», лого «Space».")]
        [SerializeField] private Color pink = Hex("#FF7FB5");
        [SerializeField] private Color pink300 = Hex("#FFC6DC");
        [SerializeField] private Color pinkInk = Hex("#4A0026");
        [Tooltip("Рекорд і перше місце (11:1 на panel-bottom).")]
        [SerializeField] private Color record = Hex("#FFC46B");
        [Tooltip("Текст помилки й контур поля з помилкою.")]
        [SerializeField] private Color danger = Hex("#FF8A8A");
        [SerializeField] private Color dangerLine = Hex("#FF6F7F");

        [Header("U1Glaze: рідкість (рамки, мітки, чипи) — Звичайна … Космічна")]
        [SerializeField] private Color[] rarityFrames =
        {
            Hex("#9A9CC0"), Hex("#58D68D"), Hex("#5FB6FF"), Hex("#B77BFF"), Hex("#FFB35C"), Hex("#FF6FAE")
        };

        [Tooltip("Мітки 1–6 під МАЛОЮ рамкою (< 70 px) — частка від натурального розміру спрайта pips_N. " +
                 "У натуральному розмірі мітки Космічної (65 px) ширші за малу рамку й налазять на сусідні.")]
        [SerializeField, Range(0.3f, 1f)] private float rarityPipsSmallScale = 0.6f;

        [Header("U1Glaze: типографіка (Nunito 900 — заголовки, числа, кнопки; 700 — текст)")]
        [Tooltip("Nunito Black 900: ui-display, ui-h1…h3, ui-button, ui-label.")]
        [SerializeField] private TMPro.TMP_FontAsset? fontBlack;
        [Tooltip("Nunito Bold 700: ui-body, ui-caption, рядки списків.")]
        [SerializeField] private TMPro.TMP_FontAsset? fontBody;
        [Tooltip("Матеріал із тінню тексту для коралової кнопки (Nunito Black SDF - Shadow).")]
        [SerializeField] private Material? fontBlackShadow;
        [SerializeField] private float uiDisplay = 145f;      // 54 px — лого, велике число при програші
        [SerializeField] private float uiH1 = 75f;            // 28 px — назва екрана, планети, діалогу
        [SerializeField] private float uiH2 = 59f;            // 22 px — заголовки карток, числа статистики
        [SerializeField] private float uiH3 = 48f;            // 18 px — імена, назви картинок, рядки списків
        [SerializeField] private float uiButton = 54f;        // 20 px — текст кнопок 56
        [SerializeField] private float uiButtonSmall = 46f;   // 17 px — текст кнопок 46
        [SerializeField] private float uiBody = 43f;          // 16 px — пояснення
        [SerializeField] private float uiLabel = 32f;         // 12 px — підписи розділів великими
        [SerializeField] private float uiCaption = 35f;       // 13 px — дрібні підписи й лічильники
        [Tooltip("Розрядка підписів великими літерами: .16em макета (одиниці TMP — соті em).")]
        [SerializeField] private float uiLabelSpacing = 16f;
        [Tooltip("Розрядка напису бейджа (TMP: сотні em): .04em — щільніше за ui-label (.16em). На S01Home «14 КАРТИНОК» " +
                 "займає ~94 px при 12/900, тобто майже без розрядки; .16em роздувало бейдж на ~20 px.")]
        [SerializeField] private float uiBadgeSpacing = 4f;
        [Tooltip("Тінь тексту коралової кнопки: 0 −2 #6E0A28 35 % (SPEC).")]
        [SerializeField] private Color buttonTextShadow = new Color(0.43f, 0.04f, 0.16f, 0.35f);
        [SerializeField, Range(-1f, 0f)] private float buttonTextShadowOffsetY = -0.35f;
        [SerializeField, Range(0f, 1f)] private float buttonTextShadowSoftness = 0.2f;

        [Header("U1Glaze: розміри, reference-одиниці (px макета × 2.687)")]
        [SerializeField] private float headerHeight = 150f;       // 56
        [SerializeField] private float navHeight = 204f;          // 76
        [SerializeField] private float buttonHeight = 150f;       // 56
        [SerializeField] private float buttonSmallHeight = 124f;  // 46
        [SerializeField] private float iconButtonSize = 124f;     // 46
        [SerializeField] private float tapMin = 118f;             // 44 — нічого меншого
        [SerializeField] private float sideGutter = 43f;          // 16 — бічні поля екрана
        [SerializeField] private float cardGap = 32f;             // 12 — між картками й кнопками
        [SerializeField] private float listRowHeight = 150f;      // 56 — рядок списку (56–64)
        [SerializeField] private float navItemTap = 161f;         // 60 — пункт навігації

        [Header("U1Glaze: прозорості й анімації (повзунки)")]
        [Tooltip("Затемнення під діалогом.")]
        [SerializeField, Range(0f, 1f)] private float scrimOpacity = 0.78f;
        [Tooltip("Затемнення під нижнім листом дій.")]
        [SerializeField, Range(0f, 1f)] private float sheetScrimOpacity = 0.55f;
        [Tooltip("Недоступний вміст (вимкнені рядки й елементи).")]
        [SerializeField, Range(0f, 1f)] private float disabledOpacity = 0.4f;
        [Tooltip("Сила гало під кнопками (button_glow_*).")]
        [SerializeField, Range(0f, 1f)] private float buttonGlowAlpha = 0.32f;
        [Tooltip("Сила гало навколо рамок Епічної+ (frame_glow_*).")]
        [SerializeField, Range(0f, 1f)] private float frameGlowAlpha = 1f;
        [Tooltip("Поява діалогу: масштаб від dialogAppearScale до 1 і альфа 0 → 1.")]
        [SerializeField, Min(0.05f)] private float dialogAppearDuration = 0.2f;
        [SerializeField, Range(0.5f, 1f)] private float dialogAppearScale = 0.92f;
        [Tooltip("Наскільки темнішає кнопка під пальцем (частка) і на скільки секунд.")]
        [SerializeField, Range(0f, 0.6f)] private float pressDarken = 0.18f;
        [SerializeField, Min(0.02f)] private float pressDarkenDuration = 0.08f;
        [Tooltip("Один оберт переливу Космічної рамки, сек.")]
        [SerializeField, Min(0.5f)] private float cosmicSheenPeriod = 6f;
        [Tooltip("«Планета ожила»: гало 0 → 1, сек.")]
        [SerializeField, Min(0.1f)] private float planetAliveDuration = 0.6f;

        [Header("U1Glaze: планета — «Сфера» до N комірок, «Горизонт» від N+1")]
        [Tooltip("До стількох комірок — куля, що крутиться; від наступної — «Горизонт».")]
        [SerializeField, Range(1, 24)] private int sphereMaxSlots = 8;
        [Tooltip("Нахил осі кулі до глядача, градуси (globe_slots.py: 16).")]
        [SerializeField, Range(0f, 45f)] private float sphereAxisTilt = 16f;
        [Tooltip("«Сфера»: діаметр кулі — частка ширини екрана (S04: 291.5 / 402).")]
        [SerializeField, Range(0.4f, 0.9f)] private float sphereWidthShare = 0.725f;
        [Tooltip("«Сфера»: висота кулі з дугою й мітками — частка діаметра; ділянці, нижчій за це, куля меншає (SE).")]
        [SerializeField, Range(1f, 1.6f)] private float sphereExtent = 1.3f;
        [Tooltip("Гало кулі — частка діаметра (S04: до 1.33 r); «планета ожила» — сильніше (1.64 r), з'являється за planetAliveDuration.")]
        [SerializeField, Range(1f, 2f)] private float sphereHalo = 1.33f;
        [SerializeField, Range(1f, 2.4f)] private float sphereAliveHalo = 1.64f;
        [Tooltip("Бірюзова дуга видимої половини (S04): внутрішній і зовнішній радіус — частки діаметра (162.5 і 187.5 з 291.5), прозорість.")]
        [SerializeField, Range(0.5f, 0.7f)] private float sphereArcInner = 0.557f;
        [SerializeField, Range(0.5f, 0.75f)] private float sphereArcOuter = 0.643f;
        [SerializeField, Range(0f, 1f)] private float sphereArcAlpha = 0.18f;
        [Tooltip("Кільце міток навколо кулі: радіус до центру мітки й сторона мітки — частки діаметра (175 і 16 з 291.5).")]
        [SerializeField, Range(0.5f, 0.8f)] private float spherePipRing = 0.6f;
        [SerializeField, Range(0.02f, 0.1f)] private float spherePipSize = 0.055f;
        [Tooltip("Самообертання кулі, градусів/с (0 — стоїть; знак — напрям: мінус — поверхня йде вправо).")]
        [SerializeField, Range(-60f, 60f)] private float sphereAutoSpin = -8f;
        [Tooltip("Свайп по кулі: градусів на px макета руху пальця.")]
        [SerializeField, Range(0.05f, 2f)] private float sphereSwipeRate = 0.6f;
        [Tooltip("Інерція свайпу: згасання вибігу за секунду (більше — спиняється різкіше).")]
        [SerializeField, Range(0.5f, 12f)] private float sphereSwipeDamping = 4f;
        [Tooltip("Палець стояв довше за стільки секунд перед відпусканням — вибігу немає.")]
        [SerializeField, Range(0.02f, 0.5f)] private float sphereFlingHold = 0.08f;
        [Tooltip("Тап по комірці з глибиною меншою за поріг спершу довертає її до центру (SPEC: d < 0.6, 0.35 с).")]
        [SerializeField, Range(0f, 1f)] private float sphereTurnDepth = 0.6f;
        [SerializeField, Min(0.05f)] private float sphereTurnDuration = 0.35f;
        [Tooltip("Від коралової «Поставити картинку» до підказки, px (S04_01, S12_05: 13.5 до літер).")]
        [SerializeField, Min(0f)] private float planetHintGap = 9f;
        [Tooltip("Обводка панелі «планета ожила»: бірюзове гало навколо (S04_05: ≈ 13 % зовні, лінія ≈ 40 % по краю).")]
        [SerializeField, Range(0f, 1f)] private float planetAliveRingAlpha = 0.25f;
        [Tooltip("Вибрана комірка: масштаб (SPEC: 1.1).")]
        [SerializeField, Range(1f, 1.4f)] private float slotSelectedScale = 1.1f;

        [Tooltip("«Горизонт»: радіус великої планети, px макета (SPEC: ≈720); проміжок між комірками вздовж дуги (SPEC: 12).")]
        [SerializeField, Min(200f)] private float horizonRadius = 720f;
        [FormerlySerializedAs("horizonRowGap")]
        [SerializeField, Min(0f)] private float horizonSlotGap = 12f;
        [Tooltip("«Горизонт»: крок рядів (проміжок між рядами вздовж радіуса) і відступ дальнього ряду від краю планети, px (S05: 24, 37).")]
        [SerializeField, Min(0f)] private float horizonRowStep = 24f;
        [SerializeField, Min(0f)] private float horizonTopInset = 37f;
        [Tooltip("«Горизонт»: найбільша комірка (ближній ряд) і перспектива — у скільки разів дальший ряд менший (S05: 86.5, 0.877).")]
        [SerializeField, Range(40f, 140f)] private float horizonNearSlot = 86.5f;
        [SerializeField, Range(0.5f, 1f)] private float horizonPerspective = 0.877f;
        [Tooltip("«Горизонт»: бічне поле ряду, найдовший ряд (S05: 21 px, 7 комірок).")]
        [SerializeField, Min(0f)] private float horizonSideInset = 21f;
        [SerializeField, Range(3, 12)] private int horizonMaxRow = 7;
        [Tooltip("«Горизонт»: глибина сцени від верху планети до кнопки, найменше неба над планетою, проміжок над кнопкою, px " +
                 "(S05: 375.6, 113, 12). Нижчій ділянці вся сцена рівномірно меншає (SE: ×0.81).")]
        [SerializeField, Min(100f)] private float horizonDepth = 375.6f;
        [SerializeField, Min(0f)] private float horizonSkyMin = 113f;
        [SerializeField, Min(0f)] private float horizonBottomGap = 12f;
        [Tooltip("«Горизонт»: рядок міток під назвою — мітка, проміжок і найбільша ширина рядка, px (S05_01: 16 / 3; S05_02: 14 / 2).")]
        [SerializeField, Range(8f, 24f)] private float horizonPipSize = 16f;
        [SerializeField, Range(0f, 8f)] private float horizonPipGap = 3f;
        [SerializeField, Min(100f)] private float horizonPipRowMax = 330f;

        [Header("U1Glaze: галактика (S03) — частки діаметра планети, px макета там, де сказано")]
        [Tooltip("Найбільший діаметр планети у фокусі, px макета (S03Galaxy_01: 225). Менше — коли каруселі бракує висоти.")]
        [SerializeField, Range(120f, 300f)] private float galaxyPlanetSide = 225f;
        [Tooltip("Кільце міток комірок: радіус до центру мітки й сторона мітки — частки діаметра (137/225 і 16/225).")]
        [SerializeField, Range(0.5f, 0.8f)] private float galaxyPipRing = 0.609f;
        [SerializeField, Range(0.03f, 0.12f)] private float galaxyPipSize = 0.071f;
        [Tooltip("Бейдж-галочка ожилої планети: сторона й зсув центру від центру планети по x і y (44/225 і 81/225).")]
        [SerializeField, Range(0.1f, 0.3f)] private float galaxyBadgeSize = 0.196f;
        [SerializeField, Range(0.2f, 0.5f)] private float galaxyBadgeOffset = 0.36f;
        [Tooltip("Галочка в бейджі — частка діаметра (30/225).")]
        [SerializeField, Range(0.05f, 0.25f)] private float galaxyBadgeIcon = 0.133f;
        [Tooltip("Плитка із замком замкненої планети й іконка в ній (60/225 і 30/225).")]
        [SerializeField, Range(0.15f, 0.4f)] private float galaxyLockTile = 0.267f;
        [SerializeField, Range(0.08f, 0.2f)] private float galaxyLockIcon = 0.133f;
        [Tooltip("Мітки й бейдж видно лише у фокусі: гаснуть як 1 − |зсув|·k, поки планета їде з центру.")]
        [SerializeField, Range(1f, 6f)] private float galaxyFocusFade = 2.5f;
        [Tooltip("Пагінація: крапка, активна пігулка, проміжок — px макета (S03: 8, 22, 6).")]
        [SerializeField, Min(2f)] private float galaxyDot = 8f;
        [SerializeField, Min(4f)] private float galaxyDotActive = 22f;
        [SerializeField, Min(0f)] private float galaxyDotGap = 6f;
        [Tooltip("Нова галактика (S03_05): гало планети-героя — частка діаметра (≈ 370/221).")]
        [SerializeField, Range(1.1f, 2.2f)] private float galaxyRevealHaze = 1.68f;
        [Tooltip("«Галактику завершено» (S03_04): дуга планет — радіус і діаметр планети, px макета, і розмах дуги, градуси.")]
        [SerializeField, Min(40f)] private float galaxyArcRadius = 120f;
        [SerializeField, Min(10f)] private float galaxyArcPlanet = 35f;
        [SerializeField, Range(60f, 180f)] private float galaxyArcSpread = 140f;
        [Tooltip("«Галактику завершено»: проміжок від найнижчих планет дуги до напису «ГАЛАКТИКА …», px макета (S03_04 ≈ 61 разом зі spacing панелі).")]
        [SerializeField, Min(0f)] private float galaxyArcBottomGap = 32f;
        [Tooltip("Ритм під планетою, px макета (S03_01, S12_04): підпис → «Відкрити», «Відкрити» → крапки, крапки → навігація.")]
        [SerializeField, Min(0f)] private float galaxyOpenGap = 14f;
        [SerializeField, Min(0f)] private float galaxyDotsGapTop = 18f;
        [SerializeField, Min(0f)] private float galaxyDotsGapBottom = 16f;
        [Tooltip("Бейдж-галочка: світлий обідок навколо бірюзового кола, px макета (S03_02).")]
        [SerializeField, Range(0f, 6f)] private float galaxyBadgeRim = 2.5f;
        [Tooltip("Неактивна крапка пагінації: тон кружка toggle_knob_off (≈ #73759E) — на екрані ≈ #454552, як на S03. " +
                 "Непрозорий: альфа в лінійному просторі змішувалась би вдвічі яскравіше за CSS макета.")]
        [SerializeField] private Color galaxyDotInactive = new Color(0.60f, 0.59f, 0.52f, 1f);
        [Tooltip("Орбіта місяця — частка діаметра. Усередині кільця міток (galaxyPipRing − пів мітки − пів місяця), " +
                 "інакше на Аквілі місяць ховався під мітки о 3-й і 9-й.")]
        [SerializeField, Range(0.3f, 0.6f)] private float galaxyMoonOrbit = 0.52f;

        // ───────────────────────── Кольори картинок ─────────────────────────
        // Кольори фігур, поля й пікселів — це майстер-палітра Core (§3), а не токени
        // дизайн-системи: одна таблиця на гру, картинки й інтерфейс не можуть розійтись.

        [Header("Космічний фон (radial 130%×90% з точки 50% / -12%)")]
        [SerializeField] private Color backgroundEdge = Hex("#070512");

        [Tooltip("Позиції зупинок градієнта фону (0..1), як у макеті: 0 / .34 / .68 / 1.")]
        [SerializeField] private Vector4 backgroundStops = new Vector4(0f, 0.34f, 0.68f, 1f);

        [Header("Неонові акценти")]
        [SerializeField] private Color accentGold = Hex("#FFB300");

        [Header("Текст")]
        [SerializeField] private Color textPrimary = Hex("#FFFFFF");
        [SerializeField] private Color textMuted = new Color(1f, 1f, 1f, 0.62f);

        // ───────────────────────── Типографіка ─────────────────────────

        [Header("Типографіка")]
        [Tooltip("Макет намальовано в Baloo 2; у грі — Nunito (Baloo 2 не має кирилиці).")]
        [SerializeField] private TMPro.TMP_FontAsset? font;

        [Tooltip("Розріджені великі літери (labels): .14em макета.")]
        [SerializeField] private float letterSpacingWide = 14f;

        // ───────────────────────── Крапля ─────────────────────────

        // ───────────────────────── Рух ─────────────────────────

        [Header("Рух: криві (дзеркала cubic-bezier макета)")]
        [Tooltip("cubic-bezier(.34,1.56,.64,1) — «back-out», основна крива натискань.")]
        [SerializeField]
        private AnimationCurve curveBackOut = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3.2f), new Keyframe(0.45f, 1.12f), new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("cubic-bezier(.3,1.6,.4,1) — пружне приземлення.")]
        [SerializeField]
        private AnimationCurve curveLand = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.45f, 1.25f), new Keyframe(0.75f, 0.92f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("ease-in-out — погойдування, пульсації.")]
        [SerializeField] private AnimationCurve curveEaseInOut = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Зорі фону")]
        [Tooltip("Мерехтіння дальнього шару, сек.")]
        [SerializeField] private float starTwinkleSlow = 11f;

        [Tooltip("Мерехтіння ближнього шару, сек.")]
        [SerializeField] private float starTwinkleFast = 7f;

        [SerializeField, Min(0)] private int starCount = 90;

        [Header("Зорі фону: мерехтіння")]
        [Tooltip("Яка частка зір мерехтить. Решта світить рівно — так небо виглядає глибшим.")]
        [SerializeField, Range(0f, 1f)] private float starTwinkleFraction = 0.35f;

        [Header("Реакція на дотик")]
        [Tooltip("До якого масштабу стискається елемент під пальцем.")]
        [SerializeField, Range(0.85f, 1f)] private float pressScale = 0.96f;

        [Tooltip("Тривалість стискання, сек.")]
        [SerializeField, Range(0.02f, 0.3f)] private float pressDownDuration = 0.07f;

        [Tooltip("Тривалість пружного повернення (з overshoot).")]
        [SerializeField, Range(0.05f, 0.6f)] private float pressReleaseDuration = 0.26f;

        // ───────────────────────── Доступ ─────────────────────────

        public Color BackgroundEdge => backgroundEdge;
        public Vector4 BackgroundStops => backgroundStops;

        public Color AccentGold => accentGold;

        public Color TextPrimary => textPrimary;
        public Color TextMuted => textMuted;

        public TMPro.TMP_FontAsset? Font => font;
        public float LetterSpacingWide => letterSpacingWide;

        [Header("Планета-куля (галактика)")]
        [Tooltip("Діаметр планети у фокусі: 186 px макета.")]
        [SerializeField] private float planetSize = 515f;

        [Tooltip("Фінальна планета галактики більша: 208 px макета.")]
        [SerializeField] private float planetSizeFinale = 576f;

        [Tooltip("Крок каруселі між сусідніми планетами: 176 px макета.")]
        [SerializeField] private float planetCarouselStep = 487f;

        [Tooltip("Наскільки меншає планета за кожен крок від центру (макет: 0.4).")]
        [SerializeField, Range(0.1f, 0.8f)] private float planetCarouselScaleFalloff = 0.4f;

        [SerializeField, Range(0.1f, 1f)] private float planetCarouselMinScale = 0.34f;

        [Tooltip("Наскільки тьмянішає планета за кожен крок від центру (макет: 0.5).")]
        [SerializeField, Range(0.1f, 1f)] private float planetCarouselAlphaFalloff = 0.5f;

        [SerializeField, Range(0f, 1f)] private float planetCarouselMinAlpha = 0.14f;

        [Tooltip("Час прилипання до центру. Коротше — різко, довше — кисіль.")]
        [SerializeField, Range(0.05f, 0.6f)] private float planetCarouselSnapTime = 0.16f;

        [Tooltip("З якої швидкості змах перегортає на планету далі, індексів/с.")]
        [SerializeField, Min(0.2f)] private float planetCarouselFlickVelocity = 2.2f;

        [Tooltip("Серпанок атмосфери відносно діаметра планети (макет: 1.24).")]
        [SerializeField] private float planetAtmosphereScale = 1.24f;

        [SerializeField] private float planetRingWidthScale = 1.9f;
        [SerializeField] private float planetRingHeightScale = 0.6f;

        [SerializeField, Min(1f)] private float planetMoonPeriod = 8f;

        [Tooltip("Палітри поверхонь, СТРОГО в порядку PlanetType. " +
                 "Довжина мусить дорівнювати PlanetTypes.Count.")]
        [SerializeField] private PlanetPalette[] planetPalettes = DefaultPlanetPalettes();

        public float StarTwinkleFraction => starTwinkleFraction;

        [Header("Ігрове поле (блоки, лоток, привид)")]
        [Tooltip("Фігура лягає на поле: пружний поп-ін блоків.")]
        [SerializeField, Min(0f)] private float boardPlaceDuration = 0.2f;

        [Tooltip("Зрив лінії: блоки стискаються в нуль.")]
        [SerializeField, Min(0f)] private float lineClearDuration = 0.3f;

        [Tooltip("Пауза між лініями одного ходу — саме вона робить ланцюг читабельним.")]
        [SerializeField, Min(0f)] private float lineClearStagger = 0.07f;

        [Tooltip("Тряска поля на ланцюгу.")]
        [SerializeField, Min(0f)] private float boardShakeDuration = 0.43f;
        [SerializeField] private float boardShakeAmplitude = 14f;

        [Tooltip("З якої кількості ліній за хід трясти поле.")]
        [SerializeField, Min(2)] private int boardShakeFromLines = 3;

        [Tooltip("Привид фігури під пальцем: прозорість, коли фігура влазить і коли ні.")]
        [SerializeField, Range(0f, 1f)] private float ghostValidAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float ghostInvalidAlpha = 0.18f;
        [SerializeField] private Color ghostInvalidTint = Hex("#FF5A78");

        [Tooltip("Підсвітка ліній, які зірвуться, якщо відпустити фігуру тут: чиста яскравіша.")]
        [SerializeField, Range(0f, 1f)] private float linePreviewPureAlpha = 0.34f;
        [SerializeField, Range(0f, 1f)] private float linePreviewMixedAlpha = 0.12f;

        [SerializeField] private Color boardPlateStroke = new Color(1f, 1f, 1f, 0.1f);

        [Tooltip("Лоток: прозорість фігури, поки її тягнуть.")]
        [SerializeField, Range(0f, 1f)] private float trayDraggingAlpha = 0.3f;

        [Header("K1Candy: панель, лунки, блоки, лоток (docs/StyleRef/K1Candy/)")]
        [Tooltip("Кут панелі поля й картинки: 28 px макета; слота лотка — 20.")]
        [SerializeField] private float boardPanelRadius = 78f;
        [SerializeField] private float traySlotRadius = 55f;
        [Tooltip("Тінь під панеллю: чорна ~60 %, зсув униз 10 px макета, розмиття 12.")]
        [SerializeField] private Color panelShadow = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private float panelShadowOffset = 28f;
        [SerializeField] private float panelShadowBlur = 33f;
        [Tooltip("Сторона блоку — частка кроку сітки (решта — проміжок).")]
        [SerializeField, Range(0.6f, 1f)] private float blockFraction = 0.88f;
        [Tooltip("Спрайт блоку в центрі має яскравість ~0.83 від білого: тонуємо кольором родини, " +
                 "помноженим на це число, щоб основний тон лягав рівно на середину блоку.")]
        [SerializeField, Range(1f, 1.4f)] private float blockTintBoost = 1.2f;
        [Tooltip("Стеля яскравості блока: найяскравіший канал після підсилення не вище цього числа, відтінок " +
                 "зберігається. Без стелі білий, вершки, срібло й м'ята давали чистий білий — без блиску й грані " +
                 "(знімок КОСМОНАВТА з білим полем). 1 — вимкнено.")]
        [SerializeField, Range(0.5f, 1f)] private float blockMaxBrightness = 0.92f;
        [Tooltip("Світіння блоку — спрайт-гало під блоком (НЕ Bloom): прозорість і розмір відносно блоку.")]
        [SerializeField, Range(0f, 1f)] private float blockGlowAlpha = 0.55f;
        [SerializeField, Range(1f, 2f)] private float blockGlowScale = 1.5f;
        [Tooltip("Смуга блиску й іскра — нетонований спрайт поверх блоку.")]
        [SerializeField, Range(0f, 1f)] private float blockHighlightAlpha = 1f;
        [Tooltip("Фігури в лотку — зменшені блоки: крок клітинки як частка кроку поля, коли вміщається.")]
        [SerializeField, Range(0.3f, 1f)] private float trayPieceScale = 0.62f;
        [Tooltip("Бічне поле панелі поля й лотка від краю екрана, px макета (шапка й рахунок тримають своє поле 16).")]
        [SerializeField, Range(0f, 24f)] private float boardSideMargin = BoardGeometry.DefaultSideMargin;
        [Tooltip("Відступ від краю панелі до крайніх лунок, px макета: в еталоні K1 ≈45 % кроку сітки (18 із 40) — " +
                 "у кілька разів більше за проміжок, і кутова лунка не тисне на заокруглений кут.")]
        [SerializeField, Range(0f, 40f)] private float boardPadding = BoardGeometry.DefaultPadding;
        [Tooltip("Проміжок між лунками, px макета (еталон K1: 4 при кроці 40).")]
        [SerializeField, Range(0f, 12f)] private float boardGap = BoardGeometry.DefaultGap;

        [Header("W4DarkCanvas: картинка на темному полотні (docs/StyleRef/W4DarkCanvas/)")]
        [Tooltip("Назва картинки над полотном (картки, колекція): #c9cbe8, великими, з розрядкою.")]
        [SerializeField] private Color pictureTitleColor = Hex("#C9CBE8");
        [Tooltip("Полотно #403c78 і порожнє поле навколо арту, пікселі арту.")]
        [SerializeField] private Color pictureCanvasColor = Hex("#403C78");
        [SerializeField, Range(0, 6)] private int pictureMargin = 2;
        [Tooltip("Плетіння: клітинка (пікселі арту), сила на полотні, частка сили крізь фарбу.")]
        [SerializeField, Range(0.05f, 2f)] private float pictureWeavePitch = 0.25f;
        [SerializeField, Range(0f, 0.2f)] private float pictureWeaveAlpha = 0.045f;
        [SerializeField, Range(0f, 1f)] private float pictureWeaveOnPaint = 0.6f;
        [Tooltip("Світло зверху й тінь знизу на фарбі (частка білого / чорного на краю арту).")]
        [SerializeField, Range(0f, 0.5f)] private float pictureLightTop = 0.16f;
        [SerializeField, Range(0f, 0.5f)] private float pictureShadeBottom = 0.16f;
        [Tooltip("Гало фарби (НЕ Bloom): розмиття в пікселях арту, найбільша альфа, підсилення розмитого покриття.")]
        [SerializeField, Range(0f, 4f)] private float pictureHaloSigma = 1.4f;
        [SerializeField, Range(0f, 1f)] private float pictureHaloAlpha = 0.45f;
        [SerializeField, Range(0.5f, 4f)] private float pictureHaloGain = 1.6f;
        [Tooltip("Контури незафарбованого: колір, альфа, товщина (пікселі арту, не тонше пікселя екрана).")]
        [SerializeField] private Color pictureSketchColor = Hex("#DFE0F5");
        [SerializeField, Range(0f, 1f)] private float pictureSketchAlpha = 0.35f;
        [SerializeField, Range(0.02f, 0.3f)] private float pictureSketchWidth = 0.07f;
        [Tooltip("Контури між УСІМА тонами, а не лише між родинами й по силуету (за замовчуванням вимкнено: " +
                 "у картинці 10–16 тонів, і лінії між світлом і тінню однієї родини шуміли б).")]
        [SerializeField] private bool pictureSketchAllTones;
        [Tooltip("Кут полотна (пікселі арту) і тонка темна рамка: альфа, товщина (пікселі арту).")]
        [SerializeField, Range(0f, 4f)] private float pictureCornerRadius = 1f;
        [SerializeField, Range(0f, 1f)] private float pictureBorderAlpha = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float pictureBorderWidth = 0.08f;
        [Tooltip("Проявлення кроку: 0 → 1 за стільки секунд (еталон 0.15–0.25), піксель росте з центру з легким «попом».")]
        [SerializeField, Min(0.05f)] private float pictureRevealDuration = 0.2f;
        [SerializeField, Range(0f, 0.5f)] private float pictureRevealPop = 0.15f;

        [Header("Картинка над полем: без рамки, лічильника й назви; рідкість — гало з-під неї")]
        [Tooltip("Найбільша сторона полотна над полем, px макета (U1Glaze S02Run_01: вікно картки 214; Сесія 5 — 154 по центру). " +
                 "Полотно стоїть ліворуч, праворуч — панелі рахунку заввишки з нього; блок = полотно + запас під найширше гало " +
                 "з кожного боку; на короткому екрані полотно стискається першим (поле → лоток → картинка, RunLayout).")]
        [SerializeField, Range(116f, 240f)] private float runCanvasMaxSide = 214f;
        [Tooltip("Гало рідкості з-під картинки: ширина за краєм полотна, px макета, — від звичайної до космічної. " +
                 "Чим вища рідкість, тим ширше; найширше задає запас блоку, тож гало не лізе на поле й рахунок.")]
        [SerializeField] private float[] rarityHaloWidths = { 6f, 8f, 10f, 12f, 14f, 16f };
        [Tooltip("Сила гало рідкості (альфа кольору рідкості) — від звичайної до космічної. На краю полотна видно ~половину.")]
        [SerializeField] private float[] rarityHaloAlphas = { 0.35f, 0.45f, 0.55f, 0.65f, 0.8f, 0.9f };

        [Header("Тривога поля «мало місця» (§11): A1Breathe, docs/StyleRef/A1Breathe/")]
        [Tooltip("Яскравість тривоги за рівнем: множник альфи всіх шарів рівня, і сяйв, і тонкої рамки (1 — як в еталоні). " +
                 "Автор після гри: на третину тьмяніше, а «останній хід» помітно сильніший — тож «мало місця» 0.6, «останній хід» 0.75. " +
                 "Тонка рамка однакова для обох рівнів, і на тонкій смузі вона стирала різницю між ними.")]
        [SerializeField, Range(0f, 1f)] private float pulseBrightnessWarn = 0.6f;
        [SerializeField, Range(0f, 1f)] private float pulseBrightnessStrong = 0.75f;
        [Tooltip("Товщина смуги сяйва: множник ширини штриха й розмиття (1 — як в еталоні A1Breathe). Пік альфи від неї " +
                 "не змінюється — лише ширина. Тонка рамка завжди 2 px макета. Автор після гри: удвічі тонше.")]
        [SerializeField, Range(0.25f, 1.5f)] private float pulseThickness = 0.5f;
        [Tooltip("Градієнт по діагоналі панелі: лівий верхній кут → правий нижній (еталон: кораловий → бурштиновий).")]
        [SerializeField] private Color pulseColorFrom = Hex("#FF5E7A");
        [SerializeField] private Color pulseColorTo = Hex("#FFB35C");
        [Tooltip("Дихання сяйва: a = мін + (макс − мін)·(0.5 − 0.5·cos(2π·t / період)).")]
        [SerializeField, Range(0f, 1f)] private float pulseAlphaMin = 0.25f;
        [SerializeField, Range(0f, 1f)] private float pulseAlphaMax = 1f;
        [Tooltip("Період дихання: «мало місця» 1.8 с, «останній хід» 0.9 с.")]
        [SerializeField, Min(0.2f)] private float pulseCalmPeriod = 1.8f;
        [SerializeField, Min(0.2f)] private float pulseCriticalPeriod = 0.9f;
        [Tooltip("Перехід між рівнями й згасання тривоги — за стільки секунд, без стрибка.")]
        [SerializeField, Min(0.05f)] private float pulseFadeDuration = 0.3f;

        [Header("Рахунок і рекорд (§11)")]
        [Tooltip("Число спершу меншає до цієї частки розміру, і лише потім переходить у компактний формат.")]
        [SerializeField, Range(0.3f, 1f)] private float scoreMinFontScale = 0.55f;
        [Tooltip("Ширина табличної цифри в em — цифри набираються моноширинно, щоб ширина не стрибала.")]
        [SerializeField, Range(0.4f, 0.8f)] private float scoreDigitEm = 0.62f;

        [Header("Краплі в картинку (§5)")]
        [Tooltip("Політ краплі з клітинки в піксель: тривалість, пауза між краплями, висота дуги, розмір.")]
        [SerializeField, Min(0.05f)] private float dropFlightDuration = 0.38f;
        [SerializeField, Min(0f)] private float dropStagger = 0.028f;
        [SerializeField] private float dropArc = 70f;
        [SerializeField, Min(4f)] private float dropSize = 28f;
        [Tooltip("«Пуф» на пікселі: до якого масштабу розпливається й за скільки гасне.")]
        [SerializeField, Min(1f)] private float dropPuffScale = 2.1f;
        [SerializeField, Min(0.02f)] private float dropPuffDuration = 0.16f;

        [Header("Перефарбування хвилею (§5)")]
        [Tooltip("Пауза між клітинками хвилі і стеля тривалості всієї хвилі.")]
        [SerializeField, Min(0f)] private float recolorWaveStagger = 0.02f;
        [SerializeField, Min(0.05f)] private float recolorWaveMaxDuration = 0.7f;

        [Header("Завершення картинки (§8)")]
        [Tooltip("Свайп: скільки одиниць канваса — рішення; нахил картки на одиницю зсуву; тривалості.")]
        [SerializeField, Min(20f)] private float completionSwipeThreshold = 220f;
        [SerializeField, Range(0f, 0.2f)] private float completionTilt = 0.03f;
        [SerializeField, Min(0.05f)] private float completionFlyDuration = 0.42f;
        [SerializeField, Min(0.05f)] private float completionEnterDuration = 0.35f;

        [Header("Рамки рідкості (§6)")]
        [Tooltip("Епічна — м'яке світіння; легендарна — відблиск раз на період; космічна — переливи й частинки.")]
        [SerializeField, Range(0f, 1f)] private float epicGlowAlpha = 0.3f;
        [SerializeField, Min(0.2f)] private float legendarySweepPeriod = 2.4f;
        [SerializeField, Min(0f)] private float cosmicHueSpeed = 0.12f;
        [SerializeField, Range(0f, 1f)] private float cosmicParticleAlpha = 0.85f;
        [SerializeField, Min(0f)] private float cosmicParticleSpeed = 0.3f;

        [Tooltip("Число «+фарба» над зірваною лінією: 25 px макета для чистої, 16 — для мішаної.")]
        [SerializeField] private float fontSizeLineFloatPure = 69f;
        [SerializeField] private float fontSizeLineFloatMixed = 44f;
        [SerializeField, Min(0.1f)] private float lineFloatDuration = 1.5f;
        [SerializeField] private float lineFloatRise = 80f;

        [Header("Нескінченний")]
        [Tooltip("Спалах у момент, коли рахунок перегнав рекорд — не після смерті.")]
        [SerializeField, Min(0.1f)] private float recordFlashDuration = 0.9f;

        [Tooltip("Множник ланцюга спливає по центру екрана: 60 px макета.")]
        [SerializeField] private float fontSizeComboPop = 166f;
        [SerializeField, Min(0.1f)] private float comboPopDuration = 1.1f;
        [SerializeField] private float comboPopRise = 120f;

        [Tooltip("Скільки секунд без ходу — і підказка сама покаже, куди влазить фігура; і скільки її видно.")]
        [SerializeField, Min(1f)] private float hintIdleDelay = 5f;
        [SerializeField, Min(0.2f)] private float hintShowDuration = 1.6f;

        [SerializeField] private Color picturePlateStroke = new Color(1f, 1f, 1f, 0.14f);
        [SerializeField, Min(0f)] private float pictureCompleteDuration = 1.1f;
        [SerializeField, Range(0f, 0.3f)] private float pictureCompletePop = 0.06f;
        [SerializeField, Range(0f, 1f)] private float pictureGlowAlpha = 0.55f;
        [SerializeField] private float fontSizePictureName = 27f;
        [SerializeField] private float fontSizePictureCaption = 24f;

        [Tooltip("Картка перед забігом: «цього забігу — така картинка». Скільки висить сама, поки не тапнули.")]
        [SerializeField, Min(0.5f)] private float runIntroDuration = 2.6f;
        [SerializeField, Min(0f)] private float runIntroFadeDuration = 0.25f;

        [Header("Кінець партії (Нескінченний)")]

        [Tooltip("Нагорода набігає цифрами, а не з'являється готовою.")]
        [SerializeField, Min(0.1f)] private float rewardCountDuration = 0.9f;

        [Tooltip("Скільки летить конфеті за новий рекорд.")]
        [SerializeField, Min(0.2f)] private float confettiFallDuration = 2.2f;

        [Header("Профіль")]

        [Tooltip("До якої частки кегля стискається нік, щоб уміститись в один рядок (візитка, шапка хаба).")]
        [SerializeField, Range(0.3f, 1f)] private float nickMinScale = 0.55f;

        [Header("Рейтинги «Ліга» (U1Glaze S08)")]
        [Tooltip("Рядок таблиці, px макета (S08: 56, між рядками — лише роздільник).")]
        [SerializeField, Min(44f)] private float rankRowHeightPx = 56f;
        [SerializeField, Min(0f)] private float rankRowGapPx = 0f;

        [Tooltip("Запас під останнім рядком у картці таблиці, px макета.")]
        [SerializeField, Min(0f)] private float rankListBottomPaddingPx = 8f;

        [Tooltip("Цифра на медалі топ-3 (S08: #2A1600 — на золоті білий не читається).")]
        [SerializeField] private Color medalInk = Hex("#2A1600");

        [Tooltip("Теплий відтінок рядка першого місця і бірюзовий — свого рядка (альфа поверх картки).")]
        [SerializeField, Range(0f, 1f)] private float rankRowFirstAlpha = 0.14f;
        [SerializeField, Range(0f, 1f)] private float rankRowYouAlpha = 0.16f;

        [Tooltip("Бірюзова обводка картки «Ти» (рішення автора, S08) і притлумлення картки без мережі (S08_04).")]
        [SerializeField, Range(0f, 1f)] private float youCardRingAlpha = 0.6f;
        [SerializeField, Range(0.2f, 1f)] private float youCardOfflineAlpha = 0.6f;

        [Header("Фарбування планети")]

        [Tooltip("Наближення до планети з екрана огляду.")]
        [SerializeField, Min(0.1f)] private float paintApproachDuration = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float paintApproachFromScale = 0.35f;

        [SerializeField] private float paintConfettiSize = 60f;
        [SerializeField] private float paintConfettiSpread = 160f;

        [Header("Колекція (U1Glaze S06)")]
        [Tooltip("Колонок у сітці колекції.")]
        [SerializeField, Range(2, 4)] private int collectionColumns = 3;
        [Tooltip("Картка колекції в px макета: рамка рідкості на всю ширину (114 на 402), під нею мітки, назва в два рядки й лічильник (S06Collection_01).")]
        [SerializeField, Min(60f)] private float collectionCardWidthPx = 114f;
        [SerializeField, Min(80f)] private float collectionCardHeightPx = 190f;
        [Tooltip("Проміжок між картками, px макета (по горизонталі й між рядами).")]
        [SerializeField, Min(0f)] private float collectionGapPx = 14f;
        [Tooltip("Прозорість картки, усі копії якої вже стоять у слотах (режим вибору).")]
        [SerializeField, Range(0.1f, 1f)] private float collectionUsedAlpha = 0.35f;

        public float BoardPlaceDuration => boardPlaceDuration;
        public float LineClearDuration => lineClearDuration;
        public float LineClearStagger => lineClearStagger;
        public float BoardShakeDuration => boardShakeDuration;
        public float BoardShakeAmplitude => boardShakeAmplitude;
        public int BoardShakeFromLines => boardShakeFromLines;
        public float GhostValidAlpha => ghostValidAlpha;
        public float GhostInvalidAlpha => ghostInvalidAlpha;
        public Color GhostInvalidTint => ghostInvalidTint;
        public float LinePreviewPureAlpha => linePreviewPureAlpha;
        public float LinePreviewMixedAlpha => linePreviewMixedAlpha;
        public float TrayDraggingAlpha => trayDraggingAlpha;

        public float BoardPanelRadius => boardPanelRadius;
        public float TraySlotRadius => traySlotRadius;
        public Color PanelShadow => panelShadow;
        public float PanelShadowOffset => panelShadowOffset;
        public float PanelShadowBlur => panelShadowBlur;
        public float BlockFraction => blockFraction;
        public float BlockGlowAlpha => blockGlowAlpha;
        public float BlockGlowScale => blockGlowScale;
        public float BlockHighlightAlpha => blockHighlightAlpha;
        public float TrayPieceScale => trayPieceScale;
        public float BoardSideMargin => boardSideMargin;

        /// <summary>Геометрія поля під токени відступів (px макета) — єдине місце, де вони зустрічаються з Core.</summary>
        public BoardGeometry BoardGeometryFor(int width, int height) =>
            BoardGeometry.For(width, height, boardSideMargin, boardPadding, boardGap);

        public float BlockMaxBrightness => blockMaxBrightness;

        /// <summary>
        /// Тонування білого спрайта блоку: основний тон родини лягає на середину блоку (K1Candy).
        /// Найяскравіший канал обрізається до <see cref="BlockMaxBrightness"/> зі збереженням відтінку:
        /// світлі родини лишаються світлими, але блиск і грань спрайта на них видно.
        /// </summary>
        public Color BlockTint(Color baseColor)
        {
            var r = baseColor.r * blockTintBoost;
            var g = baseColor.g * blockTintBoost;
            var b = baseColor.b * blockTintBoost;
            var peak = Mathf.Max(r, Mathf.Max(g, b));
            if (peak > blockMaxBrightness && peak > 0f)
            {
                var k = blockMaxBrightness / peak;
                r *= k;
                g *= k;
                b *= k;
            }
            return new Color(Mathf.Min(1f, r), Mathf.Min(1f, g), Mathf.Min(1f, b), 1f);
        }

        public Color PictureTitleColor => pictureTitleColor;
        public Color PictureCanvasColor => pictureCanvasColor;
        public int PictureMargin => pictureMargin;
        public float PictureWeavePitch => pictureWeavePitch;
        public float PictureWeaveAlpha => pictureWeaveAlpha;
        public float PictureWeaveOnPaint => pictureWeaveOnPaint;
        public float PictureLightTop => pictureLightTop;
        public float PictureShadeBottom => pictureShadeBottom;
        public float PictureHaloSigma => pictureHaloSigma;
        public float PictureHaloAlpha => pictureHaloAlpha;
        public float PictureHaloGain => pictureHaloGain;
        public Color PictureSketchColor => pictureSketchColor;
        public float PictureSketchAlpha => pictureSketchAlpha;
        public float PictureSketchWidth => pictureSketchWidth;
        public bool PictureSketchAllTones => pictureSketchAllTones;
        public float PictureCornerRadius => pictureCornerRadius;
        public float PictureBorderAlpha => pictureBorderAlpha;
        public float PictureBorderWidth => pictureBorderWidth;
        public float PictureRevealDuration => pictureRevealDuration;
        public float PictureRevealPop => pictureRevealPop;
        public float RunCanvasMaxSide => runCanvasMaxSide;

        /// <summary>Ширина гало рідкості за краєм полотна, px макета.</summary>
        public float RarityHaloWidth(Rarity rarity) => Pick(rarityHaloWidths, rarity, 6f + 2f * (int)rarity);

        /// <summary>Сила гало рідкості (0..1).</summary>
        public float RarityHaloAlpha(Rarity rarity) => Mathf.Clamp01(Pick(rarityHaloAlphas, rarity, 0.35f + 0.1f * (int)rarity));

        /// <summary>Запас блоку картинки під найширше гало, px макета.</summary>
        public float RarityHaloReserve
        {
            get
            {
                var max = 0f;
                for (var i = 0; i < Rarities.Count; i++)
                    max = Mathf.Max(max, RarityHaloWidth((Rarity)i));
                return max;
            }
        }

        private static float Pick(float[]? values, Rarity rarity, float fallback)
        {
            var i = (int)rarity;
            return values != null && i >= 0 && i < values.Length ? Mathf.Max(values[i], 0f) : fallback;
        }

        public float PulseAlphaMin => pulseAlphaMin;
        public float PulseAlphaMax => pulseAlphaMax;
        public float PulseCalmPeriod => pulseCalmPeriod;
        public float PulseCriticalPeriod => pulseCriticalPeriod;
        public float PulseFadeDuration => pulseFadeDuration;
        public float PulseBrightnessWarn => pulseBrightnessWarn;
        public float PulseBrightnessStrong => pulseBrightnessStrong;
        public float PulseThickness => pulseThickness;
        public Color PulseColorFrom => pulseColorFrom;
        public Color PulseColorTo => pulseColorTo;

        public float ScoreMinFontScale => scoreMinFontScale;
        public float ScoreDigitEm => scoreDigitEm;

        public float DropFlightDuration => dropFlightDuration;
        public float DropStagger => dropStagger;
        public float DropArc => dropArc;
        public float DropSize => dropSize;
        public float DropPuffScale => dropPuffScale;
        public float DropPuffDuration => dropPuffDuration;
        public float RecolorWaveStagger => recolorWaveStagger;
        public float RecolorWaveMaxDuration => recolorWaveMaxDuration;
        public float CompletionSwipeThreshold => completionSwipeThreshold;
        public float CompletionTilt => completionTilt;
        public float CompletionFlyDuration => completionFlyDuration;
        public float CompletionEnterDuration => completionEnterDuration;
        public float EpicGlowAlpha => epicGlowAlpha;
        public float LegendarySweepPeriod => legendarySweepPeriod;
        public float CosmicHueSpeed => cosmicHueSpeed;
        public float CosmicParticleAlpha => cosmicParticleAlpha;
        public float CosmicParticleSpeed => cosmicParticleSpeed;
        public float FontSizeLineFloatPure => fontSizeLineFloatPure;
        public float FontSizeLineFloatMixed => fontSizeLineFloatMixed;
        public float LineFloatDuration => lineFloatDuration;
        public float LineFloatRise => lineFloatRise;
        public float RecordFlashDuration => recordFlashDuration;
        public float FontSizeComboPop => fontSizeComboPop;
        public float ComboPopDuration => comboPopDuration;
        public float ComboPopRise => comboPopRise;
        public float HintIdleDelay => hintIdleDelay;
        public float HintShowDuration => hintShowDuration;
        public Color PicturePlateStroke => picturePlateStroke;
        public float PictureCompleteDuration => pictureCompleteDuration;
        public float PictureCompletePop => pictureCompletePop;
        public float PictureGlowAlpha => pictureGlowAlpha;
        public float FontSizePictureName => fontSizePictureName;
        public float FontSizePictureCaption => fontSizePictureCaption;
        public float RunIntroDuration => runIntroDuration;
        public float RunIntroFadeDuration => runIntroFadeDuration;

        /// <summary>
        /// Колір рідкості для гало, назв і старих рамок — той самий, що в рамок і бейджів U1Glaze (rarity-1 … rarity-6):
        /// одне джерело на гру (Сесія 1, рішення автора «так» на кольори рідкості з tokens.json).
        /// </summary>
        public Color RarityColor(Rarity rarity) => RarityFrame(rarity);

        /// <summary>Колір індексу майстер-палітри (§3) — фігури, клітинки поля, пікселі, краплі.</summary>
        public static Color PaletteColor(byte index) => MasterPalette.ColorOf(index).ToColor();
        public float RewardCountDuration => rewardCountDuration;
        public float ConfettiFallDuration => confettiFallDuration;
        public float NickMinScale => nickMinScale;
        public float RankRowHeight => rankRowHeightPx * MockupToReference;
        public float RankRowGap => rankRowGapPx * MockupToReference;
        public float RankListBottomPadding => rankListBottomPaddingPx * MockupToReference;
        public Color MedalInk => medalInk;
        public float RankRowFirstAlpha => rankRowFirstAlpha;
        public float RankRowYouAlpha => rankRowYouAlpha;
        public float YouCardRingAlpha => youCardRingAlpha;
        public float YouCardOfflineAlpha => youCardOfflineAlpha;

        public int CollectionColumns => collectionColumns;
        /// <summary>Картка колекції в reference-одиницях (px макета × <see cref="MockupToReference"/>).</summary>
        public float CollectionCardWidth => collectionCardWidthPx * MockupToReference;
        public float CollectionCardHeight => collectionCardHeightPx * MockupToReference;
        public float CollectionGap => collectionGapPx * MockupToReference;
        public float CollectionUsedAlpha => collectionUsedAlpha;
        public float PaintApproachDuration => paintApproachDuration;
        public float PaintApproachFromScale => paintApproachFromScale;
        public float PaintConfettiSize => paintConfettiSize;
        public float PaintConfettiSpread => paintConfettiSpread;

        public float PlanetSize => planetSize;
        public float PlanetSizeFinale => planetSizeFinale;
        public float PlanetCarouselStep => planetCarouselStep;
        public float PlanetCarouselScaleFalloff => planetCarouselScaleFalloff;
        public float PlanetCarouselMinScale => planetCarouselMinScale;
        public float PlanetCarouselAlphaFalloff => planetCarouselAlphaFalloff;
        public float PlanetCarouselMinAlpha => planetCarouselMinAlpha;
        public float PlanetCarouselSnapTime => planetCarouselSnapTime;
        public float PlanetCarouselFlickVelocity => planetCarouselFlickVelocity;
        public float PlanetAtmosphereScale => planetAtmosphereScale;
        public float PlanetRingWidthScale => planetRingWidthScale;
        public float PlanetRingHeightScale => planetRingHeightScale;
        public float PlanetMoonPeriod => planetMoonPeriod;

        /// <summary>
        /// Палітри дев'яти типів рівно з макета (масив PLANETS, поле pal).
        /// Порядок збігається з PlanetType — інакше Ocean дістане поверхню Rocky,
        /// і помітно це буде лише очима на екрані.
        /// </summary>
        private static PlanetPalette[] DefaultPlanetPalettes() => new[]
        {
            new PlanetPalette(Hex("#1F6F8F"), Hex("#2FA38A"), Hex("#7FE0FF"), 26f), // Ocean
            new PlanetPalette(Hex("#9C7A5A"), Hex("#5F4630"), Hex("#E7C9A0"), 34f), // Rocky
            new PlanetPalette(Hex("#BFE0F0"), Hex("#6FA8C9"), Hex("#D6F2FF"), 32f), // Ice
            new PlanetPalette(Hex("#2F6FD0"), Hex("#3FA65A"), Hex("#8FD0FF"), 30f), // Earth
            new PlanetPalette(Hex("#D8B37A"), Hex("#B98F55"), Hex("#F0D9A8"), 24f), // Rings
            new PlanetPalette(Hex("#8A5FB0"), Hex("#CAA2E0"), Hex("#D8B8FF"), 18f), // Gas
            new PlanetPalette(Hex("#3A2320"), Hex("#FF5A28"), Hex("#FF8A3C"), 30f), // Volcano
            new PlanetPalette(Hex("#CF9450"), Hex("#E6B46A"), Hex("#F4D29A"), 30f), // Desert
            new PlanetPalette(Hex("#E9DCFF"), Hex("#FFD6F0"), Hex("#FFFFFF"), 16f)  // Pearl
        };

        /// <summary>Палітра поверхні за типом планети. Поза межами — перша, щоб
        /// вкорочений масив давав тьмяну планету, а не виняток посеред свайпу.</summary>
        public PlanetPalette Planet(PlanetType type)
        {
            var i = (int)type;
            if (planetPalettes == null || planetPalettes.Length == 0)
                return default;
            return planetPalettes[i >= 0 && i < planetPalettes.Length ? i : 0];
        }

        public float PressScale => pressScale;
        public float PressDownDuration => pressDownDuration;
        public float PressReleaseDuration => pressReleaseDuration;

        public AnimationCurve CurveBackOut => curveBackOut;
        public AnimationCurve CurveLand => curveLand;
        public AnimationCurve CurveEaseInOut => curveEaseInOut;

        public float StarTwinkleSlow => starTwinkleSlow;
        public float StarTwinkleFast => starTwinkleFast;
        public int StarCount => starCount;

        /// <summary>Те саме, що darken() у макеті: змішування з чорним.</summary>
        public static Color Darken(Color c, float amount) =>
            Color.Lerp(c, new Color(0f, 0f, 0f, c.a), amount);

        public static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }

        /// <summary>Переводить px макета в reference-одиниці UGUI.</summary>

        // ───────────────────────── U1Glaze: акцесори ─────────────────────────
        public Color SpaceTop => spaceTop;
        public Vector4 SpaceGradientMix => spaceGradientMix;
        public Color SpaceBottom => spaceBottom;
        public Color PanelTop => panelTop;
        public Color PanelBottom => panelBottom;
        public Color PanelLowTop => panelLowTop;
        public Color PanelLowBottom => panelLowBottom;
        public Color GlassTop => glassTop;
        public Color GlassBottom => glassBottom;
        public Color Well => well;
        public Color Line => line;
        public Color Scrim => scrim;
        public Color InkText => inkText;
        public Color InkMuted => inkMuted;
        public Color InkDim => inkDim;
        public Color ButtonOffText => buttonOffText;
        public Color Coral => coral;
        public Color Coral300 => coral300;
        public Color Coral700 => coral700;
        public Color Teal => teal;
        public Color Teal100 => teal100;
        public Color Teal300 => teal300;
        public Color Teal700 => teal700;
        public Color TealInk => tealInk;
        public Color Amber => amber;
        public Color Amber300 => amber300;
        public Color AmberInk => amberInk;
        public Color Pink => pink;
        public Color Pink300 => pink300;
        public Color PinkInk => pinkInk;
        public Color Record => record;
        public Color Danger => danger;
        public Color DangerLine => dangerLine;

        /// <summary>Колір рамки й міток рідкості U1Glaze (rarity-1 … rarity-6).</summary>
        public Color RarityFrame(Rarity rarity)
        {
            var index = (int)rarity;
            return rarityFrames != null && index >= 0 && index < rarityFrames.Length ? rarityFrames[index] : inkText;
        }

        public TMPro.TMP_FontAsset? FontBlack => fontBlack != null ? fontBlack : font;
        public TMPro.TMP_FontAsset? FontBody => fontBody != null ? fontBody : font;
        public Material? FontBlackShadow => fontBlackShadow;
        public float UiDisplay => uiDisplay;
        public float UiH1 => uiH1;
        public float UiH2 => uiH2;
        public float UiH3 => uiH3;
        public float UiButton => uiButton;
        public float UiButtonSmall => uiButtonSmall;
        public float UiBody => uiBody;
        public float UiLabel => uiLabel;
        public float UiCaption => uiCaption;
        public float UiLabelSpacing => uiLabelSpacing;
        public float UiBadgeSpacing => uiBadgeSpacing;
        public Color ButtonTextShadow => buttonTextShadow;
        public float ButtonTextShadowOffsetY => buttonTextShadowOffsetY;
        public float ButtonTextShadowSoftness => buttonTextShadowSoftness;
        public float HeaderHeight => headerHeight;
        public float NavHeight => navHeight;
        public float ButtonHeight => buttonHeight;
        public float ButtonSmallHeight => buttonSmallHeight;
        public float IconButtonSize => iconButtonSize;
        public float TapMin => tapMin;
        public float SideGutter => sideGutter;
        public float CardGap => cardGap;
        public float ListRowHeight => listRowHeight;
        public float NavItemTap => navItemTap;
        public float ScrimOpacity => scrimOpacity;
        public float SheetScrimOpacity => sheetScrimOpacity;
        public float DisabledOpacity => disabledOpacity;
        public float ButtonGlowAlpha => buttonGlowAlpha;
        public float FrameGlowAlpha => frameGlowAlpha;
        public float RarityPipsSmallScale => rarityPipsSmallScale;
        public float DialogAppearDuration => dialogAppearDuration;
        public float DialogAppearScale => dialogAppearScale;
        public float PressDarken => pressDarken;
        public float PressDarkenDuration => pressDarkenDuration;
        public float CosmicSheenPeriod => cosmicSheenPeriod;
        public float PlanetAliveDuration => planetAliveDuration;
        public int SphereMaxSlots => sphereMaxSlots;
        public float SphereAxisTilt => sphereAxisTilt;
        public float SphereWidthShare => sphereWidthShare;
        public float SphereExtent => sphereExtent;
        public float SphereHalo => sphereHalo;
        public float SphereAliveHalo => sphereAliveHalo;
        public float SphereArcInner => sphereArcInner;
        public float SphereArcOuter => sphereArcOuter;
        public float SphereArcAlpha => sphereArcAlpha;
        public float SpherePipRing => spherePipRing;
        public float SpherePipSize => spherePipSize;
        public float SphereAutoSpin => sphereAutoSpin;
        public float SphereSwipeRate => sphereSwipeRate;
        public float SphereSwipeDamping => sphereSwipeDamping;
        public float SphereFlingHold => sphereFlingHold;
        public float SphereTurnDepth => sphereTurnDepth;
        public float SphereTurnDuration => sphereTurnDuration;
        public float SlotSelectedScale => slotSelectedScale;
        public float PlanetHintGap => planetHintGap;
        public float PlanetAliveRingAlpha => planetAliveRingAlpha;
        public float HorizonRadius => horizonRadius;
        public float HorizonSlotGap => horizonSlotGap;
        public float HorizonRowStep => horizonRowStep;
        public float HorizonTopInset => horizonTopInset;
        public float HorizonNearSlot => horizonNearSlot;
        public float HorizonPerspective => horizonPerspective;
        public float HorizonSideInset => horizonSideInset;
        public int HorizonMaxRow => horizonMaxRow;
        public float HorizonDepth => horizonDepth;
        public float HorizonSkyMin => horizonSkyMin;
        public float HorizonBottomGap => horizonBottomGap;
        public float HorizonPipSize => horizonPipSize;
        public float HorizonPipGap => horizonPipGap;
        public float HorizonPipRowMax => horizonPipRowMax;
        public float GalaxyPlanetSide => galaxyPlanetSide;
        public float GalaxyPipRing => galaxyPipRing;
        public float GalaxyPipSize => galaxyPipSize;
        public float GalaxyBadgeSize => galaxyBadgeSize;
        public float GalaxyBadgeOffset => galaxyBadgeOffset;
        public float GalaxyBadgeIcon => galaxyBadgeIcon;
        public float GalaxyLockTile => galaxyLockTile;
        public float GalaxyLockIcon => galaxyLockIcon;
        public float GalaxyFocusFade => galaxyFocusFade;
        public float GalaxyDot => galaxyDot;
        public float GalaxyDotActive => galaxyDotActive;
        public float GalaxyDotGap => galaxyDotGap;
        public float GalaxyRevealHaze => galaxyRevealHaze;
        public float GalaxyArcRadius => galaxyArcRadius;
        public float GalaxyArcPlanet => galaxyArcPlanet;
        public float GalaxyArcSpread => galaxyArcSpread;
        public float GalaxyArcBottomGap => galaxyArcBottomGap;
        public float GalaxyOpenGap => galaxyOpenGap;
        public float GalaxyDotsGapTop => galaxyDotsGapTop;
        public float GalaxyDotsGapBottom => galaxyDotsGapBottom;
        public float GalaxyBadgeRim => galaxyBadgeRim;
        public Color GalaxyDotInactive => galaxyDotInactive;
        public float GalaxyMoonOrbit => galaxyMoonOrbit;

        public static float FromMockupPx(float mockupPx) => mockupPx * MockupToReference;

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
