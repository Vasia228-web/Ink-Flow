namespace InkFlow.Core
{
    /// <summary>
    /// Розкладка поля й лотка в px макета (прототип v3, `geoWell`: полотно 358, поле 8,
    /// проміжок 5; лоток `TB = 21, TG = 4`).
    ///
    /// Живе в Core, бо тут є інваріант, який тримається тестом: точка на полотні мусить
    /// однозначно перетворюватись у клітинку й назад, а фігура з лотка — читатись як та
    /// сама фігура, що ляже на поле. Якщо масштаб лотка й крок поля розійдуться, гравець
    /// промахуватиметься, і жодна анімація це не сховає.
    /// </summary>
    public readonly struct BoardGeometry
    {
        /// <summary>Сторона полотна поля, px макета.</summary>
        public const float Canvas = 358f;

        /// <summary>Внутрішнє поле полотна.</summary>
        public const float Padding = 8f;

        /// <summary>Проміжок між клітинками поля.</summary>
        public const float Gap = 5f;

        /// <summary>Блок займає цю частку клітинки — решта дає видимий шов між сусідами.</summary>
        public const float BlockFraction = 0.9f;

        /// <summary>Клітинка фігури в лотку й проміжок між ними, px макета.</summary>
        public const float TrayBox = 21f;
        public const float TrayGap = 4f;

        private BoardGeometry(int width, int height, float box)
        {
            Width = width;
            Height = height;
            Box = box;
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Сторона клітинки поля.</summary>
        public float Box { get; }

        /// <summary>Крок сітки — від центру до центру сусіда.</summary>
        public float Step => Box + Gap;

        /// <summary>Видимий розмір блока всередині клітинки.</summary>
        public float Block => Round(Box * BlockFraction);

        /// <summary>Крок сітки лотка.</summary>
        public static float TrayStep => TrayBox + TrayGap;

        public static BoardGeometry For(int width, int height)
        {
            if (width < 2) width = 2;
            if (height < 2) height = 2;
            var size = width > height ? width : height;
            var box = Floor((Canvas - Padding * 2f - Gap * (size - 1)) / size);
            return new BoardGeometry(width, height, box);
        }

        public static BoardGeometry For(int size) => For(size, size);

        /// <summary>Поле по горизонталі, що центрує вужчу сітку.</summary>
        public float InsetX => Padding + (Canvas - Padding * 2f - (Width * Step - Gap)) * 0.5f;

        public float InsetY => Padding + (Canvas - Padding * 2f - (Height * Step - Gap)) * 0.5f;

        /// <summary>Центр клітинки від ЛІВОГО краю полотна, px макета.</summary>
        public float CenterX(int column) => InsetX + column * Step + Box * 0.5f;

        /// <summary>
        /// Центр клітинки від ВЕРХУ полотна. Ряд 0 у моделі — найнижчий,
        /// тому нумерація тут перевертається.
        /// </summary>
        public float CenterY(int row) => InsetY + (Height - 1 - row) * Step + Box * 0.5f;

        /// <summary>Колонка, у яку потрапляє точка від лівого краю полотна (для перетягування).</summary>
        public int ColumnAt(float x)
        {
            var raw = (int)Floor((x - InsetX + Gap * 0.5f) / Step);
            return raw < 0 ? 0 : raw >= Width ? Width - 1 : raw;
        }

        /// <summary>Ряд моделі, у який потрапляє точка від верху полотна.</summary>
        public int RowAt(float y)
        {
            var raw = (int)Floor((y - InsetY + Gap * 0.5f) / Step);
            var row = Height - 1 - raw;
            return row < 0 ? 0 : row >= Height ? Height - 1 : row;
        }

        /// <summary>
        /// Якір фігури, якщо палець (клітинка під ним) тримає її за центр: прототип v3
        /// зміщує якір на половину форми, щоб фігура сиділа під пальцем, а не звисала.
        /// </summary>
        public static GridPos AnchorFor(PieceShape shape, int column, int row) =>
            new GridPos(column - (shape.Width - 1) / 2, row - (shape.Height - 1) / 2);

        // Core збирається без UnityEngine — арифметику беремо з System.Math.
        private static float Floor(float v) => (float)System.Math.Floor(v);
        private static float Round(float v) => (float)System.Math.Round(v);
    }
}
