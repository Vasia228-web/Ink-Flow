using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Розкладка екрана забігу (документ §11, U1Glaze S02Run_01) для будь-якого екрана: шапка 56, під нею
    /// ліворуч картинка (полотно W4 із запасом під гало рідкості), праворуч дві панелі РАХУНОК / РЕКОРД
    /// заввишки з полотно, далі поле — квадрат зі стороною min(ширина, висота, що лишилась між картинкою
    /// й лотком), лоток завжди при нижньому краю safe area. Жоден елемент не виходить за екран — це тримає
    /// тест на розмірах реальних пристроїв, а не око.
    ///
    /// Усі числа — в px макета (402 завширшки, як полотно U1Glaze); в'ю множить на свій коефіцієнт.
    /// Коли екрану бракує висоти, стискається спершу полотно картинки (до <see cref="CanvasMinSide"/>),
    /// і лише потім поле; лоток — ніколи (пріоритет «поле → лоток → картинка», уточнення автора п.4).
    ///
    /// Бічне поле панелі поля й лотка (<see cref="BoardMargin"/>) — токен дизайн-системи (за замовчуванням
    /// <see cref="BoardGeometry.DefaultSideMargin"/>); шапка, картинка й панелі рахунку тримають бічне поле
    /// <see cref="SideMargin"/>. Гало рідкості виходить за полотно на <see cref="HaloReserve"/> з кожного боку:
    /// запас під нього — частина блоку картинки, тож світіння не лізе ні на поле, ні на панелі рахунку.
    /// </summary>
    public readonly struct RunLayout
    {
        /// <summary>Бічне поле шапки, картинки й панелей рахунку (side-gutter U1Glaze).</summary>
        public const float SideMargin = 16f;

        /// <summary>Висота шапки «‹ · НЕСКІНЧЕННИЙ · шестерня» (header-h U1Glaze).</summary>
        public const float HeaderHeight = 56f;

        /// <summary>Полотно картинки за замовчуванням — вікно картки S02Run_01 (токен дизайн-системи).</summary>
        public const float CanvasMaxSide = 214f;

        /// <summary>Нижче цього панелям рахунку бракує висоти на підпис і число.</summary>
        public const float CanvasMinSide = 116f;

        /// <summary>Запас під гало рідкості за замовчуванням (найширше гало, токен).</summary>
        public const float DefaultHaloReserve = 16f;

        public const float TrayGap = 10f;
        public const float TrayHeight = 86f;
        public const float BoardMinSide = 200f;

        /// <summary>Найменший проміжок між полотном і панелями рахунку; більший, якщо гало ширше.</summary>
        public const float StatsMinGap = 12f;

        /// <summary>Проміжок між панелями РАХУНОК і РЕКОРД.</summary>
        public const float StatsRowGap = 10f;

        /// <summary>Найвужча колонка рахунку: «999 999 999» у ній ще вміщається (ScoreFormat).</summary>
        public const float StatsMinWidth = 110f;

        /// <summary>Поля всередині панелі рахунку ліворуч і праворуч: число вміщається в ширину панелі мінус них.</summary>
        public const float StatPadLeft = 14f;
        public const float StatPadRight = 10f;

        private RunLayout(float width, float height, float boardMargin, float haloReserve, float canvasCap, float canvasSide,
            float boardSide, float trayTop)
        {
            Width = width;
            Height = height;
            BoardMargin = boardMargin;
            HaloReserve = haloReserve;
            CanvasCap = canvasCap;
            CanvasSide = canvasSide;
            BoardSide = boardSide;
            TrayTop = trayTop;
        }

        public float Width { get; }
        public float Height { get; }

        /// <summary>Бічне поле панелі поля й лотка від краю екрана.</summary>
        public float BoardMargin { get; }

        /// <summary>Запас під гало рідкості з кожного боку полотна.</summary>
        public float HaloReserve { get; }

        /// <summary>Найбільше полотно, яке дозволяють токен і ширина екрана (без стискання по висоті).</summary>
        public float CanvasCap { get; }

        /// <summary>Сторона полотна картинки.</summary>
        public float CanvasSide { get; }

        /// <summary>Масштаб полотна відносно найбільшого (1 — не стиснуте).</summary>
        public float PictureScale => CanvasCap > 0f ? CanvasSide / CanvasCap : 1f;

        /// <summary>Блок картинки: полотно плюс гало з кожного боку — квадрат від шапки.</summary>
        public float PictureTop => HeaderHeight;
        public float PictureLeft => CanvasLeft - HaloReserve;
        public float PictureBlock => CanvasSide + HaloReserve * 2f;

        /// <summary>
        /// Ліва й верхня межа самого полотна (без гало): врівень із «‹» (бічне поле), а якщо гало ширше за бічне
        /// поле — відсунуте, щоб світіння не виходило за край екрана.
        /// </summary>
        public float CanvasLeft => Math.Max(SideMargin, HaloReserve);
        public float CanvasTop => HeaderHeight + HaloReserve;

        /// <summary>Проміжок між полотном і панелями рахунку: гало туди не дістає.</summary>
        public float StatsGap => Math.Max(StatsMinGap, HaloReserve);

        /// <summary>Колонка панелей рахунку: від полотна до правого бічного поля, заввишки з полотно.</summary>
        public float StatsLeft => CanvasLeft + CanvasSide + StatsGap;
        public float StatsTop => CanvasTop;
        public float StatsWidth => Width - SideMargin - StatsLeft;
        public float StatsHeight => CanvasSide;
        public float StatPanelHeight => (CanvasSide - StatsRowGap) * 0.5f;

        /// <summary>Верх поля від верху екрана й сторона квадрата.</summary>
        public float BoardTop => PictureTop + PictureBlock;
        public float BoardSide { get; }

        /// <summary>Верх лотка від верху екрана: лоток притиснутий до низу.</summary>
        public float TrayTop { get; }
        public float TrayBottom => TrayTop + TrayHeight;

        /// <summary>Чи все вміщається: поле не заходить під лоток, лоток не вилазить за низ, панелі рахунку — за край.</summary>
        public bool Fits => BoardTop + BoardSide + TrayGap <= TrayTop + 0.01f && TrayBottom <= Height + 0.01f
                            && BoardSide <= Width - BoardMargin * 2f + 0.01f
                            && PictureLeft >= -0.01f && StatsWidth > 0f && StatsLeft + StatsWidth <= Width - SideMargin + 0.01f;

        public static RunLayout For(float width, float height) => For(width, height, BoardGeometry.DefaultSideMargin);

        public static RunLayout For(float width, float height, float boardMargin) =>
            For(width, height, boardMargin, CanvasMaxSide, DefaultHaloReserve);

        /// <summary>
        /// Розкладка з бічним полем панелі поля, найбільшим полотном картинки й запасом під гало з дизайн-системи
        /// (px макета). Полотно стискається першим, але не нижче <see cref="CanvasMinSide"/>; ширина екрана теж
        /// обмежує його — колонка рахунку не вужча за <see cref="StatsMinWidth"/>.
        /// </summary>
        public static RunLayout For(float width, float height, float boardMargin, float canvasMax, float haloReserve)
        {
            if (width <= 0f || height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (boardMargin < 0f)
                boardMargin = 0f;
            if (haloReserve < 0f)
                haloReserve = 0f;
            if (canvasMax < CanvasMinSide)
                canvasMax = CanvasMinSide;

            var statsGap = Math.Max(StatsMinGap, haloReserve);
            var widthCap = width - Math.Max(SideMargin, haloReserve) - SideMargin - statsGap - StatsMinWidth;
            var canvasCap = Math.Max(CanvasMinSide, Math.Min(canvasMax, widthCap));
            var canvas = canvasCap;

            var maxSide = width - boardMargin * 2f;
            var trayTop = height - TrayHeight;
            float Available(float c) => trayTop - TrayGap - (HeaderHeight + c + haloReserve * 2f);

            // Скільки лишається полю при повному полотні.
            var side = Math.Min(maxSide, Available(canvas));

            // Не влазить по висоті — спершу стискаємо полотно, і лише потім поле.
            if (side < maxSide)
            {
                canvas = Math.Max(CanvasMinSide, canvas - (maxSide - side));
                side = Math.Min(maxSide, Available(canvas));
            }

            side = Math.Max(side, Math.Min(BoardMinSide, maxSide));
            return new RunLayout(width, height, boardMargin, haloReserve, canvasCap, canvas, side, trayTop);
        }
    }
}
