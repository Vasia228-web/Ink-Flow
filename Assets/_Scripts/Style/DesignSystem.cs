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
        public const int CurrentTokenVersion = 20;

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

        // ───────────────────────── Кольори картинок ─────────────────────────
        // Кольори фігур, поля й пікселів — це майстер-палітра Core (§3), а не токени
        // дизайн-системи: одна таблиця на гру, картинки й інтерфейс не можуть розійтись.

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
        [SerializeField, Range(0f, 0.3f)] private float cardTintStrong = 0.30f;

        [Tooltip("Друга зупинка градієнта — майже прозора.")]
        [SerializeField, Range(0f, 0.3f)] private float cardTintWeak = 0.16f;

        [Tooltip("Темна основа під кольоровим підтоном картки: та сама, що в скла.")]
        [SerializeField] private Color cardBase = new Color32(9, 6, 22, 92);

        [Tooltip("Прозорість НЕЙТРАЛЬНОГО обведення картки. Акцентного контуру " +
                 "тут немає свідомо: він читався як яскраве кільце навколо картки.")]
        [SerializeField, Range(0f, 1f)] private float cardStrokeAlpha = 0.85f;

        [Tooltip("Ширина згасання гало НАЗОВНІ від краю картки, в reference-одиницях. " +
                 "30 ≈ 11 px макета — саме той діапазон 8-12 px, що в макеті.")]
        [SerializeField, Range(16f, 60f)] private float cardGlowRadius = 30f;

        [Tooltip("Не використовується картками режимів: у них зовнішнього гало немає. " +
                 "Лишається для інших елементів, які беруть card-glow.")]
        [SerializeField, Range(0f, 1f)] private float cardGlowAlpha = 0f;

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
        public float GlowPanelRadius => glowPanelRadius;
        public float GlowPanelAlpha => glowPanelAlpha;

        public float DropHighlightLighten => dropHighlightLighten;
        public float DropMidLighten => dropMidLighten;
        public float DropEdgeDarken => dropEdgeDarken;
        public float DropGlossAlpha => dropGlossAlpha;
        public float DropShadowAlpha => dropShadowAlpha;
        public float DropGlowScale => dropGlowScale;

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

        [Tooltip("Напис на кнопці «Відкрити» в галактиці й «Колекція» на планеті: 18 px макета.")]
        [SerializeField] private float fontSizePaintButton = 50f;

        [Tooltip("Розрядка заголовка галактики: .1em макета (проти .14em у хабі).")]
        [SerializeField] private float letterSpacingGalaxyTitle = 10f;

        [Tooltip("До якої частки розміру назва галактики стискається, щоб влізти в шапку (між стрілками циклів — без розрядки).")]
        [SerializeField, Range(0.4f, 1f)] private float galaxyTitleMinScale = 0.6f;

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

        [Tooltip("Полотно поля: заливка й обведення під сіткою (застаріле — панель тепер K1Candy зі спрайта).")]
        [SerializeField] private Color boardPlateFill = new Color(1f, 1f, 1f, 0.04f);
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
        [Tooltip("Сторона полотна над полем, px макета (було 132 у панелі з назвою). Блок над полем = полотно + " +
                 "запас під найширше гало з кожного боку; на короткому екрані блок стискається першим (поле → лоток → картинка).")]
        [SerializeField, Range(110f, 210f)] private float runPictureSide = 154f;
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
        [Tooltip("Затемнення поверх розмитого знімка і тон самого знімка.")]
        [SerializeField] private Color completionScrim = new Color(0.03f, 0.02f, 0.08f, 0.5f);
        [SerializeField] private Color completionBackdropTint = new Color(0.62f, 0.6f, 0.7f, 1f);
        [Tooltip("Знімок зменшується в стільки разів і розмивається стількома проходами — один раз.")]
        [SerializeField, Range(2, 16)] private int completionBlurDownscale = 8;
        [SerializeField, Range(0, 4)] private int completionBlurPasses = 2;
        [Tooltip("Свайп: скільки одиниць канваса — рішення; нахил картки на одиницю зсуву; тривалості.")]
        [SerializeField, Min(20f)] private float completionSwipeThreshold = 220f;
        [SerializeField, Range(0f, 0.2f)] private float completionTilt = 0.03f;
        [SerializeField, Min(0.05f)] private float completionFlyDuration = 0.42f;
        [SerializeField, Min(0.05f)] private float completionEnterDuration = 0.35f;
        [SerializeField] private float fontSizeCompletionTitle = 59f;
        [SerializeField] private float fontSizeCompletionRarity = 30f;
        [SerializeField] private float fontSizeCompletionHint = 30f;

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

        [Header("HUD партії")]
        [SerializeField] private float fontSizeStatLabel = 27f;
        [SerializeField] private float fontSizeGameTitle = 32f;

        [Tooltip("Капсули статистики: заливка й обведення.")]
        [SerializeField] private Color statCapsuleFill = new Color(1f, 1f, 1f, 0.06f);
        [SerializeField] private Color statCapsuleStroke = new Color(1f, 1f, 1f, 0.11f);

        [Header("Нескінченний")]
        [Tooltip("Числа рахунку й рекорду: 27 px макета.")]
        [SerializeField] private float fontSizeScoreNumber = 52f;

        [Tooltip("Капсули світяться різним: рахунок — бірюзою, рекорд — золотом.")]
        [SerializeField] private Color scoreCapsuleGlow = new Color(0f, 0.851f, 0.753f, 0.18f);
        [SerializeField] private Color recordCapsuleGlow = new Color(1f, 0.702f, 0f, 0.18f);

        [Tooltip("Спалах у момент, коли рахунок перегнав рекорд — не після смерті.")]
        [SerializeField, Min(0.1f)] private float recordFlashDuration = 0.9f;

        [Tooltip("Множник ланцюга спливає по центру екрана: 60 px макета.")]
        [SerializeField] private float fontSizeComboPop = 166f;
        [SerializeField, Min(0.1f)] private float comboPopDuration = 1.1f;
        [SerializeField] private float comboPopRise = 120f;

        [Tooltip("Скільки секунд без ходу — і підказка сама покаже, куди влазить фігура; і скільки її видно.")]
        [SerializeField, Min(1f)] private float hintIdleDelay = 5f;
        [SerializeField, Min(0.2f)] private float hintShowDuration = 1.6f;

        [Tooltip("Картинка над полем: плитка, рамка рідкості, спалах завершення.")]
        [SerializeField] private Color picturePlateFill = new Color(1f, 0.99f, 0.96f, 0.08f);
        [SerializeField] private Color picturePlateStroke = new Color(1f, 1f, 1f, 0.14f);
        [SerializeField, Min(0f)] private float pictureCompleteDuration = 1.1f;
        [SerializeField, Range(0f, 0.3f)] private float pictureCompletePop = 0.06f;
        [SerializeField, Range(0f, 1f)] private float pictureGlowAlpha = 0.55f;
        [SerializeField] private float fontSizePictureName = 27f;
        [SerializeField] private float fontSizePictureCaption = 24f;

        [Tooltip("Рідкість (§6): колір рамки, назви й чипа — звичайна / незвичайна / рідкісна / епічна / легендарна / космічна.")]
        [SerializeField] private Color rarityCommon = Hex("#B8BCC8");
        [SerializeField] private Color rarityUncommon = Hex("#4ED37A");
        [SerializeField] private Color rarityRare = Hex("#5AA7FF");
        [SerializeField] private Color rarityEpic = Hex("#AE7BFF");
        [SerializeField] private Color rarityLegendary = Hex("#FFC145");
        [SerializeField] private Color rarityCosmic = Hex("#F075E6");

        [Tooltip("Картка перед забігом: «цього забігу — така картинка». Скільки висить сама, поки не тапнули.")]
        [SerializeField, Min(0.5f)] private float runIntroDuration = 2.6f;
        [SerializeField, Min(0f)] private float runIntroFadeDuration = 0.25f;
        [SerializeField] private float fontSizeIntroKicker = 27f;
        [SerializeField] private float fontSizeIntroName = 59f;
        [SerializeField] private float fontSizeIntroRarity = 30f;
        [SerializeField] private float fontSizeIntroHint = 30f;
        [SerializeField] private float fontSizeOverCollected = 27f;

        [Header("Кінець партії (Нескінченний)")]
        [SerializeField] private float fontSizeOverScore = 155f;
        [SerializeField] private float fontSizeOverLabel = 30f;
        [SerializeField] private float fontSizeRecordChip = 36f;
        [SerializeField] private float fontSizeRewardNumber = 47f;
        [SerializeField] private float fontSizeOverBest = 37f;
        [SerializeField] private float fontSizeOverPrimary = 53f;
        [SerializeField] private float fontSizeOverSecondary = 39f;
        [SerializeField] private Color overCardFrom = new Color(0.180f, 0.102f, 0.290f, 0.96f);
        [SerializeField] private Color overCardTo = new Color(0.094f, 0.051f, 0.165f, 0.96f);
        [SerializeField] private Color overScrim = new Color(0.047f, 0.020f, 0.094f, 0.78f);
        [SerializeField] private Color recordChipFrom = Hex("#FFD54A");
        [SerializeField] private Color recordChipTo = Hex("#FFB300");
        [SerializeField] private Color recordChipText = Hex("#2A1A02");

        [Tooltip("Нагорода набігає цифрами, а не з'являється готовою.")]
        [SerializeField, Min(0.1f)] private float rewardCountDuration = 0.9f;

        [Tooltip("Скільки летить конфеті за новий рекорд.")]
        [SerializeField, Min(0.2f)] private float confettiFallDuration = 2.2f;

        [Header("Карта рівнів")]
        [Tooltip("Номер на вузлі: 19 px макета, на поточному — 24.")]
        [SerializeField] private float fontSizeLevelNode = 53f;
        [SerializeField] private float fontSizeLevelNodeCurrent = 66f;

        [Tooltip("Клякс — не колір палітри, а майже чорна куля з фіолетовим нутром.")]
        [SerializeField] private Color bossNodeFrom = Hex("#3B2258");
        [SerializeField] private Color bossNodeTo = Hex("#080312");
        [SerializeField] private Color bossCaption = Hex("#C9A2FF");

        [SerializeField] private Color lockedNodeFrom = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color lockedNodeTo = new Color(1f, 1f, 1f, 0.02f);
        [SerializeField] private Color lockedNodeStroke = new Color(1f, 1f, 1f, 0.09f);

        [Tooltip("Світиться лише поточний вузол і бос — решта не сперечається з ними.")]
        [SerializeField, Range(0f, 1f)] private float levelNodeGlowAlpha = 0.5f;

        [Tooltip("Відступ підпису під вузлом: 5 px макета.")]
        [SerializeField] private float levelCaptionOffset = 14f;

        [Tooltip("Порожня зірочка під вузлом.")]
        [SerializeField] private Color starPipEmpty = new Color(1f, 1f, 1f, 0.2f);

        [Tooltip("Слід: пройдена ділянка, замкнена, і чорнильне згущення перед босом.")]
        [SerializeField] private Color trailPassed = new Color(0.616f, 0.302f, 1f, 0.5f);
        [SerializeField] private Color trailLocked = new Color(1f, 1f, 1f, 0.13f);
        [SerializeField] private Color trailInk = new Color(0.149f, 0.047f, 0.251f, 0.55f);
        [SerializeField] private Color trailInkDeep = new Color(0.055f, 0.016f, 0.094f, 0.97f);

        [SerializeField] private Color trailBonus = new Color(1f, 0.835f, 0.29f, 0.4f);
        [SerializeField] private float trailBonusWidth = 17f;

        [Tooltip("Де стоїть поточний рівень при автоскролі: 0.5 — центр, " +
                 "0.56 — трохи вище, щоб було видно більше шляху попереду.")]
        [SerializeField, Range(0.3f, 0.8f)] private float levelMapFocus = 0.56f;

        [Tooltip("Відмова при тапі на замкнений вузол.")]
        [SerializeField, Min(0.05f)] private float levelShakeDuration = 0.32f;
        [SerializeField] private float levelShakeAmplitude = 14f;

        [Tooltip("Картка боса темніша за звичайну.")]
        [SerializeField] private Color bossSheetFrom = new Color(0.114f, 0.055f, 0.176f, 0.96f);
        [SerializeField] private Color bossSheetTo = new Color(0.055f, 0.024f, 0.094f, 0.96f);

        [Header("Профіль")]
        [Tooltip("Нік на візитці: 30 px макета.")]
        [SerializeField] private float fontSizeProfileNick = 83f;

        [Tooltip("Число на плитці статистики: 27 px макета.")]
        [SerializeField] private float fontSizeProfileStat = 75f;

        [Tooltip("Кружок пройденого звання.")]
        [SerializeField] private Color rankNodeAchieved = new Color(1f, 1f, 1f, 0.08f);

        [Tooltip("Кружок ще не відкритого звання — темний, майже фон.")]
        [SerializeField] private Color rankNodeLocked = new Color(0.04f, 0.024f, 0.086f, 0.9f);

        [SerializeField] private Color rankNodeStrokeDim = new Color(1f, 1f, 1f, 0.08f);

        [Tooltip("Світіння поточного звання — головний акцент блоку.")]
        [SerializeField, Range(0f, 1f)] private float rankGlowAlpha = 0.5f;

        [SerializeField, Range(0f, 1f)] private float rankCapsuleGlowAlpha = 0.45f;

        [Tooltip("Заливка неотриманого бейджа досягнення.")]
        [SerializeField] private Color badgeLockedFill = new Color(1f, 1f, 1f, 0.05f);

        [SerializeField, Range(0f, 1f)] private float badgeGlowAlpha = 0.35f;

        [Tooltip("Секунд на оберт планети у вітрині. Повільно — вона не для дії.")]
        [SerializeField, Min(1f)] private float showcaseSpin = 40f;

        [Tooltip("Скільки висить підказка про умову досягнення.")]
        [SerializeField, Min(0.5f)] private float toastDuration = 2.2f;

        [Header("Рейтинги")]
        [Tooltip("Нік і номер у рядку: 15 px макета.")]
        [SerializeField] private float fontSizeRankRow = 42f;

        [Tooltip("Число метрики в рядку: 18 px макета.")]
        [SerializeField] private float fontSizeRankValue = 50f;

        [Tooltip("Число першого місця на подіумі: 26 px макета.")]
        [SerializeField] private float fontSizePodiumFirst = 72f;

        [Tooltip("Число другого й третього місця: 20 px макета.")]
        [SerializeField] private float fontSizePodiumOther = 55f;

        [Tooltip("Медалі: золото, срібло, бронза. Порядок = місце.")]
        [SerializeField] private Color[] medals =
        {
            Hex("#FFD54A"), Hex("#D7DEE8"), Hex("#E0975A")
        };

        [Tooltip("Цифра на бейджі місця — темна: на золоті білий не читається.")]
        [SerializeField] private Color medalText = Hex("#231A08");

        [SerializeField, Range(0f, 1f)] private float podiumGlowAlpha = 0.55f;

        [Tooltip("Секунд на оберт: перше місце помітно живіше за решту.")]
        [SerializeField] private float podiumSpinFirst = 20f;
        [SerializeField] private float podiumSpinOther = 26f;

        [Tooltip("Планета й аватар схованого профілю.")]
        [SerializeField] private Color rankIncognito = Hex("#5C6274");

        [Tooltip("Рядок списку: 56 px макета, проміжок 8.")]
        [SerializeField] private float rankRowHeight = 155f;
        [SerializeField] private float rankRowGap = 22f;

        [Tooltip("Запас унизу списку під закріплену картку «Ти».")]
        [SerializeField] private float rankListBottomPadding = 321f;

        [Tooltip("Підтон картки «Ти» — маджента й фіолет із макета.")]
        [SerializeField, Range(0f, 0.6f)] private float youCardTintFrom = 0.16f;
        [SerializeField, Range(0f, 0.6f)] private float youCardTintTo = 0.14f;
        [SerializeField] private Color youCardStroke = new Color(1f, 0.353f, 0.667f, 0.7f);
        [SerializeField, Range(0f, 1f)] private float youCardGlowAlpha = 0.4f;
        [SerializeField] private Color youCardText = Hex("#FFD0E6");

        [Header("Магазин")]
        [Tooltip("Назва фарби й напис на кнопці картки: 13.5 px макета.")]
        [SerializeField] private float fontSizeShopCard = 37f;

        [Tooltip("Ціна на картці: 14.5 px макета.")]
        [SerializeField] private float fontSizeShopPrice = 40f;

        [Tooltip("Розрядка «МАГАЗИН»: .18em макета.")]
        [SerializeField] private float letterSpacingShopTitle = 18f;

        [Tooltip("Найтьмяніший текст — «порожньо» на картці фарби.")]
        [SerializeField] private Color textFaintest = new Color(1f, 1f, 1f, 0.3f);

        [Tooltip("Ціна, коли нафти не вистачає. М'яко-червона, не тривожна.")]
        [SerializeField] private Color shopUnaffordablePrice = Hex("#FF7A97");

        [Tooltip("Текст на світлій заливці: на «Персику» чи «М'яті» білий зникає.")]
        [SerializeField] private Color shopOnLightText = Hex("#1C0F33");

        [SerializeField, Range(0f, 0.6f)] private float shopSpecialGlowAlpha = 0.19f;
        [SerializeField, Range(0f, 0.6f)] private float shopHotGlowAlpha = 0.22f;

        [SerializeField] private Color shopTabActiveFrom = new Color(1f, 0.176f, 0.541f, 0.92f);
        [SerializeField] private Color shopTabActiveTo = new Color(0.616f, 0.302f, 1f, 0.9f);

        [Tooltip("Підтон банера «Фарба тижня» в колір самої фарби.")]
        [SerializeField, Range(0f, 0.5f)] private float shopWeeklyTintFrom = 0.16f;
        [SerializeField, Range(0f, 0.5f)] private float shopWeeklyTintTo = 0.1f;
        [SerializeField, Range(0f, 0.6f)] private float shopWeeklyGlowAlpha = 0.26f;

        [Tooltip("Відступ заливки від стінок мензурки: 1 px макета.")]
        [SerializeField] private float shopBeakerInset = 3f;

        [Header("Фарбування планети")]
        [Tooltip("Назва планети в шапці: 15 px макета, ls .04em.")]
        [SerializeField] private float fontSizePaintTitle = 42f;
        [SerializeField] private float letterSpacingPaintTitle = 4f;

        [Tooltip("«{Планета} завершена!»: 26 px макета.")]
        [SerializeField] private float fontSizeCompletion = 72f;

        [Tooltip("Розмір зони відносно радіуса планети. У макеті R = 132 і " +
                 "size = r · 1.72, звідси 1.72/132.")]
        [SerializeField] private float paintZoneSizeFactor = 0.01303f;

        [Tooltip("Наскільки зони не доходять до лімба (макет: 0.94). Ближче до 1 — " +
                 "і зона злизується з краю кулі, читаючись як приклеєна ззовні.")]
        [SerializeField, Range(0.7f, 1f)] private float paintZoneInset = 0.94f;

        [Tooltip("Градусів обертання на одиницю руху пальця.")]
        [SerializeField] private float paintRotationPerUnit = 0.25f;

        [Tooltip("Згасання вибігу після відпускання. Більше — різкіше спиняється.")]
        [SerializeField, Min(0.5f)] private float paintRotationDamping = 4f;

        [Tooltip("Швидкість автообертання після завершення планети, градусів/с.")]
        [SerializeField] private float paintAutoSpinSpeed = 12f;

        [Tooltip("Радіус слота в px макета для планети з найменшою кількістю слотів і з найбільшою; " +
                 "між ними — лінійно за кількістю. На планеті з 12 слотами вони мусять бути меншими, щоб не накладались.")]
        [SerializeField, Range(12f, 60f)] private float slotRadiusFewest = 40f;
        [SerializeField, Range(12f, 60f)] private float slotRadiusMost = 24f;
        [Tooltip("Кількості слотів, між якими інтерполюється радіус (дефолтна розкладка: 4 … 12).")]
        [SerializeField, Min(1)] private int slotCountFewest = 4;
        [SerializeField, Min(1)] private int slotCountMost = 12;
        [Tooltip("Панель і рамка картинки в слоті відносно діаметра слота.")]
        [SerializeField, Range(0.4f, 1f)] private float slotPictureScale = 0.82f;
        [Tooltip("Піксель-арт відносно панелі слота: поле між пікселями й рамкою.")]
        [SerializeField, Range(0.4f, 1f)] private float slotPixelsScale = 0.82f;
        [Tooltip("Прозорість порожнього слота (сіра пляма з пунктиром).")]
        [SerializeField, Range(0.1f, 1f)] private float slotEmptyAlpha = 0.85f;
        [Tooltip("Зерно форми порожньої плями (_Seed шейдера InkFlow/Zone): усі гнізда однакові, це не материки.")]
        [SerializeField, Range(0f, 1f)] private float slotSocketSeed = 0.37f;
        [Tooltip("Спалах «планета ожила» відносно діаметра планети.")]
        [SerializeField, Range(1f, 2f)] private float planetFlashScale = 1.4f;
        [Tooltip("Колір супутника, що влітає на орбіту ожилої планети.")]
        [SerializeField] private Color planetMoonColor = new Color(0.94f, 0.95f, 1f, 1f);

        [SerializeField] private Color paintLowFill = new Color(1f, 0.42f, 0.54f, 0.16f);
        [SerializeField] private Color paintLowStroke = new Color(1f, 0.42f, 0.54f, 0.45f);
        [SerializeField] private Color paintLowText = Hex("#FF9FB2");

        [Tooltip("Наближення до планети з екрана огляду.")]
        [SerializeField, Min(0.1f)] private float paintApproachDuration = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float paintApproachFromScale = 0.35f;

        [SerializeField, Min(0.1f)] private float paintFlashDuration = 0.7f;
        [SerializeField, Min(0.1f)] private float paintMoonFlyDuration = 1f;

        [SerializeField, Min(0)] private int paintConfettiCount = 12;
        [SerializeField] private float paintConfettiSize = 60f;
        [SerializeField] private float paintConfettiSpread = 160f;

        [Header("Колекція (§12)")]
        [Tooltip("Колонок у сітці колекції.")]
        [SerializeField, Range(2, 4)] private int collectionColumns = 3;
        [Tooltip("Картка колекції: ширина й висота (панель + підпис), px макета 104 × 128.")]
        [SerializeField] private float collectionCardWidth = 288f;
        [SerializeField] private float collectionCardHeight = 354f;
        [Tooltip("Проміжок між картками: 11 px макета.")]
        [SerializeField] private float collectionGap = 30f;
        [Tooltip("Прозорість картки, усі копії якої вже стоять у слотах (режим вибору).")]
        [SerializeField, Range(0.1f, 1f)] private float collectionUsedAlpha = 0.35f;
        [SerializeField] private float fontSizeCollectionName = 30f;
        [SerializeField] private float fontSizeCollectionCount = 27f;

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
        public Color BoardPlateFill => boardPlateFill;
        public Color BoardPlateStroke => boardPlateStroke;
        public float TrayDraggingAlpha => trayDraggingAlpha;

        public float BoardPanelRadius => boardPanelRadius;
        public float TraySlotRadius => traySlotRadius;
        public Color PanelShadow => panelShadow;
        public float PanelShadowOffset => panelShadowOffset;
        public float PanelShadowBlur => panelShadowBlur;
        public float BlockFraction => blockFraction;
        public float BlockTintBoost => blockTintBoost;
        public float BlockGlowAlpha => blockGlowAlpha;
        public float BlockGlowScale => blockGlowScale;
        public float BlockHighlightAlpha => blockHighlightAlpha;
        public float TrayPieceScale => trayPieceScale;
        public float BoardSideMargin => boardSideMargin;
        public float BoardPadding => boardPadding;
        public float BoardGap => boardGap;

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
        public float RunPictureSide => runPictureSide;

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
        public Color CompletionScrim => completionScrim;
        public Color CompletionBackdropTint => completionBackdropTint;
        public int CompletionBlurDownscale => completionBlurDownscale;
        public int CompletionBlurPasses => completionBlurPasses;
        public float CompletionSwipeThreshold => completionSwipeThreshold;
        public float CompletionTilt => completionTilt;
        public float CompletionFlyDuration => completionFlyDuration;
        public float CompletionEnterDuration => completionEnterDuration;
        public float FontSizeCompletionTitle => fontSizeCompletionTitle;
        public float FontSizeCompletionRarity => fontSizeCompletionRarity;
        public float FontSizeCompletionHint => fontSizeCompletionHint;
        public float EpicGlowAlpha => epicGlowAlpha;
        public float LegendarySweepPeriod => legendarySweepPeriod;
        public float CosmicHueSpeed => cosmicHueSpeed;
        public float CosmicParticleAlpha => cosmicParticleAlpha;
        public float CosmicParticleSpeed => cosmicParticleSpeed;
        public float FontSizeLineFloatPure => fontSizeLineFloatPure;
        public float FontSizeLineFloatMixed => fontSizeLineFloatMixed;
        public float LineFloatDuration => lineFloatDuration;
        public float LineFloatRise => lineFloatRise;
        public float FontSizeStatLabel => fontSizeStatLabel;
        public float FontSizeGameTitle => fontSizeGameTitle;
        public Color StatCapsuleFill => statCapsuleFill;
        public Color StatCapsuleStroke => statCapsuleStroke;
        public float FontSizeScoreNumber => fontSizeScoreNumber;
        public Color ScoreCapsuleGlow => scoreCapsuleGlow;
        public Color RecordCapsuleGlow => recordCapsuleGlow;
        public float RecordFlashDuration => recordFlashDuration;
        public float FontSizeComboPop => fontSizeComboPop;
        public float ComboPopDuration => comboPopDuration;
        public float ComboPopRise => comboPopRise;
        public float HintIdleDelay => hintIdleDelay;
        public float HintShowDuration => hintShowDuration;
        public Color PicturePlateFill => picturePlateFill;
        public Color PicturePlateStroke => picturePlateStroke;
        public float PictureCompleteDuration => pictureCompleteDuration;
        public float PictureCompletePop => pictureCompletePop;
        public float PictureGlowAlpha => pictureGlowAlpha;
        public float FontSizePictureName => fontSizePictureName;
        public float FontSizePictureCaption => fontSizePictureCaption;
        public float RunIntroDuration => runIntroDuration;
        public float RunIntroFadeDuration => runIntroFadeDuration;
        public float FontSizeIntroKicker => fontSizeIntroKicker;
        public float FontSizeIntroName => fontSizeIntroName;
        public float FontSizeIntroRarity => fontSizeIntroRarity;
        public float FontSizeIntroHint => fontSizeIntroHint;
        public float FontSizeOverCollected => fontSizeOverCollected;

        /// <summary>Колір рідкості (§6): рамка картинки, назва, чип на картці.</summary>
        public Color RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => rarityUncommon,
            Rarity.Rare => rarityRare,
            Rarity.Epic => rarityEpic,
            Rarity.Legendary => rarityLegendary,
            Rarity.Cosmic => rarityCosmic,
            _ => rarityCommon
        };

        /// <summary>Колір індексу майстер-палітри (§3) — фігури, клітинки поля, пікселі, краплі.</summary>
        public static Color PaletteColor(byte index) => MasterPalette.ColorOf(index).ToColor();
        public float FontSizeOverScore => fontSizeOverScore;
        public float FontSizeOverLabel => fontSizeOverLabel;
        public float FontSizeRecordChip => fontSizeRecordChip;
        public float FontSizeRewardNumber => fontSizeRewardNumber;
        public float FontSizeOverBest => fontSizeOverBest;
        public float FontSizeOverPrimary => fontSizeOverPrimary;
        public float FontSizeOverSecondary => fontSizeOverSecondary;
        public Color OverCardFrom => overCardFrom;
        public Color OverCardTo => overCardTo;
        public Color OverScrim => overScrim;
        public Color RecordChipFrom => recordChipFrom;
        public Color RecordChipTo => recordChipTo;
        public Color RecordChipText => recordChipText;
        public float RewardCountDuration => rewardCountDuration;
        public float ConfettiFallDuration => confettiFallDuration;
        public float FontSizeLevelNode => fontSizeLevelNode;
        public float FontSizeLevelNodeCurrent => fontSizeLevelNodeCurrent;
        public Color BossNodeFrom => bossNodeFrom;
        public Color BossNodeTo => bossNodeTo;
        public Color BossCaption => bossCaption;
        public Color LockedNodeFrom => lockedNodeFrom;
        public Color LockedNodeTo => lockedNodeTo;
        public Color LockedNodeStroke => lockedNodeStroke;
        public float LevelNodeGlowAlpha => levelNodeGlowAlpha;
        public float LevelCaptionOffset => levelCaptionOffset;
        public Color StarPipEmpty => starPipEmpty;
        public Color TrailPassed => trailPassed;
        public Color TrailLocked => trailLocked;
        public Color TrailInk => trailInk;
        public Color TrailInkDeep => trailInkDeep;
        public Color TrailBonus => trailBonus;
        public float TrailBonusWidth => trailBonusWidth;
        public float LevelMapFocus => levelMapFocus;
        public float LevelShakeDuration => levelShakeDuration;
        public float LevelShakeAmplitude => levelShakeAmplitude;
        public Color BossSheetFrom => bossSheetFrom;
        public Color BossSheetTo => bossSheetTo;
        public float FontSizeProfileNick => fontSizeProfileNick;
        public float FontSizeProfileStat => fontSizeProfileStat;
        public Color RankNodeAchieved => rankNodeAchieved;
        public Color RankNodeLocked => rankNodeLocked;
        public Color RankNodeStrokeDim => rankNodeStrokeDim;
        public float RankGlowAlpha => rankGlowAlpha;
        public float RankCapsuleGlowAlpha => rankCapsuleGlowAlpha;
        public Color BadgeLockedFill => badgeLockedFill;
        public float BadgeGlowAlpha => badgeGlowAlpha;
        public float ShowcaseSpin => showcaseSpin;
        public float ToastDuration => toastDuration;
        public float FontSizeRankRow => fontSizeRankRow;
        public float FontSizeRankValue => fontSizeRankValue;
        public float FontSizePodiumFirst => fontSizePodiumFirst;
        public float FontSizePodiumOther => fontSizePodiumOther;
        public Color MedalText => medalText;
        public float PodiumGlowAlpha => podiumGlowAlpha;
        public float PodiumSpinFirst => podiumSpinFirst;
        public float PodiumSpinOther => podiumSpinOther;
        public Color RankIncognito => rankIncognito;
        public float RankRowHeight => rankRowHeight;
        public float RankRowGap => rankRowGap;
        public float RankListBottomPadding => rankListBottomPadding;
        public float YouCardTintFrom => youCardTintFrom;
        public float YouCardTintTo => youCardTintTo;
        public Color YouCardStroke => youCardStroke;
        public float YouCardGlowAlpha => youCardGlowAlpha;
        public Color YouCardText => youCardText;

        /// <summary>Колір медалі за місцем (1..3). Поза межами — бронза.</summary>
        public Color Medal(int place)
        {
            var i = place - 1;
            if (medals == null || medals.Length == 0)
                return Color.white;
            return medals[i >= 0 && i < medals.Length ? i : medals.Length - 1];
        }

        public float FontSizeShopCard => fontSizeShopCard;
        public float FontSizeShopPrice => fontSizeShopPrice;
        public float LetterSpacingShopTitle => letterSpacingShopTitle;
        public Color TextFaintest => textFaintest;
        public Color ShopUnaffordablePrice => shopUnaffordablePrice;
        public Color ShopOnLightText => shopOnLightText;
        public float ShopSpecialGlowAlpha => shopSpecialGlowAlpha;
        public float ShopHotGlowAlpha => shopHotGlowAlpha;
        public Color ShopTabActiveFrom => shopTabActiveFrom;
        public Color ShopTabActiveTo => shopTabActiveTo;
        public float ShopWeeklyTintFrom => shopWeeklyTintFrom;
        public float ShopWeeklyTintTo => shopWeeklyTintTo;
        public float ShopWeeklyGlowAlpha => shopWeeklyGlowAlpha;
        public float ShopBeakerInset => shopBeakerInset;
        public float FontSizePaintTitle => fontSizePaintTitle;
        public float LetterSpacingPaintTitle => letterSpacingPaintTitle;
        public float FontSizeCompletion => fontSizeCompletion;
        public float PaintZoneSizeFactor => paintZoneSizeFactor;
        public float PaintZoneInset => paintZoneInset;
        public float PaintRotationPerUnit => paintRotationPerUnit;
        public float PaintRotationDamping => paintRotationDamping;
        public float PaintAutoSpinSpeed => paintAutoSpinSpeed;
        public float SlotPictureScale => slotPictureScale;
        public float SlotPixelsScale => slotPixelsScale;
        public float SlotEmptyAlpha => slotEmptyAlpha;
        public float SlotSocketSeed => slotSocketSeed;
        public float PlanetFlashScale => planetFlashScale;
        public Color PlanetMoonColor => planetMoonColor;

        /// <summary>Радіус слота (px макета) для планети з такою кількістю слотів: лінійно між двома токенами.</summary>
        public float SlotRadiusFor(int slotCount)
        {
            if (slotCountMost <= slotCountFewest)
                return slotRadiusFewest;
            var t = Mathf.InverseLerp(slotCountFewest, slotCountMost, slotCount);
            return Mathf.Lerp(slotRadiusFewest, slotRadiusMost, t);
        }

        public int CollectionColumns => collectionColumns;
        public float CollectionCardWidth => collectionCardWidth;
        public float CollectionCardHeight => collectionCardHeight;
        public float CollectionGap => collectionGap;
        public float CollectionUsedAlpha => collectionUsedAlpha;
        public float FontSizeCollectionName => fontSizeCollectionName;
        public float FontSizeCollectionCount => fontSizeCollectionCount;
        public Color PaintLowFill => paintLowFill;
        public Color PaintLowStroke => paintLowStroke;
        public Color PaintLowText => paintLowText;
        public float PaintApproachDuration => paintApproachDuration;
        public float PaintApproachFromScale => paintApproachFromScale;
        public float PaintFlashDuration => paintFlashDuration;
        public float PaintMoonFlyDuration => paintMoonFlyDuration;
        public int PaintConfettiCount => paintConfettiCount;
        public float PaintConfettiSize => paintConfettiSize;
        public float PaintConfettiSpread => paintConfettiSpread;

        public float FontSizeGalaxyTitle => fontSizeGalaxyTitle;
        public float FontSizeNextGalaxy => fontSizeNextGalaxy;
        public float FontSizePaintButton => fontSizePaintButton;
        public float LetterSpacingGalaxyTitle => letterSpacingGalaxyTitle;
        public float GalaxyTitleMinScale => galaxyTitleMinScale;
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

        public float MotionWobbleDuration => motionWobbleDuration;
        public float MotionWobbleScale => motionWobbleScale;
        public float MotionWobbleTilt => motionWobbleTilt;
        public float MotionLandDuration => motionLandDuration;
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
