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
    /// </summary>
    public readonly struct RunLayout
    {
        public const float SideMargin = 16f;
        public const float HeaderHeight = 40f;
        public const float PictureGapTop = 10f;
        public const float PictureHeight = 172f;
        public const float PictureMinHeight = 108f;
        public const float BoardGapTop = 8f;
        public const float TrayGap = 10f;
        public const float TrayHeight = 86f;
        public const float BoardMinSide = 200f;
        public const float PictureMaxWidth = 196f;
        public const float StatsMinWidth = 56f;
        public const float StatsMaxWidth = 92f;
        public const float StatsGap = 4f;

        private RunLayout(float width, float height, float pictureScale, float boardTop, float boardSide, float trayTop)
        {
            Width = width;
            Height = height;
            PictureScale = pictureScale;
            BoardTop = boardTop;
            BoardSide = boardSide;
            TrayTop = trayTop;
        }

        public float Width { get; }
        public float Height { get; }

        /// <summary>Ширина блоку картинки по центру і колонки рахунку праворуч — так, щоб не перекривались.</summary>
        public float StatsWidth => Math.Max(StatsMinWidth, Math.Min(StatsMaxWidth, (Width - PictureMaxWidth) * 0.5f - SideMargin - StatsGap));
        public float PictureWidth => Math.Min(PictureMaxWidth, Width - 2f * (SideMargin + StatsWidth + StatsGap));

        /// <summary>Масштаб блоку картинки (1 — повний); висота блоку = PictureHeight × масштаб.</summary>
        public float PictureScale { get; }
        public float PictureTop => HeaderHeight + PictureGapTop;
        public float PictureShownHeight => PictureHeight * PictureScale;

        /// <summary>Верх поля від верху екрана й сторона квадрата.</summary>
        public float BoardTop { get; }
        public float BoardSide { get; }

        /// <summary>Верх лотка від верху екрана: лоток притиснутий до низу.</summary>
        public float TrayTop { get; }
        public float TrayBottom => TrayTop + TrayHeight;

        /// <summary>Чи все вміщається: поле не заходить під лоток, лоток не вилазить за низ.</summary>
        public bool Fits => BoardTop + BoardSide + TrayGap <= TrayTop + 0.01f && TrayBottom <= Height + 0.01f
                            && BoardSide <= Width - SideMargin * 2f + 0.01f
                            && Width * 0.5f + PictureWidth * 0.5f <= Width - SideMargin - StatsWidth - StatsGap + 0.01f;

        public static RunLayout For(float width, float height)
        {
            if (width <= 0f || height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));

            var maxSide = width - SideMargin * 2f;
            var trayTop = height - TrayHeight;
            var pictureHeight = PictureHeight;

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
            return new RunLayout(width, height, pictureHeight / PictureHeight, boardTop, side, trayTop);
        }
    }
}
