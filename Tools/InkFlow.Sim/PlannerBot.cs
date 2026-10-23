using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Sim
{
    /// <summary>
    /// «Уважний гравець»: дивиться на весь лоток і шукає порядок фігур, а не кладе по одній.
    /// Жадібний <see cref="RunBot"/> — нижня межа (гравець, що не планує); цей — верхня.
    /// Без нього ідеальний лоток неможливо оцінити: жадібний бот не бачить, що друга фігура
    /// зірве дві лінії, якщо першу покласти «невигідно».
    ///
    /// Пошук: на кожному рівні лишаються Keep найкращих розміщень за жадібною оцінкою,
    /// цінність шляху = лінії по дорозі + стан поля в кінці. Шум — на першому кроці, як у RunBot.
    /// </summary>
    public sealed class PlannerBot
    {
        private const int Keep = 5;

        private readonly BotWeights _weights;
        private readonly IRandomSource _noiseSource;
        private readonly float _noise;
        private readonly List<Line> _lines = new List<Line>(16);
        private readonly Board[] _boards = new Board[5]; // 0–3 — поле на кожному кроці, 4 — чернетка
        private readonly (float score, int piece, GridPos at, int lines, int pure)[][] _top =
            new (float, int, GridPos, int, int)[4][];
        private BalanceData? _balance;

        public PlannerBot(BotWeights weights, IRandomSource noiseSource, float noise)
        {
            _weights = weights;
            _noiseSource = noiseSource;
            _noise = noise;
            for (var i = 0; i < _top.Length; i++)
                _top[i] = new (float, int, GridPos, int, int)[Keep];
        }

        public bool TryChooseMove(RunSession session, out int trayIndex, out GridPos anchor)
        {
            _balance = session.Balance;
            var board = session.Board;
            for (var i = 0; i < _boards.Length; i++)
                if (_boards[i] is null || _boards[i].Width != board.Width)
                    _boards[i] = new Board(board.Width, board.Height);

            var tray = session.TrayPieces;
            var used = new bool[tray.Length];
            trayIndex = -1;
            anchor = default;
            var best = float.NegativeInfinity;

            var count = Candidates(board, tray, used, 0, noisy: true);
            for (var c = 0; c < count; c++)
            {
                var (_, piece, at, lines, pure) = _top[0][c];
                Apply(board, _boards[1], tray[piece], at);
                used[piece] = true;
                var value = Step(lines, pure) + Search(_boards[1], tray, used, 1);
                used[piece] = false;
                if (_noise > 0f)
                    value += _noise * (_noiseSource.Next(1024) / 1024f);
                if (value <= best)
                    continue;
                best = value;
                trayIndex = piece;
                anchor = at;
            }
            return trayIndex >= 0;
        }

        /// <summary>Найкраща цінність продовження з цього поля для фігур, що лишились.</summary>
        private float Search(Board board, PieceDef[] tray, bool[] used, int depth)
        {
            var count = Candidates(board, tray, used, depth, noisy: false);
            if (count == 0)
                return Final(board) - (AnyLeft(tray, used) ? 1000f : 0f); // фігура, що не влізла, — смерть
            var best = float.NegativeInfinity;
            for (var c = 0; c < count; c++)
            {
                var (_, piece, at, lines, pure) = _top[depth][c];
                Apply(board, _boards[depth + 1], tray[piece], at);
                used[piece] = true;
                var value = Step(lines, pure) + Search(_boards[depth + 1], tray, used, depth + 1);
                used[piece] = false;
                if (value > best)
                    best = value;
            }
            return best;
        }

        private static bool AnyLeft(PieceDef[] tray, bool[] used)
        {
            for (var i = 0; i < tray.Length; i++)
                if (!used[i] && !tray[i].IsEmpty)
                    return true;
            return false;
        }

        /// <summary>Keep найкращих розміщень серед фігур, що лишились, за жадібною оцінкою; 0 — нічого не влазить або фігур немає.</summary>
        private int Candidates(Board board, PieceDef[] tray, bool[] used, int depth, bool noisy)
        {
            var top = _top[depth];
            var count = 0;
            var scratch = _boards[4];
            for (var i = 0; i < tray.Length; i++)
            {
                if (used[i] || tray[i].IsEmpty)
                    continue;
                var shape = tray[i].Shape!;
                for (var y = 0; y <= board.Height - shape.Height; y++)
                    for (var x = 0; x <= board.Width - shape.Width; x++)
                    {
                        var at = new GridPos(x, y);
                        if (!PlacementRules.CanPlace(board, shape, at))
                            continue;
                        Apply(board, scratch, tray[i], at);
                        var score = Step(_lastLines, _lastPure) + Final(scratch);
                        if (count == Keep && score <= top[Keep - 1].score)
                            continue;
                        var slot = count < Keep ? count++ : Keep - 1;
                        while (slot > 0 && top[slot - 1].score < score)
                        {
                            top[slot] = top[slot - 1];
                            slot--;
                        }
                        top[slot] = (score, i, at, _lastLines, _lastPure);
                    }
            }
            return count;
        }

        private int _lastLines;
        private int _lastPure;

        /// <summary>Ставить фігуру на копію поля й зриває лінії; скільки ліній і чистих — у _lastLines/_lastPure.</summary>
        private void Apply(Board from, Board into, PieceDef piece, GridPos at)
        {
            into.CopyFrom(from);
            var shape = piece.Shape!;
            for (var c = 0; c < shape.Cells.Length; c++)
                into[new GridPos(at.X + shape.Cells[c].X, at.Y + shape.Cells[c].Y)] = piece.Color;
            _lines.Clear();
            PlacementRules.CollectFullLines(into, _lines);
            _lastPure = 0;
            for (var l = 0; l < _lines.Count; l++)
                if (LineResolver.Resolve(into, _lines[l], _balance!).IsPure)
                    _lastPure++;
            for (var l = 0; l < _lines.Count; l++)
            {
                var length = LineResolver.LengthOf(into, _lines[l].Kind);
                for (var c = 0; c < length; c++)
                    into[LineResolver.CellAt(_lines[l], c)] = Board.Empty;
            }
            _lastLines = _lines.Count;
        }

        private float Step(int lines, int pure) => _weights.LineCleared * lines + _weights.PureLine * pure;

        private float Final(Board board)
        {
            var frag = 0;
            for (var y = 0; y < board.Height; y++)
                for (var x = 0; x < board.Width; x++)
                {
                    if (board[x, y] != Board.Empty)
                        continue;
                    frag += Blocked(board, x + 1, y) + Blocked(board, x - 1, y) + Blocked(board, x, y + 1) + Blocked(board, x, y - 1);
                }
            return _weights.EmptyCell * board.CountEmpty() - _weights.Fragmentation * frag;
        }

        private static int Blocked(Board board, int x, int y) =>
            x < 0 || x >= board.Width || y < 0 || y >= board.Height || board[x, y] != Board.Empty ? 1 : 0;
    }
}

