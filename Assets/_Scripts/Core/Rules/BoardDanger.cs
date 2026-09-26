using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Рівень небезпеки «мало місця» (документ §11).</summary>
    public enum DangerLevel : byte
    {
        None = 0,

        /// <summary>Хоч одну фігуру з руки вже нікуди поставити, або найзручнішій лишилось дуже мало позицій.</summary>
        Warn = 1,

        /// <summary>Лишилась лише одна фігура, яку можна поставити, а інша застрягла.</summary>
        Strong = 2
    }

    /// <summary>
    /// Оцінка небезпеки поля (документ §11): тригер визначає логіка, не відсоток на око.
    /// Чистий підрахунок на готових станах поля — без стану й без часу, тож у тестах
    /// його можна перевірити на будь-якій розкладці, а в'ю лише перекладає рівень у
    /// пульсацію рамки. Пороги — в <see cref="BalanceData"/>.
    /// </summary>
    public readonly struct BoardDanger
    {
        private BoardDanger(DangerLevel level, int pieces, int stuck, int placeable, int bestFits)
        {
            Level = level;
            Pieces = pieces;
            Stuck = stuck;
            Placeable = placeable;
            BestFits = bestFits;
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

        public static readonly BoardDanger Calm = new BoardDanger(DangerLevel.None, 0, 0, 0, 0);

        public static BoardDanger Evaluate(Board board, IReadOnlyList<PieceDef> tray, BalanceData balance)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (tray is null) throw new ArgumentNullException(nameof(tray));
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
                return new BoardDanger(DangerLevel.None, pieces, stuck, placeable, bestFits); // порожня рука або вже програш — не пульсуємо

            var level = DangerLevel.None;
            if (stuck >= balance.DangerStuckPieces && placeable <= balance.DangerLastPlaceable)
                level = DangerLevel.Strong;
            else if (stuck >= balance.DangerStuckPieces || (balance.DangerFewFits > 0 && bestFits <= balance.DangerFewFits))
                level = DangerLevel.Warn;

            return new BoardDanger(level, pieces, stuck, placeable, bestFits);
        }

        public override string ToString() => $"{Level}(pieces={Pieces}, stuck={Stuck}, bestFits={BestFits})";
    }
}
