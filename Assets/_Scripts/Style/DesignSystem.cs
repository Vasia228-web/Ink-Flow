using System;
using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Style
{
    /// <summary>
    /// ЄДИНЕ джерело візуальних значень гри. Витягнуто з макета Claude Design
    /// («Ink Flow v2», полотно 390×844 px) і перераховано в reference-одиниці UGUI
    /// 1080×1920 з коефіцієнтом 1080/390 ≈ 2.769.
    ///
    /// ПРАВИЛО: жодного кольору, радіуса чи тривалості в коді або префабах —
    /// усе читається звідси. Інакше зміна стилю перетворюється на пошук по всьому проєкту.
    /// Табличне дзеркало цих значень — у CLAUDE.md, розділ «Дизайн-система».
    /// </summary>
    [CreateAssetMenu(fileName = "DesignSystem", menuName = "Ink Flow/Design System")]
    public sealed class DesignSystem : ScriptableObject
    {
        /// <summary>Ширина полотна макета в px: hint-size="390,844" у Ink Flow v2.dc.html.</summary>
        public const float MockupWidth = 390f;

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
        public const int CurrentTokenVersion = 7;

        [HideInInspector] [SerializeField] private int tokenVersion = CurrentTokenVersion;

        public int TokenVersion => tokenVersion;

        // ───────────────────────── Палітра чорнила ─────────────────────────

        [Serializable]
        public struct InkSwatch
        {
            public InkColor color;
            public Color hex;
        }

        [Header("Палітра чорнила (5 кольорів макета + 1 екстрапольований)")]
        [Tooltip("Базові кольори крапель. Індекс = InkColor.")]
        [SerializeField]
        private InkSwatch[] inkPalette =
        {
            new InkSwatch { color = InkColor.Magenta, hex = Hex("#FF2D8A") },
            new InkSwatch { color = InkColor.Cyan, hex = Hex("#00D9C0") },
            new InkSwatch { color = InkColor.Amber, hex = Hex("#FFB300") },
            new InkSwatch { color = InkColor.Lime, hex = Hex("#9BE636") },
            new InkSwatch { color = InkColor.Violet, hex = Hex("#9D4DFF") },
            // Макет визначає лише 5 ігрових кольорів. Rose — екстраполяція
            // з рожевих акцентів макета (#FF77B6); підтвердити з дизайном перед
            // рівнями, де colorsCount = 6.
            new InkSwatch { color = InkColor.Rose, hex = Hex("#FF77B6") }
        };

        [Header("Космічний фон (radial 130%×90% з точки 50% / -12%)")]
        [SerializeField] private Color backgroundInner = Hex("#2A1E56");
        [SerializeField] private Color backgroundMid = Hex("#1A1338");
        [SerializeField] private Color backgroundOuter = Hex("#0C0820");
        [SerializeField] private Color backgroundEdge = Hex("#070512");

        [Tooltip("Позиції зупинок градієнта фону (0..1), як у макеті: 0 / .34 / .68 / 1.")]
        [SerializeField] private Vector4 backgroundStops = new Vector4(0f, 0.34f, 0.68f, 1f);

        [Header("Неонові акценти")]
        [SerializeField] private Color accentPrimary = Hex("#FF2D8A");
        [SerializeField] private Color accentSecondary = Hex("#9D4DFF");
        [SerializeField] private Color accentTeal = Hex("#00D9C0");
        [SerializeField] private Color accentBlue = Hex("#3B7BFF");
        [SerializeField] private Color accentGold = Hex("#FFB300");
        [SerializeField] private Color accentLime = Hex("#9BE636");

        [Header("Текст")]
        [SerializeField] private Color textPrimary = Hex("#FFFFFF");
        [SerializeField] private Color textMuted = new Color(1f, 1f, 1f, 0.62f);
        [SerializeField] private Color textDim = new Color(1f, 1f, 1f, 0.38f);

        // ───────────────────────── Скло ─────────────────────────

        [Header("Скляна панель")]
        [Tooltip("Заливка скла: ТЕМНА основа з низькою альфою — космічний фон має просвічувати. " +
                 "Біла заливка макета (.05) читалась як сірий непрозорий пластик, бо в UGUI під нею " +
                 "немає backdrop-blur, який у вебі притемнює фон.")]
        [SerializeField] private Color glassFill = new Color32(9, 6, 22, 108);

        [Tooltip("Підвищена заливка для активних/виділених панелей.")]
        [SerializeField] private Color glassFillRaised = new Color32(16, 11, 34, 150);

        [Tooltip("Тонка світла рамка по контуру: біла з малою альфою.")]
        [SerializeField] private Color glassStroke = new Color(1f, 1f, 1f, 0.14f);

        [Tooltip("Товщина контуру в reference-одиницях: 1-2 px макета ≈ 3-5.")]
        [SerializeField] private float glassStrokeWidth = 4f;

        // ───────────────────────── Радіуси ─────────────────────────

        [Header("Радіуси, reference-одиниці (px макета × 2.769)")]
        [Tooltip("16 px макета — чипи, дрібні плашки.")]
        [SerializeField] private float radiusSmall = 44f;

        [Tooltip("18 px — нижня навігація, кнопки-іконки.")]
        [SerializeField] private float radiusMedium = 50f;

        [Tooltip("20-22 px — кнопки.")]
        [SerializeField] private float radiusButton = 61f;

        [Tooltip("26 px — основні картки/панелі.")]
        [SerializeField] private float radiusCard = 72f;

        [Tooltip("34 px — модальні листи знизу.")]
        [SerializeField] private float radiusSheet = 94f;

        // ───────────────────────── Відступи ─────────────────────────

        [Header("Сітка відступів, reference-одиниці")]
        [SerializeField] private float spacingXs = 11f;   // 4 px
        [SerializeField] private float spacingSm = 17f;   // 6 px
        [SerializeField] private float spacingMd = 28f;   // 10 px
        [SerializeField] private float spacingLg = 39f;   // 14 px
        [SerializeField] private float spacingXl = 50f;   // 18 px — бічні поля екрана

        [Tooltip("Висота нижньої навігації разом із внутрішніми відступами (65 px макета).")]
        [SerializeField] private float navBarHeight = 180f;

        // ───────────────────────── Типографіка ─────────────────────────

        [Header("Типографіка")]
        [Tooltip("Макет намальовано в Baloo 2; у грі — Nunito (Baloo 2 не має кирилиці).")]
        [SerializeField] private TMPro.TMP_FontAsset? font;

        [SerializeField] private float fontSizeCaption = 28f;  // 10 px
        [SerializeField] private float fontSizeLabel = 33f;    // 12 px
        [SerializeField] private float fontSizeBody = 42f;     // 15 px
        [SerializeField] private float fontSizeSubtitle = 47f; // 17 px
        [SerializeField] private float fontSizeTitle = 61f;    // 22 px
        [SerializeField] private float fontSizeDisplay = 75f;  // 27 px

        [Tooltip("Розріджені великі літери (labels): .14em макета.")]
        [SerializeField] private float letterSpacingWide = 14f;

        // ───────────────────────── Світіння ─────────────────────────

        [Header("Кнопка")]
        [Tooltip("Наскільки колір тону підмішується в темну скляну основу кнопки. " +
                 "У макеті всередині картки ледь помітний кольоровий підтон, а не заливка.")]
        [SerializeField, Range(0f, 0.6f)] private float buttonTintStrength = 0.16f;

        [Tooltip("Непрозорість кольорової рамки кнопки (рамка тонка, 1 px макета).")]
        [SerializeField, Range(0f, 1f)] private float buttonStrokeAlpha = 0.75f;

        [Header("Світіння")]
        [Tooltip("Радіус гало кнопки: щільна підсвітка контуру, не ореол.")]
        [SerializeField] private float glowButtonRadius = 30f;

        [Tooltip("Гало назовні — тонке й делікатне: у макеті це підсвітка контуру, а не ореол. " +
                 "Спрайт спільний із карткою і тепер рант, тож площа світіння менша.")]
        [SerializeField, Range(0f, 0.6f)] private float glowButtonAlpha = 0.34f;

        [Tooltip("Спокійний стан near-miss: drop-shadow 0 0 3px.")]
        [SerializeField] private float glowNearMissMin = 8f;

        [Tooltip("Пік near-miss: drop-shadow 0 0 14px.")]
        [SerializeField] private float glowNearMissMax = 39f;

        [Tooltip("Гало навколо великої панелі: 0 0 40px rgba(157,77,255,.18).")]
        [SerializeField] private float glowPanelRadius = 111f;

        [SerializeField, Range(0f, 1f)] private float glowPanelAlpha = 0.18f;

        // ───────────────────────── Крапля ─────────────────────────

        [Header("Крапля")]
        [Tooltip("Освітлення центру відблиску: lighten(colour, .5) у макеті.")]
        [SerializeField, Range(0f, 1f)] private float dropHighlightLighten = 0.5f;

        [Tooltip("Друга зупинка: lighten(colour, .12) на 32%.")]
        [SerializeField, Range(0f, 1f)] private float dropMidLighten = 0.12f;

        [Tooltip("Край краплі: darken(colour, .28) на 100%.")]
        [SerializeField, Range(0f, 1f)] private float dropEdgeDarken = 0.28f;

        [Tooltip("Непрозорість глянцю: rgba(255,255,255,.4) у центрі відблиску.")]
        [SerializeField, Range(0f, 1f)] private float dropGlossAlpha = 0.4f;

        [Tooltip("Тінь під краплею: 0 6px 16px withAlpha(colour, .4).")]
        [SerializeField, Range(0f, 1f)] private float dropShadowAlpha = 0.4f;

        [Tooltip("Розмір гало як частка від розміру краплі. Має лишатись у межах клітинки: " +
                 "на сітці 6×6 завелике гало зливає сусідні краплі в одну пляму.")]
        [SerializeField, Range(1f, 1.6f)] private float dropGlowScale = 1.16f;

        [Tooltip("Пік гало при near-miss. Верхня межа теж обмежена проміжком між клітинками.")]
        [SerializeField, Range(1f, 1.8f)] private float dropNearMissGlowScale = 1.3f;

        [Tooltip("Мінімальний проміжок між краплями в reference-одиницях: гало сусідів " +
                 "не мають торкатись. Клітинка = розмір краплі × NearMissGlowScale + цей проміжок.")]
        [SerializeField, Min(0f)] private float dropMinGap = 18f;

        // ───────────────────────── Рух ─────────────────────────

        [Header("Рух: тривалості, сек")]
        [Tooltip("Погойдування краплі в спокої: 5-6 s ease-in-out, нескінченно.")]
        [SerializeField] private float motionWobbleDuration = 5.5f;

        [Tooltip("Амплітуда погойдування по масштабу: scale(1.02,.98) ↔ (.98,1.03).")]
        [SerializeField] private float motionWobbleScale = 0.03f;

        [Tooltip("Нахил при погойдуванні, градуси: ±2°.")]
        [SerializeField] private float motionWobbleTilt = 2f;

        [Tooltip("Приземлення краплі (squash & stretch): .47 s.")]
        [SerializeField] private float motionLandDuration = 0.47f;

        [Tooltip("Відхилений свайп: .32 s, ±5 px і ±3°.")]
        [SerializeField] private float motionRejectDuration = 0.32f;

        [Tooltip("Поява краплі згори: .55 s.")]
        [SerializeField] private float motionDropInDuration = 0.55f;

        [Tooltip("Пульс near-miss: 1.15 s ease-in-out, нескінченно.")]
        [SerializeField] private float motionNearMissDuration = 1.15f;

        [Tooltip("Натискання кнопки: .28 s back-out.")]
        [SerializeField] private float motionPressDuration = 0.28f;

        [Tooltip("Поява екрана: .35 s.")]
        [SerializeField] private float motionScreenFadeDuration = 0.35f;

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


        // ───────────────────────── Хаб-меню ─────────────────────────

        [Header("Хаб: картки режимів (значення з розмітки макета)")]
        [Tooltip("Радіус картки режиму: 28 px макета.")]
        [SerializeField] private float radiusModeCard = 78f;

        [Tooltip("Перша зупинка градієнта картки. Скло, а не заливка: крізь картку " +
                 "мають просвічувати зорі, тож альфа тримається в межах 0.06-0.10. " +
                 "Колір живе в рамці й гало.")]
        [SerializeField, Range(0f, 0.3f)] private float cardTintStrong = 0.09f;

        [Tooltip("Друга зупинка градієнта — майже прозора.")]
        [SerializeField, Range(0f, 0.3f)] private float cardTintWeak = 0.04f;

        [Tooltip("Темна основа під кольоровим підтоном картки: та сама, що в скла.")]
        [SerializeField] private Color cardBase = new Color32(9, 6, 22, 92);

        [Tooltip("Кольорова рамка картки: rgba(accent,.38) завтовшки 1 px макета.")]
        [SerializeField, Range(0f, 1f)] private float cardStrokeAlpha = 0.38f;

        [Tooltip("Ширина згасання гало НАЗОВНІ від контуру, в reference-одиницях. " +
                 "28 ≈ 10 px макета. Діапазон навмисно вузький (6-14 px): ширше — " +
                 "і гало перестає бути контуром, стає плямою.")]
        [SerializeField, Range(16f, 40f)] private float cardGlowRadius = 28f;

        [Tooltip("Яскравість гало. Спрайт тепер рант, а не заповнений силует, тож " +
                 "площа світіння менша й альфа може бути вищою за стару 0.085.")]
        [SerializeField, Range(0f, 0.5f)] private float cardGlowAlpha = 0.2f;

        [Tooltip("У скільки разів яскравішає гало в момент натискання.")]
        [SerializeField, Range(1f, 4f)] private float cardGlowPressBoost = 2.4f;

        [Tooltip("Плитка іконки в картці: 52×52 px макета.")]
        [SerializeField] private float cardIconTileSize = 144f;

        [Tooltip("Наскільки світлішає акцент для рядка статистики (#FF9ECB від #FF2D8A).")]
        [SerializeField, Range(0f, 1f)] private float statTextLighten = 0.56f;

        [Header("Хаб: нижня навігація")]
        [Tooltip("Заливка капсули навігації — таке саме скло, що й картки: крізь неї " +
                 "видно фон. Значення макета rgba(18,12,34,.55) в UGUI без backdrop-blur " +
                 "читалось як сіра непрозора плашка.")]
        [SerializeField] private Color navFill = new Color32(14, 9, 28, 110);

        [Tooltip("Рамка капсули: rgba(255,255,255,.12).")]
        [SerializeField] private Color navStroke = new Color(1f, 1f, 1f, 0.12f);

        [Tooltip("Радіус капсули навігації: 30 px макета.")]
        [SerializeField] private float radiusNav = 83f;

        [Tooltip("Підпис активної вкладки: rgba(255,255,255,.92), вага 800.")]
        [SerializeField] private Color navLabelActive = new Color(1f, 1f, 1f, 0.92f);

        [Tooltip("Підпис неактивної: rgba(255,255,255,.5), вага 700.")]
        [SerializeField] private Color navLabelInactive = new Color(1f, 1f, 1f, 0.5f);

        [Header("Хаб: типографіка")]
        [Tooltip("Лого «Ink Flow»: 58 px макета, line-height .9.")]
        [SerializeField] private float fontSizeLogo = 161f;

        [Tooltip("Дрібний підпис (11 px макета): підзаголовок профілю, службові рядки.")]
        [SerializeField] private float fontSizeSmall = 30f;

        [Tooltip("Підзаголовок картки (12.5 px макета).")]
        [SerializeField] private float fontSizeCardSubtitle = 35f;

        [Tooltip("Підпис профілю: rgba(255,255,255,.42).")]
        [SerializeField] private Color textFaint = new Color(1f, 1f, 1f, 0.42f);

        [Tooltip("Таглайн під лого: rgba(255,255,255,.4), letter-spacing .18em.")]
        [SerializeField] private float letterSpacingTagline = 18f;

        [Tooltip("Рядок статистики: letter-spacing .02em.")]
        [SerializeField] private float letterSpacingTight = 2f;


        // ───────────────────────── Оживлення ─────────────────────────

        [Header("Крапля: органічна форма")]
        [Tooltip("Асиметрія blob-форми: наскільки осі X/Y розходяться. 0 = ідеальне коло.")]
        [SerializeField, Range(0f, 0.2f)] private float blobAsymmetry = 0.055f;

        [Tooltip("Період повільної деформації форми, сек. Свідомо не кратний періоду " +
                 "дихання — інакше рух читається як механічний.")]
        [SerializeField, Min(0.5f)] private float blobMorphPeriod = 7.3f;

        [Tooltip("На скільки зміщується відблиск при нахилі краплі, частка радіуса.")]
        [SerializeField, Range(0f, 0.3f)] private float glossDrift = 0.07f;

        [Header("Краплі, що стікають")]
        [Tooltip("Середня пауза між краплинами однієї краплі, сек.")]
        [SerializeField, Min(0.5f)] private float dripInterval = 5.5f;

        [Tooltip("Розкид паузи: ±ця частка від інтервалу. Без розкиду краплі капають хором.")]
        [SerializeField, Range(0f, 1f)] private float dripJitter = 0.55f;

        [Tooltip("Скільки летить краплина, сек.")]
        [SerializeField, Min(0.2f)] private float dripFallDuration = 1.15f;

        [Tooltip("Дистанція падіння в reference-одиницях.")]
        [SerializeField] private float dripFallDistance = 110f;

        [Tooltip("Розмір краплини як частка від розміру джерела.")]
        [SerializeField, Range(0.05f, 0.5f)] private float dripSize = 0.22f;

        [Header("Фон: туманність і зорі")]
        // Правило екрана: найяскравіше — лого й картки, фон завжди темніший.
        // Туманності гравець не має помічати свідомо; її робота — прибрати
        // пласкість фону. Якщо її видно як окремий об'єкт, вона завелика або яскрава.

        [Tooltip("Розмір туманності як частка ширини екрана. Понад 2 — ядро ширше " +
                 "за екран, і форма кулі не читається взагалі.")]
        [SerializeField, Range(0.5f, 3f)] private float nebulaScale = 2.2f;

        [Tooltip("Пікова яскравість у центрі купола. Це НЕ середня: у спрайті " +
                 "(1-t²)³ середня альфа = 1/4 пікової, тож сумарного світла тут " +
                 "приблизно вп'ятеро менше за старий суцільний диск на 0.13. " +
                 "Нижче ~0.02 підйом над фоном стає меншим за 2/255 — тобто " +
                 "туманності не видно взагалі. Верхня межа низька навмисно.")]
        [SerializeField, Range(0f, 0.1f)] private float nebulaAlpha = 0.03f;

        [Tooltip("Зсув центра від центра екрана, у частках ширини. Вниз-убік, щоб " +
                 "не сидіти рівно за лого.")]
        [SerializeField] private Vector2 nebulaOffset = new Vector2(-0.26f, -0.4f);

        [Tooltip("Наскільки колір туманності відходить від фонового до акцентного. " +
                 "0 — зливається з фоном, 1 — чистий акцент. Тримати низьким.")]
        [SerializeField, Range(0f, 1f)] private float nebulaTintMix = 0.3f;

        [Tooltip("Період «дихання» туманності, сек. Дуже повільно — це атмосфера, не анімація.")]
        [SerializeField, Min(4f)] private float nebulaBreathPeriod = 17f;

        [SerializeField, Range(0f, 0.4f)] private float nebulaBreathAmount = 0.14f;

        [Tooltip("Яка частка зір мерехтить. Решта світить рівно — так небо виглядає глибшим.")]
        [SerializeField, Range(0f, 1f)] private float starTwinkleFraction = 0.35f;

        [Header("Реакція на дотик")]
        [Tooltip("До якого масштабу стискається елемент під пальцем.")]
        [SerializeField, Range(0.85f, 1f)] private float pressScale = 0.96f;

        [Tooltip("Тривалість стискання, сек.")]
        [SerializeField, Range(0.02f, 0.3f)] private float pressDownDuration = 0.07f;

        [Tooltip("Тривалість пружного повернення (з overshoot).")]
        [SerializeField, Range(0.05f, 0.6f)] private float pressReleaseDuration = 0.26f;

        [Tooltip("Підстрибування іконки вкладки при перемиканні, reference-одиниці.")]
        [SerializeField] private float tabBounceHeight = 14f;

        [SerializeField, Range(0.05f, 0.6f)] private float tabBounceDuration = 0.32f;

        // ───────────────────────── Доступ ─────────────────────────

        public Color BackgroundInner => backgroundInner;
        public Color BackgroundMid => backgroundMid;
        public Color BackgroundOuter => backgroundOuter;
        public Color BackgroundEdge => backgroundEdge;
        public Vector4 BackgroundStops => backgroundStops;

        public Color AccentPrimary => accentPrimary;
        public Color AccentSecondary => accentSecondary;
        public Color AccentTeal => accentTeal;
        public Color AccentBlue => accentBlue;
        public Color AccentGold => accentGold;
        public Color AccentLime => accentLime;

        public Color TextPrimary => textPrimary;
        public Color TextMuted => textMuted;
        public Color TextDim => textDim;

        public Color GlassFill => glassFill;
        public Color GlassFillRaised => glassFillRaised;
        public Color GlassStroke => glassStroke;
        public float GlassStrokeWidth => glassStrokeWidth;

        public float RadiusSmall => radiusSmall;
        public float RadiusMedium => radiusMedium;
        public float RadiusButton => radiusButton;
        public float RadiusCard => radiusCard;
        public float RadiusSheet => radiusSheet;

        public float SpacingXs => spacingXs;
        public float SpacingSm => spacingSm;
        public float SpacingMd => spacingMd;
        public float SpacingLg => spacingLg;
        public float SpacingXl => spacingXl;
        public float NavBarHeight => navBarHeight;

        public TMPro.TMP_FontAsset? Font => font;
        public float FontSizeCaption => fontSizeCaption;
        public float FontSizeLabel => fontSizeLabel;
        public float FontSizeBody => fontSizeBody;
        public float FontSizeSubtitle => fontSizeSubtitle;
        public float FontSizeTitle => fontSizeTitle;
        public float FontSizeDisplay => fontSizeDisplay;
        public float LetterSpacingWide => letterSpacingWide;

        public float GlowButtonRadius => glowButtonRadius;
        public float GlowButtonAlpha => glowButtonAlpha;
        public float GlowNearMissMin => glowNearMissMin;
        public float GlowNearMissMax => glowNearMissMax;
        public float GlowPanelRadius => glowPanelRadius;
        public float GlowPanelAlpha => glowPanelAlpha;

        public float DropHighlightLighten => dropHighlightLighten;
        public float DropMidLighten => dropMidLighten;
        public float DropEdgeDarken => dropEdgeDarken;
        public float DropGlossAlpha => dropGlossAlpha;
        public float DropShadowAlpha => dropShadowAlpha;
        public float DropGlowScale => dropGlowScale;
        public float DropNearMissGlowScale => dropNearMissGlowScale;
        public float DropMinGap => dropMinGap;

        public float ButtonTintStrength => buttonTintStrength;
        public float ButtonStrokeAlpha => buttonStrokeAlpha;

        public float RadiusModeCard => radiusModeCard;
        public float CardTintStrong => cardTintStrong;
        public float CardTintWeak => cardTintWeak;
        public float CardStrokeAlpha => cardStrokeAlpha;
        public float CardGlowRadius => cardGlowRadius;
        public float CardGlowAlpha => cardGlowAlpha;
        public float CardIconTileSize => cardIconTileSize;
        public Color CardBase => cardBase;
        public float CardGlowPressBoost => cardGlowPressBoost;

        public float BlobAsymmetry => blobAsymmetry;
        public float BlobMorphPeriod => blobMorphPeriod;
        public float GlossDrift => glossDrift;

        public float DripInterval => dripInterval;
        public float DripJitter => dripJitter;
        public float DripFallDuration => dripFallDuration;
        public float DripFallDistance => dripFallDistance;
        public float DripSize => dripSize;

        public float NebulaScale => nebulaScale;
        public float NebulaAlpha => nebulaAlpha;
        public Vector2 NebulaOffset => nebulaOffset;

        /// <summary>
        /// Колір туманності будується від фонового, а не від акцентного: так вона
        /// не може стати світлішою за фон, хоч би як крутили nebulaTintMix.
        /// </summary>
        public Color NebulaTint => Color.Lerp(backgroundInner, accentSecondary, nebulaTintMix);
        public float NebulaBreathPeriod => nebulaBreathPeriod;
        public float NebulaBreathAmount => nebulaBreathAmount;
        [Header("Галактика")]
        [Tooltip("Заголовок галактики і підпис зон: 13 px макета.")]
        [SerializeField] private float fontSizeGalaxyTitle = 36f;

        [Tooltip("Назва наступної галактики у прев'ю: 16 px макета.")]
        [SerializeField] private float fontSizeNextGalaxy = 44f;

        [Tooltip("Напис на кнопці «Фарбувати»: 18 px макета.")]
        [SerializeField] private float fontSizePaintButton = 50f;

        [Tooltip("Розрядка заголовка галактики: .1em макета (проти .14em у хабі).")]
        [SerializeField] private float letterSpacingGalaxyTitle = 10f;

        [Tooltip("Заливка круглої скляної кнопки «‹»: rgba(255,255,255,.07) макета. " +
                 "Тут біле скло, а не темне: кнопка лежить на фоні, а не на контенті.")]
        [SerializeField] private Color circleButtonFill = new Color(1f, 1f, 1f, 0.07f);


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

        [Tooltip("Кільце прогресу по орбіті (макет: 1.15).")]
        [SerializeField] private float planetProgressRingScale = 1.15f;

        [SerializeField] private float planetRingWidthScale = 1.9f;
        [SerializeField] private float planetRingHeightScale = 0.6f;

        [SerializeField] private float planetMoonOrbitScale = 0.62f;
        [SerializeField, Min(1f)] private float planetMoonPeriod = 8f;

        [Tooltip("Назва планети внизу екрана: 25 px макета.")]
        [SerializeField] private float fontSizePlanetName = 69f;

        [Tooltip("Крапка пагінації: 7 px макета, активна — 18 завширшки.")]
        [SerializeField] private float paginationDotSize = 19f;
        [SerializeField] private float paginationDotActiveWidth = 50f;
        [SerializeField] private Color paginationDotInactive = new Color(1f, 1f, 1f, 0.28f);

        [Tooltip("Заливка неактивної кнопки: rgba(255,255,255,.06) макета.")]
        [SerializeField] private Color buttonDisabledFill = new Color(1f, 1f, 1f, 0.06f);

        [Tooltip("Палітри поверхонь, СТРОГО в порядку PlanetType. " +
                 "Довжина мусить дорівнювати PlanetTypes.Count.")]
        [SerializeField] private PlanetPalette[] planetPalettes = DefaultPlanetPalettes();

        public float StarTwinkleFraction => starTwinkleFraction;

        public float FontSizeGalaxyTitle => fontSizeGalaxyTitle;
        public float FontSizeNextGalaxy => fontSizeNextGalaxy;
        public float FontSizePaintButton => fontSizePaintButton;
        public float LetterSpacingGalaxyTitle => letterSpacingGalaxyTitle;
        public Color CircleButtonFill => circleButtonFill;
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
        public float PlanetProgressRingScale => planetProgressRingScale;
        public float PlanetRingWidthScale => planetRingWidthScale;
        public float PlanetRingHeightScale => planetRingHeightScale;
        public float PlanetMoonOrbitScale => planetMoonOrbitScale;
        public float PlanetMoonPeriod => planetMoonPeriod;
        public float FontSizePlanetName => fontSizePlanetName;
        public float PaginationDotSize => paginationDotSize;
        public float PaginationDotActiveWidth => paginationDotActiveWidth;
        public Color PaginationDotInactive => paginationDotInactive;
        public Color ButtonDisabledFill => buttonDisabledFill;

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
        public float TabBounceHeight => tabBounceHeight;
        public float TabBounceDuration => tabBounceDuration;

        public Color NavFill => navFill;
        public Color NavStroke => navStroke;
        public float RadiusNav => radiusNav;
        public Color NavLabelActive => navLabelActive;
        public Color NavLabelInactive => navLabelInactive;

        public float FontSizeLogo => fontSizeLogo;
        public float FontSizeSmall => fontSizeSmall;
        public float FontSizeCardSubtitle => fontSizeCardSubtitle;
        public Color TextFaint => textFaint;
        public float LetterSpacingTagline => letterSpacingTagline;
        public float LetterSpacingTight => letterSpacingTight;

        /// <summary>Колір рядка статистики картки — освітлений акцент (#FF2D8A → #FF9ECB).</summary>
        public Color StatText(Color accent) => Lighten(accent, statTextLighten);

        /// <summary>
        /// Крок сітки для краплі заданого розміру: гало сусідів не перетинаються
        /// навіть на піку near-miss. Саме це число має використовувати розкладка поля.
        /// </summary>
        public float CellPitchFor(float dropSize) => dropSize * dropNearMissGlowScale + dropMinGap;

        public float MotionWobbleDuration => motionWobbleDuration;
        public float MotionWobbleScale => motionWobbleScale;
        public float MotionWobbleTilt => motionWobbleTilt;
        public float MotionLandDuration => motionLandDuration;
        public float MotionRejectDuration => motionRejectDuration;
        public float MotionDropInDuration => motionDropInDuration;
        public float MotionNearMissDuration => motionNearMissDuration;
        public float MotionPressDuration => motionPressDuration;
        public float MotionScreenFadeDuration => motionScreenFadeDuration;

        public AnimationCurve CurveBackOut => curveBackOut;
        public AnimationCurve CurveLand => curveLand;
        public AnimationCurve CurveEaseInOut => curveEaseInOut;

        public float StarTwinkleSlow => starTwinkleSlow;
        public float StarTwinkleFast => starTwinkleFast;
        public int StarCount => starCount;

        /// <summary>Базовий колір чорнила за типом. Fallback — білий, щоб помилка була видима.</summary>
        public Color Ink(InkColor color)
        {
            for (var i = 0; i < inkPalette.Length; i++)
                if (inkPalette[i].color == color)
                    return inkPalette[i].hex;
            return Color.white;
        }

        /// <summary>Світла зупинка градієнта краплі (центр відблиску).</summary>
        public Color InkHighlight(InkColor color) => Lighten(Ink(color), dropHighlightLighten);

        /// <summary>Проміжна зупинка градієнта краплі.</summary>
        public Color InkMid(InkColor color) => Lighten(Ink(color), dropMidLighten);

        /// <summary>Темний край краплі.</summary>
        public Color InkEdge(InkColor color) => Darken(Ink(color), dropEdgeDarken);

        /// <summary>Колір тіні/гало під краплею.</summary>
        public Color InkGlow(InkColor color)
        {
            var c = Ink(color);
            c.a = dropShadowAlpha;
            return c;
        }

        /// <summary>Те саме, що lighten() у макеті: змішування з білим.</summary>
        public static Color Lighten(Color c, float amount) => Color.Lerp(c, Color.white, amount);

        /// <summary>Те саме, що darken() у макеті: змішування з чорним.</summary>
        public static Color Darken(Color c, float amount) =>
            Color.Lerp(c, new Color(0f, 0f, 0f, c.a), amount);

        public static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }

        /// <summary>Переводить px макета в reference-одиниці UGUI.</summary>
        public static float FromMockupPx(float mockupPx) => mockupPx * MockupToReference;

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        private void OnValidate()
        {
            // Порядок палітри мусить збігатися з InkColor — інакше Ink() поверне не те,
            // і помилку буде видно лише очима на екрані.
            if (inkPalette.Length < InkColors.MaxCount)
                Debug.LogWarning($"[InkFlow] DesignSystem: у палітрі {inkPalette.Length} кольорів, " +
                                 $"а InkColor має {InkColors.MaxCount}.", this);
        }
    }
}
