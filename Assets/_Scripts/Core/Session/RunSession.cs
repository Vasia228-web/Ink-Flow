using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Забіг «Нескінченного» (документ §1–5, §10): порожнє поле 8×8, три одноколірні
    /// фігури в кольорах поточної картинки, зрив рядків і стовпців — і кожна клітинка
    /// зірваної лінії заповнює один піксель картинки свого кольору.
    ///
    /// Core не анімує: <see cref="TryPlace"/> мутує модель і повертає стрічку подій.
    /// Порядок кроків ходу — це порядок подій, і в'ю відтворює його один до одного:
    ///  1. валідація → 2. клітинки фігури → 3. фігура зникає з лотка → 4. кожна повна лінія
    ///  (LineCleared) і одразу за нею її пікселі (PixelFilled, у порядку клітинок) → 5. очищення
    ///  ліній → 6. ланцюг і очки → 7. картинку закінчено: наступна й перефарбування поля й лотка
    ///  АБО колір вичерпано: перефарбування лише його → 8. поповнення лотка → 9. живість.
    /// </summary>
    public sealed class RunSession
    {
        private readonly MoveResult _result = new MoveResult();
        private readonly List<Line> _lines = new List<Line>(16);
        private readonly List<GridPos> _cellBuffer = new List<GridPos>(64);
        private readonly LineYield[] _yields = new LineYield[32];
        private readonly TrayGenerator _trays;
        private readonly PictureDeck _deck;
        private readonly Func<string, bool>? _isCollected;
        private readonly List<int> _collected = new List<int>(4);
        private readonly List<byte> _colors = new List<byte>(8);
        private readonly List<int> _colorWeights = new List<int>(8);
        private readonly List<int> _pixelBuffer = new List<int>(64);
        private readonly int[] _boardCounts = new int[MasterPalette.Count];
        private readonly byte[] _colorMap = new byte[MasterPalette.Count];

        public RunSession(BalanceData balance, PieceCatalogData catalog, IRandomSource random,
            PictureLibrary? library = null, PictureStart? start = null, Func<string, bool>? isCollected = null)
        {
            Balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Library = library ?? PictureLibrary.Fallback;
            _isCollected = isCollected;
            _deck = new PictureDeck(Library, balance);

            Board = new Board(balance.GridWidth, balance.GridHeight);
            TrayPieces = new PieceDef[balance.TraySize];
            _trays = new TrayGenerator(catalog, balance);

            Picture = start.HasValue ? Carry(start.Value) : DrawPicture(exclude: -1);
            RefillTray(_result, silent: true);
        }

        public BalanceData Balance { get; }
        public PieceCatalogData Catalog { get; }
        public IRandomSource Random { get; }
        public Board Board { get; }

        /// <summary>Бібліотека картинок (§7).</summary>
        public PictureLibrary Library { get; }

        /// <summary>Картинка, яка зараз малюється (§4). Після завершення — одразу наступна.</summary>
        public PictureProgress Picture { get; private set; }

        /// <summary>Лоток. Порожня комірка означає «фігуру вже поставили» (§2).</summary>
        public PieceDef[] TrayPieces { get; }

        public IReadOnlyList<PieceDef> Tray => TrayPieces;

        public GameState State { get; private set; } = GameState.Playing;
        public bool IsOver => State != GameState.Playing;

        public int Score { get; private set; }
        public int BestChain { get; private set; }
        public int PlacementCount { get; private set; }
        public int Round => _trays.TraysIssued;
        public int LinesCleared { get; private set; }
        public int PureLinesCleared { get; private set; }

        /// <summary>Пікселів заповнено за партію.</summary>
        public int PixelsFilled { get; private set; }

        /// <summary>Пікселів згоріло за партію — колір уже не був потрібен.</summary>
        public int PixelsWasted { get; private set; }

        public int PicturesCompleted { get; private set; }

        /// <summary>Індекси закінчених картинок у бібліотеці, у порядку завершення.</summary>
        public IReadOnlyList<int> PicturesCollected => _collected;

        public int TrayRescues => _trays.RescuesUsed;

        // ── Незавершена (§9) ──

        public bool StartedWithCarried { get; private set; }
        public int CarriedIndex { get; private set; } = -1;
        public int AttemptsLeft { get; private set; }

        private PictureProgress Carry(PictureStart start)
        {
            if (start.LibraryIndex < 0 || start.LibraryIndex >= Library.Count)
                throw new ArgumentOutOfRangeException(nameof(start), "Незавершеної картинки немає в бібліотеці.");
            var progress = new PictureProgress(Library[start.LibraryIndex], start.LibraryIndex);
            progress.Restore(start.Filled);
            StartedWithCarried = true;
            CarriedIndex = start.LibraryIndex;
            AttemptsLeft = start.AttemptsLeft;
            return progress;
        }

        // ── Продовження (§10) ──

        public int ContinuesUsed { get; private set; }
        public bool CanContinue => IsOver && ContinuesUsed < Balance.ContinuesPerRun;

        /// <summary>Чи є в руці фігура, якій уже немає місця на полі — «тиск» для прогонів.</summary>
        public bool AnyPieceStuck()
        {
            for (var i = 0; i < TrayPieces.Length; i++)
            {
                var piece = TrayPieces[i];
                if (!piece.IsEmpty && !PlacementRules.AnyFit(Board, piece.Shape!))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Хід. Відхилене розміщення нічого не змінює і нічого не витрачає.
        /// Повертає той самий екземпляр MoveResult щоразу — копіюй, якщо треба зберегти.
        /// </summary>
        public MoveResult TryPlace(int trayIndex, GridPos anchor)
        {
            _result.Reset();

            if (IsOver || trayIndex < 0 || trayIndex >= TrayPieces.Length)
                return _result;

            var piece = TrayPieces[trayIndex];
            if (piece.IsEmpty)
                return _result;

            var shape = piece.Shape!;
            if (!PlacementRules.CanPlace(Board, shape, anchor))
                return _result;

            _result.MarkAccepted();
            PlacementCount++;

            _cellBuffer.Clear();
            for (var i = 0; i < shape.Cells.Length; i++)
            {
                var p = new GridPos(anchor.X + shape.Cells[i].X, anchor.Y + shape.Cells[i].Y);
                Board[p] = piece.Color;
                _cellBuffer.Add(p);
            }

            TrayPieces[trayIndex] = PieceDef.None;
            _result.AddPiecePlaced(trayIndex, _result.PushCells(_cellBuffer), _cellBuffer.Count, piece.Color);

            var gained = shape.Size * Balance.ScorePerPlacedCell;
            gained += ResolveLines();
            AddScore(gained);

            if (Picture.IsComplete)
                CompletePicture();
            else
                RecolorExhausted();

            RefillTrayIfEmpty();
            EvaluateState();

            if (State == GameState.Lost)
                _result.AddGameLost(PlacementCount, Score);

            return _result;
        }

        /// <summary>Миттєвий рестарт без перезавантаження сцени.</summary>
        public void Restart()
        {
            Board.Clear();
            _trays.Reset();
            PicturesCompleted = 0;
            PixelsFilled = 0;
            PixelsWasted = 0;
            _collected.Clear();
            StartedWithCarried = false;
            CarriedIndex = -1;
            AttemptsLeft = 0;
            ContinuesUsed = 0;
            Picture = DrawPicture(exclude: Picture.LibraryIndex);
            Score = 0;
            BestChain = 0;
            PlacementCount = 0;
            LinesCleared = 0;
            PureLinesCleared = 0;
            State = GameState.Playing;
            for (var i = 0; i < TrayPieces.Length; i++)
                TrayPieces[i] = PieceDef.None;
            _result.Reset();
            RefillTray(_result, silent: true);
        }

        /// <summary>Продовження (§10): поле очищується, рахунок і картинка лишаються, лоток — новий.</summary>
        public MoveResult ContinueAfterLoss()
        {
            _result.Reset();
            if (!CanContinue)
                return _result;

            Board.Clear();
            ContinuesUsed++;
            State = GameState.Playing;
            for (var i = 0; i < TrayPieces.Length; i++)
                TrayPieces[i] = PieceDef.None;
            _result.MarkAccepted();
            _result.AddRunContinued(ContinuesUsed);
            RefillTray(_result, silent: false);
            return _result;
        }

        /// <summary>
        /// «Домалювати одразу» (§13): усі пікселі, картинка зарахована, наступна, поле
        /// перефарбовано. На лоток-форми, поле-форми й рахунок не впливає.
        /// </summary>
        public MoveResult CompletePictureNow()
        {
            _result.Reset();
            if (Picture.IsComplete)
                return _result;

            _result.MarkAccepted();
            _pixelBuffer.Clear();
            Picture.FillAll(_pixelBuffer);
            for (var i = 0; i < _pixelBuffer.Count; i++)
            {
                var index = _pixelBuffer[i];
                _result.AddPixelFilled(index, Picture.Picture.Pixels[index], false, -1);
                PixelsFilled++;
            }
            CompletePicture();
            return _result;
        }

        public bool TryFindHint(out int trayIndex, out GridPos anchor) =>
            PlacementRules.TryFindHint(Board, TrayPieces, out trayIndex, out anchor);

        /// <summary>Жодна фігура лотка не влазить — єдина перевірка живості (§10).</summary>
        public bool NoPieceFits() => !PlacementRules.AnyPieceFits(Board, TrayPieces);

        private void EvaluateState()
        {
            if (NoPieceFits())
                State = GameState.Lost;
        }

        // ── Кроки 4–7 ──

        private int ResolveLines()
        {
            _lines.Clear();
            PlacementRules.CollectFullLines(Board, _lines);
            if (_lines.Count == 0)
                return 0;

            // 5. Вихід кожної лінії — за станом ДО очищення; клітинка на перетині рахується в обидві.
            var count = _lines.Count < _yields.Length ? _lines.Count : _yields.Length;
            for (var i = 0; i < count; i++)
                _yields[i] = LineResolver.Resolve(Board, _lines[i], Balance);

            var multiplier = Balance.ComboFor(count);
            var score = 0;

            for (var i = 0; i < count; i++)
            {
                var line = _lines[i];
                var length = LineResolver.LengthOf(Board, line.Kind);
                _cellBuffer.Clear();
                for (var c = 0; c < length; c++)
                    _cellBuffer.Add(LineResolver.CellAt(line, c));
                var cellStart = _result.PushCells(_cellBuffer);
                var lineEvent = _result.AddLineCleared(line.Kind, line.Index, cellStart, length, _yields[i].Dominant, _yields[i].IsPure);

                // Пікселі — з кожної клітинки, у порядку клітинок: крапля летить із клітинки в піксель.
                var pixels = 0;
                for (var c = 0; c < length; c++)
                {
                    var color = Board[_cellBuffer[c]];
                    if (color == Board.Empty)
                        continue;
                    for (var k = 0; k < _yields[i].PixelsPerCell; k++)
                    {
                        var index = Picture.FillOne(color);
                        if (index < 0)
                        {
                            _result.AddPixelsWasted(1);
                            PixelsWasted++;
                            continue;
                        }
                        _result.AddPixelFilled(index, color, _yields[i].IsPure, cellStart + c);
                        PixelsFilled++;
                        pixels++;
                    }
                }

                _result.SetLinePixels(lineEvent, pixels);

                var lineScore = Balance.ScorePerLine * (_yields[i].IsPure ? Balance.PureLineScoreBonus : 1);
                score += LineResolver.ApplyCombo(lineScore, multiplier);
            }

            // 6. Очистити всі лінії ОДНОЧАСНО: перетин очищується один раз.
            for (var i = 0; i < count; i++)
            {
                var line = _lines[i];
                var length = LineResolver.LengthOf(Board, line.Kind);
                for (var c = 0; c < length; c++)
                    Board[LineResolver.CellAt(line, c)] = Board.Empty;
            }

            // 7. Ланцюг.
            if (count >= 2)
                _result.AddCombo(count, multiplier);
            if (count > BestChain)
                BestChain = count;

            LinesCleared += count;
            PureLinesCleared += _result.PureLinesCleared;
            return score;
        }

        // ── Крок 8: картинка закінчена / колір вичерпано ──

        private void CompletePicture()
        {
            _result.AddPictureCompleted(Picture.LibraryIndex);
            PicturesCompleted++;
            _collected.Add(Picture.LibraryIndex);
            Picture = DrawPicture(exclude: Picture.LibraryIndex);
            _result.AddPictureStarted(Picture.LibraryIndex);
            RecolorToPicture();
        }

        /// <summary>
        /// Нова картинка (§5): усе на полі й у лотку перефарбовується в її кольори. Старі
        /// кольори за рангом кількості на полі й у лотку зіставляються з новими за рангом
        /// залишку пікселів; колір, який є в обох, лишається собою. Старих кольорів може
        /// бути більше за нові — тоді зайві йдуть по колу.
        /// </summary>
        private void RecolorToPicture()
        {
            Picture.RemainingColors(_colors, _colorWeights);
            if (_colors.Count == 0)
                return;

            Board.CountColors(_boardCounts);
            for (var i = 0; i < TrayPieces.Length; i++)
                if (!TrayPieces[i].IsEmpty)
                    _boardCounts[TrayPieces[i].Color] += TrayPieces[i].Size;

            // Старі кольори — за спаданням кількості (нічия — менший індекс).
            var old = new List<byte>();
            for (var c = 1; c < _boardCounts.Length; c++)
                if (_boardCounts[c] > 0)
                    old.Add((byte)c);
            old.Sort((a, b) => _boardCounts[b] != _boardCounts[a] ? _boardCounts[b].CompareTo(_boardCounts[a]) : a.CompareTo(b));

            Array.Clear(_colorMap, 0, _colorMap.Length);
            var nextNew = 0;
            for (var i = 0; i < old.Count; i++)
            {
                var from = old[i];
                if (_colors.Contains(from))
                {
                    _colorMap[from] = from;
                    continue;
                }
                // Перший ще не зайнятий новий колір за рангом; коли всі зайняті — по колу.
                var to = _colors[nextNew % _colors.Count];
                nextNew++;
                _colorMap[from] = to;
            }

            ApplyColorMap(old);
        }

        /// <summary>Колір закінчився (§5): його клітинки й фігури — у колір, якого лишилось найбільше.</summary>
        private void RecolorExhausted()
        {
            var target = Picture.MostNeededColor();
            if (target == Board.Empty)
                return;

            Board.CountColors(_boardCounts);
            for (var i = 0; i < TrayPieces.Length; i++)
                if (!TrayPieces[i].IsEmpty)
                    _boardCounts[TrayPieces[i].Color]++;

            var old = new List<byte>();
            Array.Clear(_colorMap, 0, _colorMap.Length);
            for (var c = 1; c < _boardCounts.Length; c++)
            {
                if (_boardCounts[c] == 0 || Picture.Remaining((byte)c) > 0)
                    continue;
                old.Add((byte)c);
                _colorMap[c] = target;
            }

            if (old.Count > 0)
                ApplyColorMap(old);
        }

        private void ApplyColorMap(List<byte> from)
        {
            for (var i = 0; i < from.Count; i++)
            {
                var source = from[i];
                var to = _colorMap[source];
                if (to == Board.Empty || to == source)
                    continue;

                _cellBuffer.Clear();
                Board.Recolor(source, to, _cellBuffer);
                if (_cellBuffer.Count > 0)
                    _result.AddBoardRecolored(source, to, _result.PushCells(_cellBuffer), _cellBuffer.Count);

                for (var k = 0; k < TrayPieces.Length; k++)
                {
                    if (TrayPieces[k].IsEmpty || TrayPieces[k].Color != source)
                        continue;
                    TrayPieces[k] = TrayPieces[k].WithColor(to);
                    _result.AddTrayRecolored(k, to);
                }
            }
        }

        private PictureProgress DrawPicture(int exclude)
        {
            var index = _deck.Draw(Random, exclude, _isCollected);
            return new PictureProgress(Library[index], index);
        }

        private void AddScore(int gained)
        {
            if (gained <= 0)
                return;
            Score += gained;
            _result.AddScore(gained, Score);
        }

        private void RefillTrayIfEmpty()
        {
            for (var i = 0; i < TrayPieces.Length; i++)
                if (!TrayPieces[i].IsEmpty)
                    return;
            RefillTray(_result, silent: false);
        }

        private void RefillTray(MoveResult result, bool silent)
        {
            Picture.RemainingColors(_colors, _colorWeights);
            if (_colors.Count == 0)
            {
                // Картинка повна лише між ходами на мить; про всяк випадок — її кольори, порівну.
                for (var i = 0; i < Picture.Picture.FillColors.Count; i++)
                {
                    _colors.Add(Picture.Picture.FillColors[i]);
                    _colorWeights.Add(1);
                }
            }

            var rescued = _trays.Fill(TrayPieces, Board, Random, _colors, _colorWeights);
            if (silent)
                return;
            if (rescued)
                result.AddTrayRescued();
            result.AddTrayRefilled(Round);
        }
    }
}
