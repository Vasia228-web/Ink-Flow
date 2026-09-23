using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Чи влазить фігура і куди (§2.2, крок 1; §2.8).
    ///
    /// Якір — позиція клітинки (0,0) форми. Усе інше — зміщення від неї, тому одна
    /// перевірка обслуговує і палець гравця, і перебір бота, і запобіжник генерації лотка.
    /// </summary>
    public static class PlacementRules
    {
        /// <summary>Усі клітинки фігури в межах поля І порожні. Інших умов немає.</summary>
        public static bool CanPlace(Board board, PieceShape shape, GridPos anchor)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (shape is null) throw new ArgumentNullException(nameof(shape));

            var cells = shape.Cells;
            for (var i = 0; i < cells.Length; i++)
            {
                var p = new GridPos(anchor.X + cells[i].X, anchor.Y + cells[i].Y);
                if (!board.Contains(p) || board[p] != InkColor.None)
                    return false;
            }

            return true;
        }

        /// <summary>Чи існує хоч одна позиція для цієї форми — умова (A) генерації лотка.</summary>
        public static bool AnyFit(Board board, PieceShape shape)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (shape is null) throw new ArgumentNullException(nameof(shape));

            for (var y = 0; y <= board.Height - shape.Height; y++)
                for (var x = 0; x <= board.Width - shape.Width; x++)
                    if (CanPlace(board, shape, new GridPos(x, y)))
                        return true;
            return false;
        }

        /// <summary>
        /// Єдина перевірка живості партії (§2.8). Порожні комірки лотка ігноруються:
        /// поставлена фігура зникла, а лоток поповнюється лише коли спорожніє повністю.
        /// </summary>
        public static bool AnyPieceFits(Board board, IReadOnlyList<PieceDef> tray)
        {
            if (tray is null) throw new ArgumentNullException(nameof(tray));
            for (var i = 0; i < tray.Count; i++)
            {
                var piece = tray[i];
                if (!piece.IsEmpty && AnyFit(board, piece.Shape!))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Перша валідна позиція для першої ж фігури, що влазить — підказка від застою (§4).
        /// Детермінована: та сама дошка завжди підкаже те саме місце.
        /// </summary>
        public static bool TryFindHint(Board board, IReadOnlyList<PieceDef> tray,
            out int trayIndex, out GridPos anchor)
        {
            if (tray is null) throw new ArgumentNullException(nameof(tray));

            for (var i = 0; i < tray.Count; i++)
            {
                var piece = tray[i];
                if (piece.IsEmpty)
                    continue;

                var shape = piece.Shape!;
                for (var y = 0; y <= board.Height - shape.Height; y++)
                    for (var x = 0; x <= board.Width - shape.Width; x++)
                    {
                        var candidate = new GridPos(x, y);
                        if (!CanPlace(board, shape, candidate))
                            continue;
                        trayIndex = i;
                        anchor = candidate;
                        return true;
                    }
            }

            trayIndex = -1;
            anchor = default;
            return false;
        }

        /// <summary>
        /// Прев'ю наслідків до відпускання пальця (інваріант §11.8): які лінії зірвуться,
        /// якщо поставити фігуру сюди. Буфер дає викликач — щокадрово це рахує в'ю.
        /// </summary>
        public static void PreviewLines(Board board, PieceShape shape, GridPos anchor, InkColor color,
            List<Line> into)
        {
            if (into is null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (!CanPlace(board, shape, anchor))
                return;

            var cells = shape.Cells;
            for (var i = 0; i < cells.Length; i++)
                board[new GridPos(anchor.X + cells[i].X, anchor.Y + cells[i].Y)] = color;

            CollectFullLines(board, into);

            for (var i = 0; i < cells.Length; i++)
                board[new GridPos(anchor.X + cells[i].X, anchor.Y + cells[i].Y)] = InkColor.None;
        }

        /// <summary>
        /// Усі повні рядки й стовпці за поточним станом (§2.2, крок 4).
        /// Порядок фіксований: спершу рядки знизу вгору, потім стовпці зліва направо.
        /// </summary>
        public static void CollectFullLines(Board board, List<Line> into)
        {
            if (into is null) throw new ArgumentNullException(nameof(into));
            for (var y = 0; y < board.Height; y++)
                if (board.IsRowFull(y))
                    into.Add(new Line(LineKind.Row, y));
            for (var x = 0; x < board.Width; x++)
                if (board.IsColumnFull(x))
                    into.Add(new Line(LineKind.Column, x));
        }
    }
}
