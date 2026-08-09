namespace InkFlow.Core
{
    /// <summary>
    /// Розкладка ігрового поля в px макета. Полотно квадратне й фіксоване, а клітинка
    /// підганяється під розмір сітки — саме тому 4×4 і 7×7 займають однакове місце
    /// на екрані й HUD не стрибає між рівнями.
    ///
    /// Числа — з макета «Ink Flow v2». Тут, а не у в'ю, бо від співвідношення
    /// краплі до кроку залежить, чи не зіткнуться сусіди при косметичному зсуві
    /// (<see cref="BoardJitter"/>), і це перевіряється тестом.
    /// </summary>
    public readonly struct BoardGeometry
    {
        /// <summary>Сторона полотна поля, px макета.</summary>
        public const float Canvas = 358f;

        /// <summary>Внутрішнє поле полотна.</summary>
        public const float Padding = 8f;

        /// <summary>Крапля займає цю частку клітинки.</summary>
        public const float BlobFraction = 0.84f;

        private BoardGeometry(int size, int width, int height, float box, float gap)
        {
            Size = size;
            Width = width;
            Height = height;
            Box = box;
            Gap = gap;
        }

        /// <summary>Розмір, під який підігнано клітинку: більша зі сторін сітки.</summary>
        public int Size { get; }

        /// <summary>Колонок у сітці.</summary>
        public int Width { get; }

        /// <summary>Рядів у сітці.</summary>
        public int Height { get; }

        /// <summary>Сторона клітинки.</summary>
        public float Box { get; }

        /// <summary>Проміжок між клітинками.</summary>
        public float Gap { get; }

        /// <summary>Крок сітки — від центру до центру сусіда.</summary>
        public float Step => Box + Gap;

        /// <summary>Діаметр краплі.</summary>
        public float Blob => Round(Box * BlobFraction);

        /// <summary>Розмір числа густоти всередині краплі.</summary>
        public float Font => Max(13f, Round(Box * 0.335f));

        /// <summary>Частка кроку, яку займає крапля — вхід для перевірки джиттера.</summary>
        public float BlobToStep => Blob / Step;

        public static BoardGeometry For(int size) => For(size, size);

        /// <summary>
        /// Клітинку підганяємо під БІЛЬШУ сторону, а меншу центруємо в полотні:
        /// інакше на прямокутній сітці клітинки були б неквадратні, і хрест вибуху
        /// перестав би читатись як хрест.
        /// </summary>
        public static BoardGeometry For(int width, int height)
        {
            if (width < 2) width = 2;
            if (height < 2) height = 2;
            var size = width > height ? width : height;

            // Проміжок звужується зі зростанням сітки: на 7×7 однаковий із 4×4 зазор
            // з'їв би стільки місця, що крапля стала б завмалою для числа всередині.
            var gap = size >= 7 ? 6f : size >= 6 ? 7f : 9f;
            var box = Floor((Canvas - Padding * 2f - gap * (size - 1)) / size);
            return new BoardGeometry(size, width, height, box, gap);
        }

        /// <summary>Поле по горизонталі, що центрує вужчу сітку.</summary>
        public float InsetX => Padding + (Size - Width) * Step * 0.5f;

        /// <summary>Поле по вертикалі, що центрує нижчу сітку.</summary>
        public float InsetY => Padding + (Size - Height) * Step * 0.5f;

        /// <summary>Центр клітинки від ЛІВОГО-ВЕРХНЬОГО кута полотна, px макета.</summary>
        public float CenterX(int column) => InsetX + column * Step + Box * 0.5f;

        /// <summary>
        /// Центр клітинки від ВЕРХУ полотна. Ряд 0 у моделі — найнижчий,
        /// тому нумерація тут перевертається.
        /// </summary>
        public float CenterY(int row) => InsetY + (Height - 1 - row) * Step + Box * 0.5f;

        // Core збирається без UnityEngine — арифметику беремо з System.Math.
        private static float Floor(float v) => (float)System.Math.Floor(v);
        private static float Round(float v) => (float)System.Math.Round(v);
        private static float Max(float a, float b) => a > b ? a : b;
    }
}
