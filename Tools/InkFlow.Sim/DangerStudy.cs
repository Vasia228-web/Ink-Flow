using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Sim
{
    /// <summary>
    /// Ознаки поля для калібрування пульсації «мало місця» (§11, --danger-csv): що з них
    /// передбачає програш за 2–3 ходи. Лише для прогонника — у грі живе те, що з цього вибрано.
    /// </summary>
    internal static class DangerStudy
    {
        internal readonly struct Result
        {
            public Result(int catalogMinFits, int catalogFitsSum, int bigMinFits, int squares3, int fragmentation,
                int handMinFits, int handWays, bool handSolvable, int tight1, int tight3, int tight6, float logSum)
            {
                Tight1 = tight1;
                Tight3 = tight3;
                Tight6 = tight6;
                LogSum = logSum;
                CatalogMinFits = catalogMinFits;
                CatalogFitsSum = catalogFitsSum;
                BigMinFits = bigMinFits;
                Squares3 = squares3;
                Fragmentation = fragmentation;
                HandMinFits = handMinFits;
                HandWays = handWays;
                HandSolvable = handSolvable;
            }

            public int CatalogMinFits { get; }
            public int CatalogFitsSum { get; }
            public int BigMinFits { get; }
            public int Squares3 { get; }
            public int Fragmentation { get; }
            public int HandMinFits { get; }
            public int HandWays { get; }
            public bool HandSolvable { get; }
            public int Tight1 { get; }
            public int Tight3 { get; }
            public int Tight6 { get; }
            public float LogSum { get; }
        }

        private const int WaysCap = 200;
        private static readonly List<Line> Lines = new List<Line>(16);

        public static Result Features(Board board, PieceCatalogData catalog, IReadOnlyList<PieceDef> hand)
        {
            var catMin = int.MaxValue;
            var catSum = 0;
            var bigMin = int.MaxValue;
            int t1 = 0, t3 = 0, t6 = 0;
            var logSum = 0f;
            for (var c = 0; c < catalog.Count; c++)
            {
                var fits = PlacementRules.CountFits(board, catalog[c]);
                catSum += fits;
                if (fits <= 1) t1++;
                if (fits <= 3) t3++;
                if (fits <= 6) t6++;
                logSum += (float)System.Math.Log(1 + fits);
                if (fits < catMin) catMin = fits;
                if (catalog[c].Size >= 4 && fits < bigMin) bigMin = fits;
            }

            var squares = 0;
            for (var y = 0; y + 2 < board.Height; y++)
                for (var x = 0; x + 2 < board.Width; x++)
                {
                    var empty = true;
                    for (var dy = 0; dy < 3 && empty; dy++)
                        for (var dx = 0; dx < 3 && empty; dx++)
                            if (board[x + dx, y + dy] != Board.Empty)
                                empty = false;
                    if (empty) squares++;
                }

            var handMin = int.MaxValue;
            var pieces = new List<PieceShape>(3);
            for (var i = 0; i < hand.Count; i++)
            {
                if (hand[i].IsEmpty) continue;
                pieces.Add(hand[i].Shape!);
                var fits = PlacementRules.CountFits(board, hand[i].Shape!);
                if (fits < handMin) handMin = fits;
            }
            if (handMin == int.MaxValue) handMin = 0;

            var ways = 0;
            var used = new bool[pieces.Count];
            Count(board.Clone(), pieces, used, pieces.Count, ref ways);
            return new Result(catMin, catSum, bigMin == int.MaxValue ? 0 : bigMin, squares, Fragmentation(board),
                handMin, ways, ways > 0, t1, t3, t6, logSum);
        }

        /// <summary>Скільки способів поставити ВСЮ руку в будь-якому порядку зі зривами ліній (до WaysCap).</summary>
        private static void Count(Board board, List<PieceShape> pieces, bool[] used, int left, ref int ways)
        {
            if (ways >= WaysCap) return;
            if (left == 0) { ways++; return; }
            var scratch = new Board(board.Width, board.Height);
            for (var p = 0; p < pieces.Count; p++)
            {
                if (used[p]) continue;
                var shape = pieces[p];
                for (var y = 0; y < board.Height; y++)
                    for (var x = 0; x < board.Width; x++)
                    {
                        var anchor = new GridPos(x, y);
                        if (!PlacementRules.CanPlace(board, shape, anchor)) continue;
                        scratch.CopyFrom(board);
                        for (var i = 0; i < shape.Cells.Length; i++)
                            scratch[new GridPos(x + shape.Cells[i].X, y + shape.Cells[i].Y)] = 1;
                        Lines.Clear();
                        PlacementRules.CollectFullLines(scratch, Lines);
                        foreach (var line in Lines)
                        {
                            var length = LineResolver.LengthOf(scratch, line.Kind);
                            for (var c = 0; c < length; c++)
                                scratch[LineResolver.CellAt(line, c)] = Board.Empty;
                        }
                        used[p] = true;
                        Count(scratch.Clone(), pieces, used, left - 1, ref ways);
                        used[p] = false;
                        if (ways >= WaysCap) return;
                    }
            }
        }

        private static int Fragmentation(Board board)
        {
            var walls = 0;
            for (var y = 0; y < board.Height; y++)
                for (var x = 0; x < board.Width; x++)
                {
                    if (board[x, y] != Board.Empty) continue;
                    walls += Wall(board, x + 1, y) + Wall(board, x - 1, y) + Wall(board, x, y + 1) + Wall(board, x, y - 1);
                }
            return walls;
        }

        private static int Wall(Board board, int x, int y) =>
            x < 0 || x >= board.Width || y < 0 || y >= board.Height || board[x, y] != Board.Empty ? 1 : 0;
    }
}