namespace InkFlow.Sim
{
    /// <summary>
    /// Що лоток дозволяє: скільки ліній найбільше можна зірвати ОДНИМ ходом, ставлячи фігури лотка в
    /// якомусь порядку, і чи можна спорожнити поле. Пошук променем (на кожному кроці — Keep полів із найбільшою кількістю ліній
    /// й найменшою зайнятістю) — оцінка знизу, повний перебір був би в сотні разів дорожчим.
    /// </summary>
    public sealed class TrayPotential
    {
        private const int Keep = 6;
        private readonly List<Line> _lines = new List<Line>(16);
        private readonly List<(Board board, int lines, bool[] used)> _beam = new List<(Board, int, bool[])>();
        private readonly List<(Board board, int lines, bool[] used)> _next = new List<(Board, int, bool[])>();

        public void Evaluate(Board board, PieceDef[] tray, out int bestMove, out bool canEmpty)
        {
            bestMove = 0;
            canEmpty = false;
            _beam.Clear();
            _beam.Add((board.Clone(), 0, new bool[tray.Length]));
            for (var depth = 0; depth < tray.Length; depth++)
            {
                _next.Clear();
                foreach (var (from, lines, used) in _beam)
                    for (var i = 0; i < tray.Length; i++)
                    {
                        if (used[i] || tray[i].IsEmpty)
                            continue;
                        var shape = tray[i].Shape!;
                        for (var y = 0; y <= from.Height - shape.Height; y++)
                            for (var x = 0; x <= from.Width - shape.Width; x++)
                            {
                                if (!PlacementRules.CanPlace(from, shape, new GridPos(x, y)))
                                    continue;
                                var next = from.Clone();
                                foreach (var c in shape.Cells)
                                    next[new GridPos(x + c.X, y + c.Y)] = MasterPalette.FirstFill;
                                _lines.Clear();
                                PlacementRules.CollectFullLines(next, _lines);
                                foreach (var line in _lines)
                                    for (var k = 0; k < LineResolver.LengthOf(next, line.Kind); k++)
                                        next[LineResolver.CellAt(line, k)] = Board.Empty;
                                var total = lines + _lines.Count;
                                if (_lines.Count > bestMove)
                                    bestMove = _lines.Count;
                                if (_lines.Count > 0 && next.CountEmpty() == next.CellCount)
                                    canEmpty = true;
                                var mark = (bool[])used.Clone();
                                mark[i] = true;
                                _next.Add((next, total, mark));
                            }
                    }
                if (_next.Count == 0)
                    return;
                _next.Sort((a, b) => a.lines != b.lines ? b.lines.CompareTo(a.lines) : b.board.CountEmpty().CompareTo(a.board.CountEmpty()));
                _beam.Clear();
                for (var k = 0; k < _next.Count && k < Keep; k++)
                    _beam.Add(_next[k]);
            }
        }
    }
}
