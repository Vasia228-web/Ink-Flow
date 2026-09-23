using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Каталог форм: які поліоміно взагалі бувають (документ §2: прямі, кути, T, L,
    /// квадрати, зигзаги, 2–5 клітинок). Фігури не обертаються, тож кожна орієнтація
    /// лежить тут окремою формою — саме так вона й видається в лоток.
    ///
    /// Яку форму дати — вирішує <see cref="TrayGenerator"/> за вагами розміру, вільного
    /// місця й прогресії; каталог лише знає, що існує.
    /// </summary>
    public sealed class PieceCatalogData
    {
        private readonly PieceShape[] _shapes;

        public PieceCatalogData(IReadOnlyList<PieceShape> shapes)
        {
            if (shapes is null || shapes.Count == 0)
                throw new ArgumentException("Порожній каталог форм.", nameof(shapes));

            _shapes = new PieceShape[shapes.Count];
            var maxSize = 0;
            var minSize = int.MaxValue;
            for (var i = 0; i < shapes.Count; i++)
            {
                _shapes[i] = shapes[i];
                if (shapes[i].Size > maxSize) maxSize = shapes[i].Size;
                if (shapes[i].Size < minSize) minSize = shapes[i].Size;
            }

            MaxSize = maxSize;
            MinSize = minSize;
        }

        public IReadOnlyList<PieceShape> Shapes => _shapes;
        public int Count => _shapes.Length;
        public PieceShape this[int index] => _shapes[index];
        public int MaxSize { get; }
        public int MinSize { get; }

        /// <summary>Найменша форма каталогу — нею рятує запобіжник лотка.</summary>
        public PieceShape Smallest
        {
            get
            {
                var best = _shapes[0];
                for (var i = 1; i < _shapes.Length; i++)
                    if (_shapes[i].Size < best.Size)
                        best = _shapes[i];
                return best;
            }
        }

        /// <summary>
        /// Базовий каталог за документом: прямі 2–5, кути, квадрат, L/J, S/Z, T, хрест.
        /// Усі орієнтації окремо. Вага в межах розміру однакова — різницю між розмірами
        /// задає баланс (<see cref="BalanceData.SizeWeight"/>), а не каталог.
        /// </summary>
        public static PieceCatalogData Default { get; } = new PieceCatalogData(new[]
        {
            Shape("2h", "XX"),
            Shape("2v", "X", "X"),

            Shape("3h", "XXX"),
            Shape("3v", "X", "X", "X"),
            Shape("corner_ne", "XX", "X."),
            Shape("corner_nw", "XX", ".X"),
            Shape("corner_se", "X.", "XX"),
            Shape("corner_sw", ".X", "XX"),

            Shape("4h", "XXXX"),
            Shape("4v", "X", "X", "X", "X"),
            Shape("square", "XX", "XX"),
            Shape("l4_0", "X.", "X.", "XX"),
            Shape("l4_1", "XXX", "X.."),
            Shape("l4_2", "XX", ".X", ".X"),
            Shape("l4_3", "..X", "XXX"),
            Shape("j4_0", ".X", ".X", "XX"),
            Shape("j4_1", "X..", "XXX"),
            Shape("j4_2", "XX", "X.", "X."),
            Shape("j4_3", "XXX", "..X"),
            Shape("s4_0", ".XX", "XX."),
            Shape("s4_1", "X.", "XX", ".X"),
            Shape("z4_0", "XX.", ".XX"),
            Shape("z4_1", ".X", "XX", "X."),
            Shape("t4_0", "XXX", ".X."),
            Shape("t4_1", "X.", "XX", "X."),
            Shape("t4_2", ".X.", "XXX"),
            Shape("t4_3", ".X", "XX", ".X"),

            Shape("5h", "XXXXX"),
            Shape("5v", "X", "X", "X", "X", "X"),
            Shape("plus", ".X.", "XXX", ".X."),
            Shape("l5_0", "X..", "X..", "XXX"),
            Shape("l5_1", "XXX", "X..", "X.."),
            Shape("l5_2", "XXX", "..X", "..X"),
            Shape("l5_3", "..X", "..X", "XXX")
        });

        /// <summary>
        /// Форма з текстової розкладки: рядки йдуть ЗВЕРХУ ВНИЗ, як їх видно в коді,
        /// а модель рахує Y знизу вгору — перевертання тут, один раз.
        /// </summary>
        public static PieceShape Shape(string id, params string[] rows)
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

            return new PieceShape(id, cells.ToArray());
        }
    }
}
