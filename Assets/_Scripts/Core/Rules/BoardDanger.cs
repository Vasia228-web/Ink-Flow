using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Рівень небезпеки «мало місця» (документ §11).</summary>
    public enum DangerLevel : byte
    {
        None = 0,

        /// <summary>Поле тісне: багатьом формам каталогу лишилось кілька місць, або фігура з руки застрягла.</summary>
        Warn = 1,

        /// <summary>Поле майже впало: тісна половина каталогу, або фігура з руки вже нікуди не влазить.</summary>
        Strong = 2
    }

    /// <summary>
    /// Оцінка небезпеки поля (документ §11): тригер визначає логіка, не відсоток на око.
    ///
    /// Головна ознака — «тісні форми»: скільки форм КАТАЛОГУ (а не лише руки) мають на полі
    /// не більше <see cref="BalanceData.DangerTightFits"/> місць. Рука бачить лише три фігури,
    /// а наступний лоток прийде з каталогу; тому стан руки передбачав програш за один хід,
    /// а тісні форми — за кілька (калібрування ботом — `docs/implementation-notes.md`, Сесія 5).
    ///
    /// Попередження має гістерезис: вмикається на <see cref="BalanceData.DangerWarnShapes"/>
    /// тісних формах, а гасне лише коли їх знову не більше <see cref="BalanceData.DangerCalmShapes"/>.
    /// Інакше поле на межі вмикало б і гасило рамку щоходу. Тому оцінка бере попередній рівень —
    /// функція лишається чистою, а стан тримає сесія (<see cref="RunSession.Danger"/>).
    /// </summary>
    public readonly struct BoardDanger
    {
        private BoardDanger(DangerLevel level, int pieces, int stuck, int placeable, int bestFits, int tightShapes)
        {
            Level = level;
            Pieces = pieces;
            Stuck = stuck;
            Placeable = placeable;
            BestFits = bestFits;
            TightShapes = tightShapes;
        }

        public DangerLevel Level { get; }

        /// <summary>Скільки фігур у руці.</summary>
        public int Pieces { get; }

        /// <summary>Скільки з них уже нікуди поставити.</summary>
        public int Stuck { get; }

        /// <summary>Скільки ще влазить хоч кудись.</summary>
        public int Placeable { get; }

        /// <summary>Найбільше позицій серед фігур руки — скільки свободи в найзручнішої.</summary>
        public int BestFits { get; }

        /// <summary>Скільки форм каталогу мають на полі не більше DangerTightFits місць.</summary>
        public int TightShapes { get; }

        public static readonly BoardDanger Calm = new BoardDanger(DangerLevel.None, 0, 0, 0, 0, 0);

        /// <param name="previous">Рівень після попереднього ходу — для гістерезису попередження.</param>
        public static BoardDanger Evaluate(Board board, IReadOnlyList<PieceDef> tray, PieceCatalogData catalog,
            BalanceData balance, DangerLevel previous = DangerLevel.None)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (tray is null) throw new ArgumentNullException(nameof(tray));
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (balance is null) throw new ArgumentNullException(nameof(balance));

            var pieces = 0;
            var stuck = 0;
            var bestFits = 0;
            for (var i = 0; i < tray.Count; i++)
            {
                var piece = tray[i];
                if (piece.IsEmpty)
                    continue;
                pieces++;
                var fits = PlacementRules.CountFits(board, piece.Shape!);
                if (fits == 0)
                    stuck++;
                else if (fits > bestFits)
                    bestFits = fits;
            }

            var placeable = pieces - stuck;
            if (pieces == 0 || placeable == 0)
                return new BoardDanger(DangerLevel.None, pieces, stuck, placeable, bestFits, 0); // порожня рука або вже програш — не пульсуємо

            var tight = CountTightShapes(board, catalog, balance.DangerTightFits);

            var level = DangerLevel.None;
            if (stuck > 0 || tight >= balance.DangerStrongShapes)
                level = DangerLevel.Strong;
            else if (tight >= balance.DangerWarnShapes || (previous != DangerLevel.None && tight > balance.DangerCalmShapes))
                level = DangerLevel.Warn;

            return new BoardDanger(level, pieces, stuck, placeable, bestFits, tight);
        }

        /// <summary>Скільки форм каталогу мають не більше <paramref name="maxFits"/> місць (0 — застрягла форма теж тісна).</summary>
        public static int CountTightShapes(Board board, PieceCatalogData catalog, int maxFits)
        {
            var tight = 0;
            for (var i = 0; i < catalog.Count; i++)
                if (CountFitsUpTo(board, catalog[i], maxFits + 1) <= maxFits)
                    tight++;
            return tight;
        }

        /// <summary>Позиції форми, але рахуємо лише до <paramref name="limit"/>: для «тісна чи ні» більше не треба.</summary>
        private static int CountFitsUpTo(Board board, PieceShape shape, int limit)
        {
            var count = 0;
            for (var y = 0; y <= board.Height - shape.Height; y++)
                for (var x = 0; x <= board.Width - shape.Width; x++)
                    if (PlacementRules.CanPlace(board, shape, new GridPos(x, y)) && ++count >= limit)
                        return count;
            return count;
        }

        public override string ToString() =>
            $"{Level}(тісних форм {TightShapes}, у руці {Pieces}, застрягло {Stuck}, найзручнішій {BestFits} місць)";
    }
}
