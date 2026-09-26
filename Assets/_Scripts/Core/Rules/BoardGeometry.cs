namespace InkFlow.Core
{
    /// <summary>
    /// Розкладка поля й лотка в px макета (ширина екрана 390). Полотно поля — це панель
    /// на всю ширину мінус бічне поле; усередині — відступ до крайніх лунок і проміжок
    /// між лунками. Пропорції — з еталона K1Candy (`docs/StyleRef/K1Candy/`): відступ
    /// ≈45 % кроку сітки, проміжок 4 при кроці 40.
    ///
    /// Живе в Core, бо тут є інваріант, який тримається тестом: точка на полотні мусить
    /// однозначно перетворюватись у клітинку й назад, а фігура з лотка — читатись як та
    /// сама фігура, що ляже на поле. Якщо масштаб лотка й крок поля розійдуться, гравець
    /// промахуватиметься, і жодна анімація це не сховає.
    ///
    /// Значення за замовчуванням — стартові для повзунків `DesignSystem`; в'юха передає
    /// сюди те, що виставив автор (<see cref="For(int, int, float, float, float)"/>).
    /// </summary>
    public readonly struct BoardGeometry
    {
        /// <summary>Ширина макета, px.</summary>
        public const float ScreenWidth = 390f;

        /// <summary>Бічне поле панелі поля (і лотка) від краю екрана.</summary>
        public const float DefaultSideMargin = 8f;

        /// <summary>Відступ від краю панелі до крайніх лунок: ≈45 % кроку 42 (еталон K1: 18 із 40).</summary>
        public const float DefaultPadding = 19f;

        /// <summary>Проміжок між лунками (еталон K1: 4 при кроці 40).</summary>
        public const float DefaultGap = 4f;

        /// <summary>Блок займає цю частку клітинки — решта дає видимий шов між сусідами.</summary>
        public const float BlockFraction = 0.9f;

        /// <summary>Клітинка фігури в лотку й проміжок між ними, px макета.</summary>
        public const float TrayBox = 21f;
        public const float TrayGap = 4f;

        private BoardGeometry(int width, int height, float canvas, float padding, float gap, float box)
        {
            Width = width;
            Height = height;
            Canvas = canvas;
            Padding = padding;
            Gap = gap;
            Box = box;
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Сторона полотна поля (панелі), px макета.</summary>
        public float Canvas { get; }

        /// <summary>Відступ від краю панелі до крайніх лунок.</summary>
        public float Padding { get; }

        /// <summary>Проміжок між клітинками поля.</summary>
        public float Gap { get; }

        /// <summary>Сторона клітинки поля.</summary>
        public float Box { get; }

        /// <summary>Крок сітки — від центру до центру сусіда.</summary>
        public float Step => Box + Gap;

        /// <summary>Видимий розмір блока всередині клітинки.</summary>
        public float Block => Round(Box * BlockFraction);

        /// <summary>Крок сітки лотка.</summary>
        public static float TrayStep => TrayBox + TrayGap;

        /// <summary>Сторона полотна для бічного поля: панель на всю ширину макета мінус поля.</summary>
        public static float CanvasFor(float sideMargin) => ScreenWidth - sideMargin * 2f;

        public static BoardGeometry For(int width, int height) =>
            For(width, height, DefaultSideMargin, DefaultPadding, DefaultGap);

        /// <summary>Геометрія під токени дизайн-системи: бічне поле, відступ і проміжок у px макета.</summary>
        public static BoardGeometry For(int width, int height, float sideMargin, float padding, float gap)
        {
            if (width < 2) width = 2;
            if (height < 2) height = 2;
            if (sideMargin < 0f) sideMargin = 0f;
            if (padding < 0f) padding = 0f;
            if (gap < 0f) gap = 0f;
            var canvas = CanvasFor(sideMargin);
            var size = width > height ? width : height;
            var box = Floor((canvas - padding * 2f - gap * (size - 1)) / size);
            if (box < 1f) box = 1f;
            return new BoardGeometry(width, height, canvas, padding, gap, box);
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

        /// <summary>
        /// Запас між зовнішнім кутом кутової лунки й заокругленим кутом панелі радіуса
        /// <paramref name="panelRadius"/>, px макета. Додатний — лунка не тисне на кут;
        /// від'ємний — її кут вилазить за дугу (при відступі 8 і радіусі 28 запас був ~2 px).
        /// </summary>
        public float CornerClearance(float panelRadius)
        {
            var inset = InsetX < InsetY ? InsetX : InsetY;
            if (inset >= panelRadius)
                return inset;
            // Кут лунки лежить на діагоналі; дуга — коло радіуса R з центром (R, R).
            var toCentre = (panelRadius - inset) * 1.41421356f;
            return panelRadius - toCentre;
        }

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
