using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// POCO-дзеркало PieceCatalog.asset (§9): які форми взагалі бувають і як часто.
    ///
    /// Каталог одноразово розкладає форми по розмірах, бо генератор лотка питає
    /// «дай форму не більшу за N» на кожну фігуру, а ескалація змінює N по ходу партії
    /// (§2.7). Фільтрувати весь список щоразу означало б алокацію на кожен лоток.
    /// </summary>
    public sealed class PieceCatalogData
    {
        private readonly PieceShape[][] _bySizeCap;
        private readonly int[][] _weightPrefixByCap;

        public PieceCatalogData(IReadOnlyList<PieceShape> shapes)
        {
            if (shapes is null || shapes.Count == 0)
                throw new ArgumentException("Порожній каталог форм.", nameof(shapes));

            Shapes = shapes;
            var maxSize = 0;
            foreach (var s in shapes)
                if (s.Size > maxSize)
                    maxSize = s.Size;

            MaxSize = maxSize;
            _bySizeCap = new PieceShape[maxSize + 1][];
            _weightPrefixByCap = new int[maxSize + 1][];

            for (var cap = 1; cap <= maxSize; cap++)
            {
                var bucket = new List<PieceShape>();
                foreach (var s in shapes)
                    if (s.Size <= cap)
                        bucket.Add(s);

                _bySizeCap[cap] = bucket.ToArray();
                var prefix = new int[bucket.Count];
                var sum = 0;
                for (var i = 0; i < bucket.Count; i++)
                {
                    sum += bucket[i].Weight;
                    prefix[i] = sum;
                }

                _weightPrefixByCap[cap] = prefix;
            }
        }

        public IReadOnlyList<PieceShape> Shapes { get; }
        public int MaxSize { get; }

        /// <summary>Форми, не більші за стелю. Стеля піднімається ескалацією (§2.7).</summary>
        public IReadOnlyList<PieceShape> UpTo(int sizeCap) => _bySizeCap[Clamp(sizeCap)];

        /// <summary>Форма за ваговою таблицею. Детермінована від джерела випадковості.</summary>
        public PieceShape Pick(int sizeCap, IRandomSource random)
        {
            if (random is null) throw new ArgumentNullException(nameof(random));
            var cap = Clamp(sizeCap);
            var bucket = _bySizeCap[cap];
            var prefix = _weightPrefixByCap[cap];
            var roll = random.Next(prefix[prefix.Length - 1]);
            for (var i = 0; i < prefix.Length; i++)
                if (roll < prefix[i])
                    return bucket[i];
            return bucket[bucket.Length - 1];
        }

        /// <summary>Найменша форма каталогу — нею рятує fallback лотка (§2.7).</summary>
        public PieceShape Smallest
        {
            get
            {
                var best = Shapes[0];
                for (var i = 1; i < Shapes.Count; i++)
                    if (Shapes[i].Size < best.Size)
                        best = Shapes[i];
                return best;
            }
        }

        private int Clamp(int cap)
        {
            if (cap < 1) return 1;
            return cap > MaxSize ? MaxSize : cap;
        }

        /// <summary>
        /// Базовий каталог: від 1×1 до п'ятиклітинкових, як у Block Blast.
        /// Ваги спадають із розміром — великі фігури мають бути подією, а не нормою,
        /// інакше поле забивається швидше, ніж гравець устигає планувати чисту лінію.
        /// </summary>
        public static PieceCatalogData Default { get; } = new PieceCatalogData(new[]
        {
            Shape("1x1", 6, "X"),

            Shape("2h", 8, "XX"),
            Shape("2v", 8, "X", "X"),

            Shape("3h", 7, "XXX"),
            Shape("3v", 7, "X", "X", "X"),
            Shape("corner_ne", 6, "XX", "X."),
            Shape("corner_nw", 6, "XX", ".X"),
            Shape("corner_se", 6, "X.", "XX"),
            Shape("corner_sw", 6, ".X", "XX"),

            Shape("4h", 4, "XXXX"),
            Shape("4v", 4, "X", "X", "X", "X"),
            Shape("square", 6, "XX", "XX"),
            Shape("l4_0", 3, "X.", "X.", "XX"),
            Shape("l4_1", 3, "XXX", "X.."),
            Shape("l4_2", 3, "XX", ".X", ".X"),
            Shape("l4_3", 3, "..X", "XXX"),
            Shape("j4_0", 3, ".X", ".X", "XX"),
            Shape("j4_1", 3, "X..", "XXX"),
            Shape("j4_2", 3, "XX", "X.", "X."),
            Shape("j4_3", 3, "XXX", "..X"),
            Shape("s4_0", 3, ".XX", "XX."),
            Shape("s4_1", 3, "X.", "XX", ".X"),
            Shape("z4_0", 3, "XX.", ".XX"),
            Shape("z4_1", 3, ".X", "XX", "X."),
            Shape("t4_0", 3, "XXX", ".X."),
            Shape("t4_1", 3, "X.", "XX", "X."),
            Shape("t4_2", 3, ".X.", "XXX"),
            Shape("t4_3", 3, ".X", "XX", ".X"),

            Shape("5h", 2, "XXXXX"),
            Shape("5v", 2, "X", "X", "X", "X", "X"),
            Shape("plus", 2, ".X.", "XXX", ".X."),
            Shape("l5_0", 2, "X..", "X..", "XXX"),
            Shape("l5_1", 2, "XXX", "X..", "X.."),
            Shape("l5_2", 2, "XXX", "..X", "..X"),
            Shape("l5_3", 2, "..X", "..X", "XXX")
        });

        /// <summary>
        /// Форма з текстової розкладки: рядки йдуть ЗВЕРХУ ВНИЗ, як їх видно в коді,
        /// а модель рахує Y знизу вгору — перевертання тут, один раз, а не в кожній формі.
        /// </summary>
        public static PieceShape Shape(string id, int weight, params string[] rows)
        {
            if (rows is null || rows.Length == 0)
                throw new ArgumentException("Форма без рядків.", nameof(rows));

            var cells = new List<GridPos>(8);
            for (var r = 0; r < rows.Length; r++)
            {
                var row = rows[r];
                for (var c = 0; c < row.Length; c++)
                    if (row[c] == 'X')
                        cells.Add(new GridPos(c, rows.Length - 1 - r));
            }

            return new PieceShape(id, cells.ToArray(), weight);
        }
    }
}
