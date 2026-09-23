using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Ваги евристики бота. Виведені прогонником, а не вигадані (§10).</summary>
    public readonly struct BotWeights
    {
        public BotWeights(float canvasCell, float leftoverPaint, float emptyCell, float fragmentation,
            float pureLine, float purityPotential)
        {
            CanvasCell = canvasCell;
            LeftoverPaint = leftoverPaint;
            EmptyCell = emptyCell;
            Fragmentation = fragmentation;
            PureLine = pureLine;
            PurityPotential = purityPotential;
        }

        /// <summary>Цінність однієї клітинки полотна, яку хід зафарбує просто зараз.</summary>
        public float CanvasCell { get; }

        /// <summary>Цінність одиниці фарби, що лишилась у баку й потрібна активній зоні.</summary>
        public float LeftoverPaint { get; }

        /// <summary>Цінність вільної клітинки поля — нею бот платить за живість партії.</summary>
        public float EmptyCell { get; }

        /// <summary>Штраф за подряпаність поля: вільна клітинка без вільних сусідів — майже мертва.</summary>
        public float Fragmentation { get; }

        /// <summary>Окрема надбавка за чисту лінію понад її фарбу — бот має ЦІЛИТИСЬ у чистоту.</summary>
        public float PureLine { get; }

        /// <summary>
        /// Наскільки лінії поля близькі до одноколірності. Без цього члена бот бачить чистоту
        /// лише в момент зриву — тобто ніколи її не готує, а саме підготовка і є грою.
        /// </summary>
        public float PurityPotential { get; }

        public static readonly BotWeights Default = new BotWeights(
            canvasCell: 10f, leftoverPaint: 0.6f, emptyCell: 1.0f, fragmentation: 0.35f,
            pureLine: 4f, purityPotential: 0.1f);
    }

    /// <summary>
    /// Жадібний бот (§10). Серед усіх валідних розміщень бере те, що максимізує
    /// «фарба, яка справді ляже на полотно» мінус «штраф за фрагментацію поля».
    ///
    /// Оцінка користується ТИМИ САМИМИ функціями правил, що й гра
    /// (<see cref="PlacementRules"/>, <see cref="LineResolver"/>): копія правил у ботові
    /// означала б, що прогонник балансує неіснуючу гру. Наближення тут одне — скільки
    /// клітинок зони встигне лягти, рахується на трьох цілих, без повного стану полотна.
    ///
    /// Це не «розумний» бот. Він і не має бути розумним: коридори §13 описують гру
    /// звичайного гравця, а не оптимальну гру.
    /// </summary>
    public sealed class GreedyBot
    {
        private readonly BotWeights _weights;
        private readonly List<Line> _lines = new List<Line>(16);
        private readonly int[] _tanks = new int[InkColors.Count];
        private readonly IRandomSource? _noiseSource;
        private readonly float _noise;
        private Board? _scratch;

        /// <summary>
        /// <paramref name="noiseSource"/> і <paramref name="noise"/> роблять бота нерівним самому собі.
        /// Це не косметика: сід рівня фіксований (§4), тож без шуму тисяча прогонів була б
        /// тисячею копій одного прогону, і «проходиться в 85%» не мало б сенсу. Шум моделює
        /// не випадковість гри, а різних гравців.
        /// </summary>
        public GreedyBot(BotWeights weights, IRandomSource? noiseSource = null, float noise = 0f)
        {
            _weights = weights;
            _noiseSource = noiseSource;
            _noise = noise;
        }

        public GreedyBot() : this(BotWeights.Default)
        {
        }

        /// <summary>Найкраще розміщення або false, якщо жодне не влазить (це і є програш).</summary>
        public bool TryChooseMove(PlayfieldSession session, out int trayIndex, out GridPos anchor)
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

                        var score = Evaluate(session, shape, candidate, piece.Color);
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

        private float Evaluate(PlayfieldSession session, PieceShape shape, GridPos anchor, InkColor color)
        {
            var board = session.Board;
            var scratch = _scratch!;
            scratch.CopyFrom(board);

            for (var i = 0; i < shape.Cells.Length; i++)
                scratch[new GridPos(anchor.X + shape.Cells[i].X, anchor.Y + shape.Cells[i].Y)] = color;

            _lines.Clear();
            PlacementRules.CollectFullLines(scratch, _lines);

            for (var i = 0; i < InkColors.Count; i++)
                _tanks[i] = session.Tanks[InkColors.FromIndex(i)];

            var pureLines = 0;
            var overflow = 0;
            if (_lines.Count > 0)
            {
                var multiplier = session.Balance.ComboFor(_lines.Count);
                for (var i = 0; i < _lines.Count; i++)
                {
                    var yield = LineResolver.Resolve(scratch, _lines[i], session.Tanks, session.Balance);
                    if (yield.IsPure)
                        pureLines++;
                    if (yield.Color == InkColor.None)
                        continue;

                    var amount = LineResolver.ApplyCombo(yield.Amount, multiplier);
                    var t = InkColors.IndexOf(yield.Color);
                    _tanks[t] += amount;
                    if (_tanks[t] <= session.Tanks.Cap)
                        continue;
                    overflow += _tanks[t] - session.Tanks.Cap;
                    _tanks[t] = session.Tanks.Cap;
                }

                for (var i = 0; i < _lines.Count; i++)
                {
                    var line = _lines[i];
                    var length = LineResolver.LengthOf(scratch, line.Kind);
                    for (var c = 0; c < length; c++)
                        scratch[LineResolver.CellAt(line, c)] = InkColor.None;
                }
            }

            var recipe = session.Canvas.ActiveRecipe;
            var cellsPainted = 0;
            var leftover = 0f;

            if (recipe.TotalUnits > 0)
            {
                var zone = session.Canvas.ActiveZone;
                var room = session.Canvas.Definition.ZoneAt(zone).CellCount - session.Canvas.PaintedIn(zone);
                cellsPainted = Affordable(recipe);
                if (cellsPainted > room)
                    cellsPainted = room;

                _tanks[0] -= cellsPainted * recipe.Blue;
                _tanks[1] -= cellsPainted * recipe.Red;
                _tanks[2] -= cellsPainted * recipe.Yellow;

                // Лишок цінний рівно настільки, наскільки він потрібен цій же зоні:
                // сорок одиниць червоного при синій зоні — це майбутня муть, а не запас.
                for (var i = 0; i < InkColors.Count; i++)
                    if (recipe.UnitsOf(InkColors.FromIndex(i)) > 0)
                        leftover += _tanks[i];
            }

            var murkCells = (session.Tanks.Murk + overflow) / session.Balance.MurkPerCell;
            var canvasValue = cellsPainted + murkCells * session.Balance.UnderpaintWeight;

            return _weights.CanvasCell * canvasValue
                   + _weights.LeftoverPaint * leftover
                   + _weights.PureLine * pureLines
                   + _weights.PurityPotential * PurityPotential(scratch)
                   + _weights.EmptyCell * scratch.CountEmpty()
                   - _weights.Fragmentation * Fragmentation(scratch);
        }

        /// <summary>Скільки клітинок рецепта покривають поточні баки — обмежувальний колір і вирішує.</summary>
        private int Affordable(in MixRecipe recipe)
        {
            var best = int.MaxValue;
            if (recipe.Blue > 0) best = Math.Min(best, _tanks[0] / recipe.Blue);
            if (recipe.Red > 0) best = Math.Min(best, _tanks[1] / recipe.Red);
            if (recipe.Yellow > 0) best = Math.Min(best, _tanks[2] / recipe.Yellow);
            return best == int.MaxValue ? 0 : best;
        }

        /// <summary>
        /// Наскільки поле «готове» до чистих ліній: за кожну лінію рахується перевага
        /// домінантного кольору над рештою. Одноколірний недобудований рядок дає плюс,
        /// той самий рядок із чужою клітинкою — мінус.
        ///
        /// Це заміна пошуку в глибину. Без неї бот бачить чисту лінію лише коли вона вже
        /// склалась випадково, і головна ідея гри в метриках виглядає мертвою — саме це
        /// й показав перший прогін (1,5% чистих ліній).
        /// </summary>
        private static int PurityPotential(Board board)
        {
            var score = 0;
            for (var y = 0; y < board.Height; y++)
                score += LineBalance(board, LineKind.Row, y);
            for (var x = 0; x < board.Width; x++)
                score += LineBalance(board, LineKind.Column, x);
            return score;
        }

        private static int LineBalance(Board board, LineKind kind, int index)
        {
            var length = kind == LineKind.Row ? board.Width : board.Height;
            var blue = 0;
            var red = 0;
            var yellow = 0;
            for (var i = 0; i < length; i++)
            {
                var color = kind == LineKind.Row ? board[i, index] : board[index, i];
                if (color == InkColor.Blue) blue++;
                else if (color == InkColor.Red) red++;
                else if (color == InkColor.Yellow) yellow++;
            }

            var filled = blue + red + yellow;
            if (filled == 0)
                return 0;

            var max = blue > red ? blue : red;
            if (yellow > max) max = yellow;

            // Тільки додатна частина. Сума зі знаком не працює: розкладка «кольорові смуги
            // по рядах» дає плюс на рядах і рівно такий самий мінус на стовпцях, і бот
            // не бачить різниці між нею й кашею. А гравцю потрібні НЕ всі лінії чистими,
            // а хоч якісь — тому й рахуємо тільки те, що вже схоже на чисту лінію.
            var balance = 2 * max - filled;
            if (balance <= 0)
                return 0;

            // Ближча до повної лінія цінніша: три сині клітинки в порожньому ряду —
            // ще нічого, шість — уже план.
            return balance * filled;
        }

        /// <summary>
        /// Подряпаність поля: скільки «стінок» у вільних клітинок. Вільна клітинка,
        /// оточена зайнятими, коштує стільки ж, скільки зайнята, — просто ще не знає про це.
        /// </summary>
        private static int Fragmentation(Board board)
        {
            var penalty = 0;
            for (var y = 0; y < board.Height; y++)
                for (var x = 0; x < board.Width; x++)
                {
                    if (board[x, y] != InkColor.None)
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
            return board[x, y] == InkColor.None ? 0 : 1;
        }
    }
}
