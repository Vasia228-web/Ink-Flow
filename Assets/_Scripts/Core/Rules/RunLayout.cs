using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Розкладка екрана забігу (документ §11) для будь-якого екрана: лоток завжди при
    /// нижньому краю safe area, поле — квадрат зі стороною min(ширина, висота, що лишилась
    /// між картинкою й лотком), картинка вгорі стискається, коли місця мало. Жоден елемент
    /// не виходить за екран — це тримає тест на розмірах реальних пристроїв, а не око.
    ///
    /// Усі числа — в px макета (390 завширшки); в'ю множить на свій коефіцієнт.
    /// Панель картинки 216×186 — трохи більша за попередню редакцію (196×172); на короткому
    /// екрані вона стискається першою, поле й лоток — ніколи.
    ///
    /// Бічне поле панелі поля й лотка (<see cref="BoardMargin"/>) — токен дизайн-системи
    /// (за замовчуванням <see cref="BoardGeometry.DefaultSideMargin"/>); шапка й колонка
    /// рахунку тримають своє поле <see cref="SideMargin"/>.
    /// </summary>
    public readonly struct RunLayout
    {
        public const float SideMargin = 16f;
        public const float HeaderHeight = 40f;
        public const float PictureGapTop = 10f;
        public const float PictureHeight = 186f;
        public const float PictureMinHeight = 108f;
        public const float BoardGapTop = 8f;
        public const float TrayGap = 10f;
        public const float TrayHeight = 86f;
        public const float BoardMinSide = 200f;
        public const float PictureMaxWidth = 216f;
        public const float StatsMinWidth = 56f;
        public const float StatsMaxWidth = 92f;
        public const float StatsGap = 4f;

        private RunLayout(float width, float height, float boardMargin, float pictureBlock, float pictureScale, float boardTop, float boardSide, float trayTop)
        {
            Width = width;
            Height = height;
            BoardMargin = boardMargin;
            PictureBlock = pictureBlock;
            PictureScale = pictureScale;
            BoardTop = boardTop;
            BoardSide = boardSide;
            TrayTop = trayTop;
        }

        public float Width { get; }
        public float Height { get; }

        /// <summary>Бічне поле панелі поля й лотка від краю екрана.</summary>
        public float BoardMargin { get; }

        /// <summary>Повна висота блоку картинки (токен; за замовчуванням <see cref="PictureHeight"/>).</summary>
        public float PictureBlock { get; }

        /// <summary>Ширина блоку картинки по центру і колонки рахунку праворуч — так, щоб не перекривались.</summary>
        public float StatsWidth => Math.Max(StatsMinWidth, Math.Min(StatsMaxWidth, (Width - PictureMaxWidth) * 0.5f - SideMargin - StatsGap));
        public float PictureWidth => Math.Min(PictureMaxWidth, Width - 2f * (SideMargin + StatsWidth + StatsGap));

        /// <summary>Масштаб блоку картинки (1 — повний); висота блоку = PictureBlock × масштаб.</summary>
        public float PictureScale { get; }
        public float PictureTop => HeaderHeight + PictureGapTop;
        public float PictureShownHeight => PictureBlock * PictureScale;

        /// <summary>Верх поля від верху екрана й сторона квадрата.</summary>
        public float BoardTop { get; }
        public float BoardSide { get; }

        /// <summary>Верх лотка від верху екрана: лоток притиснутий до низу.</summary>
        public float TrayTop { get; }
        public float TrayBottom => TrayTop + TrayHeight;

        /// <summary>Чи все вміщається: поле не заходить під лоток, лоток не вилазить за низ.</summary>
        public bool Fits => BoardTop + BoardSide + TrayGap <= TrayTop + 0.01f && TrayBottom <= Height + 0.01f
                            && BoardSide <= Width - BoardMargin * 2f + 0.01f
                            && Width * 0.5f + PictureWidth * 0.5f <= Width - SideMargin - StatsWidth - StatsGap + 0.01f;

        public static RunLayout For(float width, float height) => For(width, height, BoardGeometry.DefaultSideMargin);

        public static RunLayout For(float width, float height, float boardMargin) => For(width, height, boardMargin, PictureHeight);

        /// <summary>
        /// Розкладка з бічним полем панелі поля й висотою блоку картинки з дизайн-системи (px макета).
        /// Блок картинки стискається першим, але не нижче <see cref="PictureMinHeight"/>.
        /// </summary>
        public static RunLayout For(float width, float height, float boardMargin, float pictureBlock)
        {
            if (width <= 0f || height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (boardMargin < 0f)
                boardMargin = 0f;
            if (pictureBlock < PictureMinHeight)
                pictureBlock = PictureMinHeight;

            var maxSide = width - boardMargin * 2f;
            var trayTop = height - TrayHeight;
            var pictureHeight = pictureBlock;

            // Скільки лишається полю при повній картинці.
            var available = trayTop - TrayGap - (HeaderHeight + PictureGapTop + pictureHeight + BoardGapTop);
            var side = Math.Min(maxSide, available);

            // Не влазить по висоті — спершу стискаємо картинку, і лише потім поле.
            if (side < maxSide)
            {
                var shortage = maxSide - side;
                pictureHeight = Math.Max(PictureMinHeight, pictureHeight - shortage);
                available = trayTop - TrayGap - (HeaderHeight + PictureGapTop + pictureHeight + BoardGapTop);
                side = Math.Min(maxSide, available);
            }

            side = Math.Max(side, Math.Min(BoardMinSide, maxSide));
            var boardTop = HeaderHeight + PictureGapTop + pictureHeight + BoardGapTop;
            return new RunLayout(width, height, boardMargin, pictureBlock, pictureHeight / pictureBlock, boardTop, side, trayTop);
        }
    }
}
