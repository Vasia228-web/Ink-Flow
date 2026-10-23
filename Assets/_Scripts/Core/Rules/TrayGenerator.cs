using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Мішок фігур (документ §2): «не чистий рандом — гра дивиться на форму вільного
    /// місця й підвищує ймовірність фігур, які туди поміщаються. Вона не підказує,
    /// куди класти, але не підсовує завідомо неможливий набір».
    ///
    /// Три правила, кожне під тестом:
    ///  (A) вага форми росте з кількістю її позицій на полі (<see cref="BalanceData.BagBias"/>);
    ///  (B) набір, у якому не влазить жодна фігура, перегенеровується — спершу як є,
    ///      потім із меншими фігурами (60 спроб, після 40 — стеля 3, далі 20 спроб двоклітинковими);
    ///  (C) якщо не влазить навіть двоклітинкова — місця справді немає, і це чесний
    ///      програш, а не вина мішка.
    ///
    /// Щедрість (як у Block Blast) — поверх правил, кожна з власним шансом у <see cref="BalanceData"/>:
    ///  • форми, що просто зараз зривають лінії, важать більше (<see cref="BalanceData.LineFitBoost"/>);
    ///  • «очистити поле»: на майже порожньому полі мішок шукає фігури, що його спорожнять;
    ///  • «ідеальний»: три фігури, що поспіль зривають багато ліній;
    ///  • «рятувальний»: на тісному полі перша фігура зриває найбільше, решта малі.
    /// Особливий лоток будується лише з фігур, що справді лягають на це поле, тож правило (B)
    /// він не порушує; не знайшовся — звичайний лоток. Складні лотки лишаються: програти можна.
    ///
    /// Кольори (§5): лише кольори заливки поточної картинки, з вагою за тим, скільки
    /// пікселів кожного кольору ще лишилось. Прогресії кольорів немає — їх диктує картинка.
    /// </summary>
    public sealed class TrayGenerator
    {
        private readonly PieceCatalogData _catalog;
        private readonly BalanceData _balance;
        private readonly int[] _fits;
        private readonly int[] _lineFits;
        private readonly float[] _weights;
        private readonly List<Line> _probe = new List<Line>(16);

        // Пошук ідеального лотка — промінь найкращих полів; усе виділено наперед, партія не алокує.
        private Board[] _beam = Array.Empty<Board>();
        private Board[] _next = Array.Empty<Board>();
        private int[][] _beamPath = Array.Empty<int[]>();
        private int[][] _nextPath = Array.Empty<int[]>();
        private float[] _beamScore = Array.Empty<float>();
        private float[] _nextScore = Array.Empty<float>();
        private int[] _beamLines = Array.Empty<int>();
        private int[] _nextLines = Array.Empty<int>();
        private Board? _scratch;
        private IReadOnlyList<byte> _colors = Array.Empty<byte>();
        private IReadOnlyList<int> _colorWeights = Array.Empty<int>();

        public TrayGenerator(PieceCatalogData catalog, BalanceData balance)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _fits = new int[catalog.Count];
            _lineFits = new int[catalog.Count];
            _weights = new float[catalog.Count];
        }

        /// <summary>Скільки лотків видано за партію — це і є раунд для прогресії.</summary>
        public int TraysIssued { get; private set; }

        /// <summary>Скільки разів довелось зменшувати фігури, щоб хоч одна влізла.</summary>
        public int RescuesUsed { get; private set; }

        /// <summary>Скільки особливих лотків видано (діагностика прогонів; у зліпок не йде).</summary>
        public int ClearTraysGiven { get; private set; }
        public int PerfectTraysGiven { get; private set; }
        public int RescueTraysGiven { get; private set; }

        public void Reset()
        {
            TraysIssued = 0;
            RescuesUsed = 0;
            ClearTraysGiven = PerfectTraysGiven = RescueTraysGiven = 0;
        }

        /// <summary>Лічильники зі зліпка забігу (§9): раунд прогресії має продовжитись, а не почати з нуля.</summary>
        public void Restore(int traysIssued, int rescuesUsed)
        {
            TraysIssued = Math.Max(0, traysIssued);
            RescuesUsed = Math.Max(0, rescuesUsed);
        }

        /// <summary>
        /// Наповнює лоток. <paramref name="colors"/> — кольори, які ще потрібні картинці,
        /// <paramref name="colorWeights"/> — скільки пікселів кожного лишилось (вага).
        /// Повертає true, якщо довелось зменшувати фігури (діагностика). Якщо не влазить
        /// нічого навіть після рятувальних спроб — лоток лишається останнім згенерованим,
        /// і сесія чесно фіксує програш.
        /// </summary>
        public bool Fill(PieceDef[] tray, Board board, IRandomSource random,
            IReadOnlyList<byte> colors, IReadOnlyList<int> colorWeights)
        {
            if (tray is null) throw new ArgumentNullException(nameof(tray));
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (random is null) throw new ArgumentNullException(nameof(random));
            if (colors is null || colors.Count == 0) throw new ArgumentException("Лоток без кольорів.", nameof(colors));
            if (colorWeights is null || colorWeights.Count != colors.Count) throw new ArgumentException("Ваги не збігаються з кольорами.", nameof(colorWeights));

            _colors = colors;
            _colorWeights = colorWeights;

            TraysIssued++;
            var tier = _balance.TierFor(TraysIssued);
            CountFits(board);

            var cap = _balance.SizeCapFor(tier);
            var occupied = board.CellCount - board.CountEmpty();
            if (occupied > 0 && occupied <= _balance.ClearTrayMaxCells && Roll(_balance.ClearTrayChance, random)
                && TryPlannedTray(tray, board, cap, tier, clear: true, random))
            {
                ClearTraysGiven++;
                return false;
            }
            if (Roll(_balance.PerfectTrayChance, random) && TryPlannedTray(tray, board, cap, tier, clear: false, random))
            {
                PerfectTraysGiven++;
                return false;
            }
            if (occupied >= _balance.RescueTrayFill * board.CellCount && Roll(_balance.RescueTrayChance, random)
                && TryRescueTray(tray, board, cap, random))
            {
                RescueTraysGiven++;
                return false;
            }

            for (var attempt = 0; attempt < _balance.MaxTrayAttempts; attempt++)
            {
                var sizeCap = attempt < _balance.TrayShrinkAfterAttempts ? cap : Math.Min(cap, 3);
                Build(tray, sizeCap, tier, random);
                if (PlacementRules.AnyPieceFits(board, tray))
                    return false;
            }

            RescuesUsed++;
            var rescueCap = Math.Min(cap, _balance.MinPieceSize);
            for (var attempt = 0; attempt < _balance.TrayRescueAttempts; attempt++)
            {
                Build(tray, rescueCap, tier, random);
                if (PlacementRules.AnyPieceFits(board, tray))
                    return true;
            }

            // Останній рубіж, і він детермінований: перша найменша форма, що влазить,
            // стає першою фігурою. Випадковість тут була б брехнею — «не влазить нічого»
            // мусить означати, що не влазить нічого, а не що не пощастило двадцять разів.
            for (var i = 0; i < _catalog.Count; i++)
            {
                var shape = _catalog[i];
                if (shape.Size > rescueCap || !PlacementRules.AnyFit(board, shape))
                    continue;
                tray[0] = new PieceDef(shape, tray[0].IsEmpty ? PickColor(random) : tray[0].Color);
                return true;
            }

            return true;
        }

        private void Build(PieceDef[] tray, int sizeCap, int tier, IRandomSource random)
        {
            for (var k = 0; k < tray.Length; k++)
                tray[k] = new PieceDef(PickShape(sizeCap, tier, random), PickColor(random));
            Shuffle(tray, random);
        }

        private void CountFits(Board board)
        {
            var lines = _balance.LineFitBoost > 0f || _balance.RescueTrayChance > 0f;
            for (var i = 0; i < _catalog.Count; i++)
            {
                _fits[i] = PlacementRules.CountFits(board, _catalog[i]);
                _lineFits[i] = lines ? BestLines(board, _catalog[i]) : 0;
            }
        }

        /// <summary>Шанс 0 не витрачає кидок — тоді вимкнена щедрість не зсуває випадковість решти мішка.</summary>
        private static bool Roll(float chance, IRandomSource random) =>
            chance > 0f && random.Next(1 << 20) < chance * (1 << 20);

        /// <summary>Скільки ліній форма зірве найкращим місцем на цьому полі.</summary>
        private int BestLines(Board board, PieceShape shape)
        {
            var best = 0;
            for (var y = 0; y <= board.Height - shape.Height; y++)
                for (var x = 0; x <= board.Width - shape.Width; x++)
                {
                    PlacementRules.PreviewLines(board, shape, new GridPos(x, y), MasterPalette.FirstFill, _probe);
                    if (_probe.Count > best)
                        best = _probe.Count;
                }
            return best;
        }

        /// <summary>Рятувальний лоток: перша фігура — одна з тих, що зривають найбільше ліній; решта — до трьох клітинок.</summary>
        private bool TryRescueTray(PieceDef[] tray, Board board, int cap, IRandomSource random)
        {
            var best = 0;
            var count = 0;
            for (var i = 0; i < _catalog.Count; i++)
            {
                if (_catalog[i].Size > cap || _lineFits[i] == 0 || _lineFits[i] < best)
                    continue;
                if (_lineFits[i] > best)
                {
                    best = _lineFits[i];
                    count = 0;
                }
                count++;
            }
            if (count == 0)
                return false;

            var pick = random.Next(count);
            for (var i = 0; i < _catalog.Count; i++)
            {
                if (_catalog[i].Size > cap || _lineFits[i] != best || pick-- > 0)
                    continue;
                tray[0] = new PieceDef(_catalog[i], PickColor(random));
                break;
            }
            for (var k = 1; k < tray.Length; k++)
                tray[k] = new PieceDef(PickShape(Math.Min(cap, 3), 0, random), PickColor(random));
            Shuffle(tray, random);
            return true;
        }

        /// <summary>
        /// Ідеальний лоток (clear = false) — фігури поспіль зривають щонайменше PerfectTrayMinLines;
        /// «очистити поле» (clear = true) — після них поле порожнє. Пошук променем: на кожному кроці
        /// лишаються TrayPlanBeam найкращих полів; кожна фігура шляху лягає на поле свого кроку,
        /// отже перша влазить на справжнє поле. Порядок, у якому їх ставити, гравець шукає сам.
        /// </summary>
        private bool TryPlannedTray(PieceDef[] tray, Board board, int cap, int tier, bool clear, IRandomSource random)
        {
            EnsureBeam(board, tray.Length);
            var width = _beam.Length;
            _beam[0].CopyFrom(board);
            _beamLines[0] = 0;
            var count = 1;
            var depth = 0;
            var done = false;

            while (depth < tray.Length && !done)
            {
                var nextCount = 0;
                for (var b = 0; b < count; b++)
                    for (var i = 0; i < _catalog.Count; i++)
                    {
                        var shape = _catalog[i];
                        if (shape.Size > cap)
                            continue;
                        // Нічия між рівними варіантами — випадкова, один кидок на форму.
                        var jitter = random.Next(256) / 1024f;
                        for (var y = 0; y <= board.Height - shape.Height; y++)
                            for (var x = 0; x <= board.Width - shape.Width; x++)
                            {
                                var at = new GridPos(x, y);
                                if (!PlacementRules.CanPlace(_beam[b], shape, at))
                                    continue;
                                var lines = PlaceAndClear(_beam[b], shape, at);
                                var total = _beamLines[b] + lines;
                                var score = clear
                                    ? total - 10f * Orphans(_scratch!) - (_scratch!.CellCount - _scratch.CountEmpty())
                                    : 10f * total + shape.Size;
                                score += jitter;
                                if (nextCount == width && score <= _nextScore[width - 1])
                                    continue;
                                nextCount = Insert(nextCount, score, total, b, depth, i);
                            }
                    }

                if (nextCount == 0)
                    return false;
                (_beam, _next) = (_next, _beam);
                (_beamPath, _nextPath) = (_nextPath, _beamPath);
                (_beamScore, _nextScore) = (_nextScore, _beamScore);
                (_beamLines, _nextLines) = (_nextLines, _beamLines);
                count = nextCount;
                depth++;
                done = clear && _beam[0].CountEmpty() == _beam[0].CellCount;
            }

            if (clear ? !done : _beamLines[0] < _balance.PerfectTrayMinLines)
                return false;

            for (var k = 0; k < tray.Length; k++)
            {
                var shape = k < depth ? _catalog[_beamPath[0][k]] : PickShape(cap, tier, random);
                tray[k] = new PieceDef(shape, PickColor(random));
            }
            Shuffle(tray, random);
            return true;
        }

        /// <summary>
        /// «Сироти» — зайняті клітинки, чий рядок і стовпець заповнені менш ніж наполовину: щоб поле
        /// спорожніло, кожна клітинка мусить потрапити в зірвану лінію, а сирота до неї найдалі.
        /// </summary>
        private static int Orphans(Board board)
        {
            var half = (board.Width + 1) / 2;
            var orphans = 0;
            for (var y = 0; y < board.Height; y++)
            {
                var row = 0;
                for (var x = 0; x < board.Width; x++)
                    if (board[x, y] != Board.Empty)
                        row++;
                if (row == 0 || row >= half)
                    continue;
                for (var x = 0; x < board.Width; x++)
                {
                    if (board[x, y] == Board.Empty)
                        continue;
                    var column = 0;
                    for (var k = 0; k < board.Height; k++)
                        if (board[x, k] != Board.Empty)
                            column++;
                    if (column < (board.Height + 1) / 2)
                        orphans++;
                }
            }
            return orphans;
        }

        /// <summary>Фігура на копію поля, повні лінії зриваються; копія — у <see cref="_scratch"/>. Повертає скільки ліній.</summary>
        private int PlaceAndClear(Board from, PieceShape shape, GridPos at)
        {
            var scratch = _scratch!;
            scratch.CopyFrom(from);
            for (var c = 0; c < shape.Cells.Length; c++)
                scratch[new GridPos(at.X + shape.Cells[c].X, at.Y + shape.Cells[c].Y)] = MasterPalette.FirstFill;
            _probe.Clear();
            PlacementRules.CollectFullLines(scratch, _probe);
            for (var l = 0; l < _probe.Count; l++)
            {
                var length = LineResolver.LengthOf(scratch, _probe[l].Kind);
                for (var c = 0; c < length; c++)
                    scratch[LineResolver.CellAt(_probe[l], c)] = Board.Empty;
            }
            return _probe.Count;
        }

        /// <summary>Вставляє <see cref="_scratch"/> у відсортований промінь наступного кроку; найгірший випадає.</summary>
        private int Insert(int nextCount, float score, int lines, int parent, int depth, int shapeIndex)
        {
            var width = _next.Length;
            var last = nextCount < width ? nextCount : width - 1;
            var at = last;
            while (at > 0 && _nextScore[at - 1] < score)
                at--;

            var board = _next[last];
            var path = _nextPath[last];
            for (var k = last; k > at; k--)
            {
                _next[k] = _next[k - 1];
                _nextPath[k] = _nextPath[k - 1];
                _nextScore[k] = _nextScore[k - 1];
                _nextLines[k] = _nextLines[k - 1];
            }

            board.CopyFrom(_scratch!);
            Array.Copy(_beamPath[parent], path, depth);
            path[depth] = shapeIndex;
            _next[at] = board;
            _nextPath[at] = path;
            _nextScore[at] = score;
            _nextLines[at] = lines;
            return nextCount < width ? nextCount + 1 : nextCount;
        }

        private void EnsureBeam(Board board, int traySize)
        {
            var width = _balance.TrayPlanBeam;
            if (_beam.Length == width && _scratch != null && _scratch.Width == board.Width && _scratch.Height == board.Height
                && _beamPath[0].Length == traySize)
                return;
            _scratch = new Board(board.Width, board.Height);
            _beam = new Board[width];
            _next = new Board[width];
            _beamPath = new int[width][];
            _nextPath = new int[width][];
            for (var i = 0; i < width; i++)
            {
                _beam[i] = new Board(board.Width, board.Height);
                _next[i] = new Board(board.Width, board.Height);
                _beamPath[i] = new int[traySize];
                _nextPath[i] = new int[traySize];
            }
            _beamScore = new float[width];
            _nextScore = new float[width];
            _beamLines = new int[width];
            _nextLines = new int[width];
        }

        /// <summary>Тасування Фішера — Йетса: без нього перша комірка завжди мала б «свою» роль.</summary>
        private static void Shuffle(PieceDef[] tray, IRandomSource random)
        {
            for (var i = tray.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (tray[i], tray[j]) = (tray[j], tray[i]);
            }
        }

        private PieceShape PickShape(int sizeCap, int tier, IRandomSource random)
        {
            var total = 0f;
            for (var i = 0; i < _catalog.Count; i++)
            {
                var shape = _catalog[i];
                var w = 0f;
                if (shape.Size <= sizeCap)
                {
                    w = _balance.SizeWeight(shape.Size, tier) * shape.Weight;
                    if (w > 0f && _balance.BagBias > 0f)
                        w *= (float)Math.Pow(_balance.BagFitOffset + _fits[i], _balance.BagBias);
                    if (w > 0f && _lineFits[i] > 0)
                    {
                        var lines = Math.Min(_lineFits[i], 3);
                        w *= 1f + _balance.LineFitBoost * lines * lines;
                    }
                }

                _weights[i] = w;
                total += w;
            }

            if (total <= 0f)
                return _catalog.Smallest;

            // Ціле джерело випадковості → дріб у [0, 1): так само, як робить бот.
            var roll = random.Next(1 << 20) / (float)(1 << 20) * total;
            for (var i = 0; i < _catalog.Count; i++)
            {
                roll -= _weights[i];
                if (roll < 0f && _weights[i] > 0f)
                    return _catalog[i];
            }

            for (var i = _catalog.Count - 1; i >= 0; i--)
                if (_weights[i] > 0f)
                    return _catalog[i];
            return _catalog.Smallest;
        }

        /// <summary>Колір — зважено за пікселями, що лишились; нульові ваги не випадають, якщо є ненульові.</summary>
        private byte PickColor(IRandomSource random)
        {
            var total = 0L;
            for (var i = 0; i < _colors.Count; i++)
                total += Math.Max(0, _colorWeights[i]);

            if (total <= 0)
                return _colors[random.Next(_colors.Count)];

            var roll = (long)(random.Next(1 << 20) / (double)(1 << 20) * total);
            for (var i = 0; i < _colors.Count; i++)
            {
                roll -= Math.Max(0, _colorWeights[i]);
                if (roll < 0 && _colorWeights[i] > 0)
                    return _colors[i];
            }

            for (var i = _colors.Count - 1; i >= 0; i--)
                if (_colorWeights[i] > 0)
                    return _colors[i];
            return _colors[0];
        }
    }
}
