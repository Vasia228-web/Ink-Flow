using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Ваги евристики бота. Стартові — вгадані; правляться за прогонами, а не на око.</summary>
    public readonly struct BotWeights
    {
        public BotWeights(float lineCleared, float pureLine, float emptyCell, float fragmentation,
            float purityPotential, float wantedBias = 0f)
        {
            LineCleared = lineCleared;
            PureLine = pureLine;
            EmptyCell = emptyCell;
            Fragmentation = fragmentation;
            PurityPotential = purityPotential;
            WantedBias = wantedBias;
        }

        /// <summary>
        /// Наскільки бот ЦІЛИТЬСЯ в колір, потрібний картинці: множник до потенціалу чистоти
        /// ліній, де домінує бажаний пігмент. 0 — не бачить картинки взагалі.
        /// </summary>
        public float WantedBias { get; }

        /// <summary>Цінність зірваної лінії.</summary>
        public float LineCleared { get; }

        /// <summary>Надбавка за чисту лінію понад звичайну — бот має ЦІЛИТИСЬ у чистоту.</summary>
        public float PureLine { get; }

        /// <summary>Цінність вільної клітинки поля — нею бот платить за живість партії.</summary>
        public float EmptyCell { get; }

        /// <summary>Штраф за подряпаність поля: вільна клітинка без вільних сусідів — майже мертва.</summary>
        public float Fragmentation { get; }

        /// <summary>
        /// Наскільки лінії поля близькі до одноколірності. Без цього члена бот бачить чистоту
        /// лише в момент зриву — тобто ніколи її не готує, а саме підготовка і є грою.
        /// </summary>
        public float PurityPotential { get; }

        public static readonly BotWeights Default = new BotWeights(
            lineCleared: 12f, pureLine: 10f, emptyCell: 1f, fragmentation: 0.35f, purityPotential: 0.12f);
    }

    /// <summary>
    /// Жадібний бот на РЕАЛЬНИХ правилах Core (<see cref="PlacementRules"/>,
    /// <see cref="LineResolver"/>): копія правил у ботові означала б, що прогонник
    /// балансує неіснуючу гру.
    ///
    /// Це не «розумний» бот і не має ним бути: він моделює звичайного гравця. Шум
    /// робить його нерівним самому собі — інакше тисяча прогонів з одним сідом були б
    /// тисячею копій одного прогону.
    /// </summary>
    public sealed class RunBot
    {
        private readonly BotWeights _weights;
        private readonly List<Line> _lines = new List<Line>(16);
        private readonly IRandomSource? _noiseSource;
        private readonly float _noise;
        private Board? _scratch;

        public RunBot(BotWeights weights, IRandomSource? noiseSource = null, float noise = 0f)
        {
            _weights = weights;
            _noiseSource = noiseSource;
            _noise = noise;
        }

        public RunBot() : this(BotWeights.Default)
        {
        }

        /// <summary>Найкраще розміщення або false, якщо жодне не влазить (це і є програш).</summary>
        public bool TryChooseMove(RunSession session, out int trayIndex, out GridPos anchor)
        {
            if (session is null) throw new ArgumentNullException(nameof(session));

            var board = session.Board;
            if (_scratch is null || _scratch.Width != board.Width || _scratch.Height != board.Height)
                _scratch = new Board(board.Width, board.Height);

            trayIndex = -1;
            anchor = default;
            var bestScore = float.NegativeInfinity;

            var tray = session.TrayPieces;
            for (var i = 0; i < tray.Length; i++)
            {
                var piece = tray[i];
                if (piece.IsEmpty)
                    continue;

                var shape = piece.Shape!;
                for (var y = 0; y <= board.Height - shape.Height; y++)
                    for (var x = 0; x <= board.Width - shape.Width; x++)
                    {
                        var candidate = new GridPos(x, y);
                        if (!PlacementRules.CanPlace(board, shape, candidate))
                            continue;

                        var score = Evaluate(session, shape, candidate, piece.Pigment);
                        if (_noiseSource is not null && _noise > 0f)
                            score += _noise * (_noiseSource.Next(1024) / 1024f);
                        if (score <= bestScore)
                            continue;

                        bestScore = score;
                        trayIndex = i;
                        anchor = candidate;
                    }
            }

            return trayIndex >= 0;
        }

        private float Evaluate(RunSession session, PieceShape shape, GridPos anchor, Pigment pigment)
        {
            var scratch = _scratch!;
            scratch.CopyFrom(session.Board);

            for (var i = 0; i < shape.Cells.Length; i++)
                scratch[new GridPos(anchor.X + shape.Cells[i].X, anchor.Y + shape.Cells[i].Y)] = pigment;

            _lines.Clear();
            PlacementRules.CollectFullLines(scratch, _lines);

            session.WantedPigments(out var wantedA, out var wantedB);
            var pureLines = 0;
            var wantedLines = 0;
            for (var i = 0; i < _lines.Count; i++)
            {
                var yield = LineResolver.Resolve(scratch, _lines[i], session.Balance);
                if (!yield.IsPure)
                    continue;
                pureLines++;
                if (yield.Pigment == wantedA || yield.Pigment == wantedB)
                    wantedLines++;
            }

            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                var length = LineResolver.LengthOf(scratch, line.Kind);
                for (var c = 0; c < length; c++)
                    scratch[LineResolver.CellAt(line, c)] = Pigment.None;
            }

            var potential = PurityPotential(scratch, wantedA, wantedB, out var wantedPotential);

            // Бажаний колір: чиста лінія ТОГО кольору цінніша і в момент зриву, і як план.
            return _weights.LineCleared * _lines.Count
                   + _weights.PureLine * (pureLines + _weights.WantedBias * wantedLines)
                   + _weights.PurityPotential * (potential + _weights.WantedBias * wantedPotential)
                   + _weights.EmptyCell * scratch.CountEmpty()
                   - _weights.Fragmentation * Fragmentation(scratch);
        }

        /// <summary>
        /// Наскільки поле «готове» до чистих ліній: за кожну лінію — перевага домінантного
        /// пігменту над рештою, і лише додатна частина: гравцю потрібні НЕ всі лінії
        /// чистими, а хоч якісь. Окремо — та сама сума лише по лініях бажаних пігментів.
        /// </summary>
        private static int PurityPotential(Board board, Pigment wantedA, Pigment wantedB, out int wanted)
        {
            var score = 0;
            wanted = 0;
            for (var y = 0; y < board.Height; y++)
            {
                var balance = LineBalance(board, LineKind.Row, y, out var dominant);
                score += balance;
                if (dominant == wantedA || dominant == wantedB) wanted += balance;
            }
            for (var x = 0; x < board.Width; x++)
            {
                var balance = LineBalance(board, LineKind.Column, x, out var dominant);
                score += balance;
                if (dominant == wantedA || dominant == wantedB) wanted += balance;
            }
            return score;
        }

        private static int LineBalance(Board board, LineKind kind, int index, out Pigment dominant)
        {
            dominant = Pigment.None;
            var length = kind == LineKind.Row ? board.Width : board.Height;
            var blue = 0;
            var red = 0;
            var yellow = 0;
            for (var i = 0; i < length; i++)
            {
                var pigment = kind == LineKind.Row ? board[i, index] : board[index, i];
                if (pigment == Pigment.Blue) blue++;
                else if (pigment == Pigment.Red) red++;
                else if (pigment == Pigment.Yellow) yellow++;
            }

            var filled = blue + red + yellow;
            if (filled == 0)
                return 0;

            var max = blue;
            dominant = Pigment.Blue;
            if (red > max) { max = red; dominant = Pigment.Red; }
            if (yellow > max) { max = yellow; dominant = Pigment.Yellow; }

            var balance = 2 * max - filled;
            if (balance <= 0)
                return 0;

            // Ближча до повної лінія цінніша: три сині клітинки в порожньому ряду —
            // ще нічого, шість — уже план.
            return balance * filled;
        }

        /// <summary>Подряпаність поля: скільки «стінок» у вільних клітинок.</summary>
        private static int Fragmentation(Board board)
        {
            var penalty = 0;
            for (var y = 0; y < board.Height; y++)
                for (var x = 0; x < board.Width; x++)
                {
                    if (board[x, y] != Pigment.None)
                        continue;
                    penalty += Blocked(board, x + 1, y) + Blocked(board, x - 1, y)
                               + Blocked(board, x, y + 1) + Blocked(board, x, y - 1);
                }

            return penalty;
        }

        private static int Blocked(Board board, int x, int y)
        {
            if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
                return 1;
            return board[x, y] == Pigment.None ? 0 : 1;
        }
    }
}
