using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Забіг «Нескінченного» (документ §2, §8): порожнє поле 8×8, три одноколірні фігури
    /// в руці, зрив рядків і стовпців, очки, програш — коли жодна фігура не влазить.
    ///
    /// Core не анімує: <see cref="TryPlace"/> мутує модель і повертає стрічку подій.
    /// Порядок кроків ходу фіксований — він же порядок подій, і в'ю відтворює його один
    /// до одного:
    ///  1. валідація → 2. клітинки фігури → 3. фігура зникає з лотка → 4. усі повні лінії
    ///  за станом ПІСЛЯ розміщення → 5. вихід кожної лінії за станом ДО очищення (клітинка
    ///  на перетині рахується в обидві) → 6. очищення всіх ліній одночасно → 7. ланцюг
    ///  і очки → 8. поповнення лотка, якщо порожній → 9. перевірка живості.
    /// </summary>
    public sealed class RunSession
    {
        private readonly MoveResult _result = new MoveResult();
        private readonly List<Line> _lines = new List<Line>(16);
        private readonly List<GridPos> _cellBuffer = new List<GridPos>(8);
        private readonly PaintYield[] _yields = new PaintYield[32];
        private readonly TrayGenerator _trays;

        public RunSession(BalanceData balance, PieceCatalogData catalog, IRandomSource random)
        {
            Balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Random = random ?? throw new ArgumentNullException(nameof(random));

            Board = new Board(balance.GridWidth, balance.GridHeight);
            TrayPieces = new PieceDef[balance.TraySize];
            _trays = new TrayGenerator(catalog, balance);

            RefillTray(_result, silent: true);
        }

        public BalanceData Balance { get; }
        public PieceCatalogData Catalog { get; }
        public IRandomSource Random { get; }
        public Board Board { get; }

        /// <summary>Лоток. Порожня комірка означає «фігуру вже поставили» (§2).</summary>
        public PieceDef[] TrayPieces { get; }

        public IReadOnlyList<PieceDef> Tray => TrayPieces;

        public GameState State { get; private set; } = GameState.Playing;
        public bool IsOver => State != GameState.Playing;

        public int Score { get; private set; }

        /// <summary>Найдовший ланцюг за партію — другий рекорд документа (§8).</summary>
        public int BestChain { get; private set; }

        /// <summary>Скільки фігур поставлено за партію.</summary>
        public int PlacementCount { get; private set; }

        /// <summary>Скільки лотків видано — раунд для прогресії складності (§8).</summary>
        public int Round => _trays.TraysIssued;

        public int LinesCleared { get; private set; }
        public int PureLinesCleared { get; private set; }
        public int PaintYielded { get; private set; }

        /// <summary>Скільки разів мішок мусив зменшувати фігури — метрика якості мішка.</summary>
        public int TrayRescues => _trays.RescuesUsed;

        /// <summary>
        /// Тиск: поле майже забите АБО хоч одна фігура з руки вже нікуди не влазить —
        /// в'ю світить попереджувальне гало. Друга умова головна: прогони показали, що
        /// партія гине з ~25 вільними клітинками, «дірявою», а не повною, і сам лише
        /// поріг вільних клітинок не спрацьовував жодного разу.
        /// </summary>
        public bool HaloWarning => Board.CountEmpty() < Balance.HaloWarningFreeCells || AnyPieceStuck();

        /// <summary>Чи є в руці фігура, якій уже немає місця на полі.</summary>
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
        /// Який пігмент бажаний для наступного лотка (крок 4: колір активної зони
        /// картинки). None — мішок вирішує сам.
        /// </summary>
        public Pigment WantedPigment { get; set; } = Pigment.None;

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

            // 1. Валідація.
            if (!PlacementRules.CanPlace(Board, shape, anchor))
                return _result;

            _result.MarkAccepted();
            PlacementCount++;

            // 2. Заповнити клітинки. 3. Прибрати фігуру з лотка.
            _cellBuffer.Clear();
            for (var i = 0; i < shape.Cells.Length; i++)
            {
                var p = new GridPos(anchor.X + shape.Cells[i].X, anchor.Y + shape.Cells[i].Y);
                Board[p] = piece.Pigment;
                _cellBuffer.Add(p);
            }

            TrayPieces[trayIndex] = PieceDef.None;
            _result.AddPiecePlaced(trayIndex, _result.PushCells(_cellBuffer), _cellBuffer.Count, piece.Pigment);

            var gained = shape.Size * Balance.ScorePerPlacedCell;
            gained += ResolveLines();
            AddScore(gained);

            RefillTrayIfEmpty();
            EvaluateState();

            if (State == GameState.Lost)
                _result.AddGameLost(PlacementCount, Score);

            return _result;
        }

        /// <summary>Миттєвий рестарт без перезавантаження сцени (&lt; 300 мс).</summary>
        public void Restart()
        {
            Board.Clear();
            _trays.Reset();
            Score = 0;
            BestChain = 0;
            PlacementCount = 0;
            LinesCleared = 0;
            PureLinesCleared = 0;
            PaintYielded = 0;
            State = GameState.Playing;
            for (var i = 0; i < TrayPieces.Length; i++)
                TrayPieces[i] = PieceDef.None;
            _result.Reset();
            RefillTray(_result, silent: true);
        }

        /// <summary>Підказка від застою: перша фігура й перше місце, куди вона влазить.</summary>
        public bool TryFindHint(out int trayIndex, out GridPos anchor) =>
            PlacementRules.TryFindHint(Board, TrayPieces, out trayIndex, out anchor);

        /// <summary>Жодна фігура лотка не влазить — єдина перевірка живості (§8).</summary>
        public bool NoPieceFits() => !PlacementRules.AnyPieceFits(Board, TrayPieces);

        private void EvaluateState()
        {
            if (NoPieceFits())
                State = GameState.Lost;
        }

        /// <summary>Кроки 4–7 ходу. Повертає очки за лінії.</summary>
        private int ResolveLines()
        {
            // 4. Зібрати ВСІ повні лінії за станом ПІСЛЯ розміщення.
            _lines.Clear();
            PlacementRules.CollectFullLines(Board, _lines);
            if (_lines.Count == 0)
                return 0;

            // 5. Порахувати вихід кожної лінії за станом ДО очищення. Клітинка на перетині
            //    зараховується в обидві лінії — саме тому рахуємо все до єдиного очищення.
            var count = _lines.Count < _yields.Length ? _lines.Count : _yields.Length;
            for (var i = 0; i < count; i++)
                _yields[i] = LineResolver.Resolve(Board, _lines[i], Balance, TankLevels());

            var multiplier = Balance.ComboFor(count);
            var score = 0;

            for (var i = 0; i < count; i++)
            {
                var line = _lines[i];
                var length = LineResolver.LengthOf(Board, line.Kind);
                _cellBuffer.Clear();
                for (var c = 0; c < length; c++)
                    _cellBuffer.Add(LineResolver.CellAt(line, c));

                var amount = LineResolver.ApplyCombo(_yields[i].Amount, multiplier);
                _result.AddLineCleared(line.Kind, line.Index,
                    _result.PushCells(_cellBuffer), _cellBuffer.Count,
                    _yields[i].Pigment, _yields[i].IsPure, amount);

                var lineScore = Balance.ScorePerLine * (_yields[i].IsPure ? Balance.PureLineScoreBonus : 1);
                score += LineResolver.ApplyCombo(lineScore, multiplier);
            }

            // 6. Очистити всі знайдені лінії ОДНОЧАСНО: перетин очищується один раз.
            for (var i = 0; i < count; i++)
            {
                var line = _lines[i];
                var length = LineResolver.LengthOf(Board, line.Kind);
                for (var c = 0; c < length; c++)
                    Board[LineResolver.CellAt(line, c)] = Pigment.None;
            }

            // 7. Ланцюг.
            if (count >= 2)
                _result.AddCombo(count, multiplier);
            if (count > BestChain)
                BestChain = count;

            LinesCleared += count;
            PureLinesCleared += _result.PureLinesCleared;
            PaintYielded += _result.PaintYielded;
            return score;
        }

        /// <summary>Рівні баків для тайбрейка мішаної лінії. Баки з'являються на кроці 2.</summary>
        private IReadOnlyList<int>? TankLevels() => null;

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
            var rescued = _trays.Fill(TrayPieces, Board, Random, WantedPigment);
            if (silent)
                return;
            if (rescued)
                result.AddTrayRescued();
            result.AddTrayRefilled(Round);
        }
    }
}
