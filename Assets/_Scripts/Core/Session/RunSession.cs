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
    ///  і очки, фарба в баки, виплески змішувача → 8. поповнення лотка, якщо порожній →
    ///  9. перевірка живості.
    /// </summary>
    public sealed class RunSession
    {
        private readonly MoveResult _result = new MoveResult();
        private readonly List<Line> _lines = new List<Line>(16);
        private readonly List<GridPos> _cellBuffer = new List<GridPos>(8);
        private readonly PaintYield[] _yields = new PaintYield[32];
        private readonly TrayGenerator _trays;
        private readonly int[] _taken = new int[Pigments.Count];
        private readonly int[] _splashesByHue = new int[Hues.Count + 1];
        private readonly List<int> _collected = new List<int>(4);
        private readonly PictureDeck _deck;

        public RunSession(BalanceData balance, PieceCatalogData catalog, IRandomSource random,
            PictureCatalogData? pictures = null, PictureStart? start = null)
        {
            Balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Pictures = pictures ?? PictureCatalogData.Default;
            _deck = new PictureDeck(Pictures, balance);

            Board = new Board(balance.GridWidth, balance.GridHeight);
            Tanks = new TankSet();
            Mixer = new Mixer(balance);
            TrayPieces = new PieceDef[balance.TraySize];
            _trays = new TrayGenerator(catalog, balance);

            Picture = start.HasValue ? Carry(start.Value) : DrawPicture(exclude: -1);
            RefillTray(_result, silent: true);
        }

        /// <summary>§7: незавершена гарантовано перша, з тим самим прогресом.</summary>
        private PictureProgress Carry(PictureStart start)
        {
            if (start.CatalogIndex < 0 || start.CatalogIndex >= Pictures.Count)
                throw new ArgumentOutOfRangeException(nameof(start), "Незавершеної картинки немає в колоді.");
            var progress = new PictureProgress(Pictures[start.CatalogIndex], start.CatalogIndex, Balance);
            progress.Restore(start.Filled);
            StartedWithCarried = true;
            CarriedIndex = start.CatalogIndex;
            AttemptsLeft = start.AttemptsLeft;
            return progress;
        }

        /// <summary>Забіг почався з незавершеної картинки (§7).</summary>
        public bool StartedWithCarried { get; private set; }

        /// <summary>Індекс перенесеної картинки; −1, якщо забіг почався з нової.</summary>
        public int CarriedIndex { get; private set; } = -1;

        /// <summary>Скільки спроб лишилось на перенесену — для напису «Спроб лишилось: N».</summary>
        public int AttemptsLeft { get; private set; }

        public BalanceData Balance { get; }
        public PieceCatalogData Catalog { get; }
        public IRandomSource Random { get; }
        public Board Board { get; }

        /// <summary>Три баки фарби (§4). Зірвана лінія ллє сюди, змішувач забирає звідси.</summary>
        public TankSet Tanks { get; }

        /// <summary>Четвертий бак (§4): спрацьовує сам, щойно в трьох разом набралось на виплеск.</summary>
        public Mixer Mixer { get; }

        /// <summary>Скільки виплесків за партію.</summary>
        public int Splashes { get; private set; }

        /// <summary>Виплески за відтінками, індекс — (int)<see cref="Hue"/>.</summary>
        public IReadOnlyList<int> SplashesByHue => _splashesByHue;

        /// <summary>Відтінок останнього виплеску партії; None, якщо їх ще не було.</summary>
        public Hue LastSplashHue { get; private set; }

        /// <summary>Колода картинок (§5–6).</summary>
        public PictureCatalogData Pictures { get; }

        /// <summary>Картинка, яка зараз малюється (§5). Після завершення — одразу наступна.</summary>
        public PictureProgress Picture { get; private set; }

        /// <summary>Картинок закінчено за партію — головний рекорд документа (§8).</summary>
        public int PicturesCompleted { get; private set; }

        /// <summary>Індекси закінчених картинок у колоді, у порядку завершення — для галереї партії.</summary>
        public IReadOnlyList<int> PicturesCollected => _collected;

        /// <summary>Фарби з виплесків, якій не було куди лягти.</summary>
        public int PaintMissed { get; private set; }

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
        /// Який пігмент бажаний для наступного лотка (прототип v3: перша фігура лотка —
        /// кольору активної зони). Чистий відтінок — його пігмент; вторинний — той із двох,
        /// якого в баках менше, щоб пропорція йшла до потрібного; коричневий — найменший
        /// із трьох. None — картинку закінчено, мішок вирішує сам.
        /// </summary>
        public Pigment WantedPigment
        {
            get
            {
                WantedPigments(out var first, out _);
                return first;
            }
        }

        /// <summary>Обидва пігменти потрібного відтінку (для вторинного); second = None для чистого.</summary>
        public void WantedPigments(out Pigment first, out Pigment second)
        {
            first = Pigment.None;
            second = Pigment.None;
            switch (Picture.WantedHue)
            {
                case Hue.Blue: first = Pigment.Blue; return;
                case Hue.Red: first = Pigment.Red; return;
                case Hue.Yellow: first = Pigment.Yellow; return;
                case Hue.Green: Order(Pigment.Blue, Pigment.Yellow, out first, out second); return;
                case Hue.Orange: Order(Pigment.Red, Pigment.Yellow, out first, out second); return;
                case Hue.Purple: Order(Pigment.Blue, Pigment.Red, out first, out second); return;
                case Hue.Brown:
                    first = Pigment.Blue;
                    if (Tanks[Pigment.Red] < Tanks[first]) first = Pigment.Red;
                    if (Tanks[Pigment.Yellow] < Tanks[first]) first = Pigment.Yellow;
                    return;
            }
        }

        private void Order(Pigment a, Pigment b, out Pigment lower, out Pigment higher)
        {
            if (Tanks[b] < Tanks[a]) { lower = b; higher = a; }
            else { lower = a; higher = b; }
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
            Tanks.Reset();
            _trays.Reset();
            Array.Clear(_splashesByHue, 0, _splashesByHue.Length);
            Splashes = 0;
            LastSplashHue = Hue.None;
            PicturesCompleted = 0;
            PaintMissed = 0;
            _collected.Clear();
            StartedWithCarried = false;
            CarriedIndex = -1;
            AttemptsLeft = 0;
            ContinuesUsed = 0;
            Picture = DrawPicture(exclude: Picture.CatalogIndex);
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

        /// <summary>Скільки разів продовжували після програшу цього забігу.</summary>
        public int ContinuesUsed { get; private set; }

        /// <summary>§9: продовжити можна лише після програшу і не більше, ніж дозволяє баланс.</summary>
        public bool CanContinue => IsOver && ContinuesUsed < Balance.ContinuesPerRun;

        /// <summary>
        /// Продовження за ролик (§9): поле очищується, рахунок, баки й картинка лишаються,
        /// лоток — новий. Повертає стрічку з однією подією <see cref="GameEventType.RunContinued"/>
        /// і поповненням лотка; або порожню, якщо продовжувати не можна.
        /// </summary>
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
        /// Донат (§9): домалювати поточну картинку одразу. Заливає всі зони, зараховує
        /// картинку, витягує наступну. На поле, лоток і рахунок не впливає — «донат не
        /// впливає на проходження». Порожню стрічку — якщо картинка вже закінчена.
        /// </summary>
        public MoveResult CompletePictureNow()
        {
            _result.Reset();
            if (Picture.IsComplete)
                return _result;

            _result.MarkAccepted();
            for (var zone = 0; zone < Picture.ZoneCount; zone++)
            {
                var missing = Picture.Capacity(zone) - Picture.Filled[zone];
                if (missing > 0)
                    Picture.Apply(Picture.Def.Zones[zone].Hue, missing, _result);
            }

            _result.AddPictureCompleted(Picture.CatalogIndex);
            PicturesCompleted++;
            _collected.Add(Picture.CatalogIndex);
            Picture = DrawPicture(exclude: Picture.CatalogIndex);
            _result.AddPictureStarted(Picture.CatalogIndex);
            return _result;
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

            // 7б. Фарба — у бак свого кольору (§4).
            for (var i = 0; i < count; i++)
            {
                if (_yields[i].Pigment == Pigment.None)
                    continue;
                var amount = LineResolver.ApplyCombo(_yields[i].Amount, multiplier);
                if (amount <= 0)
                    continue;
                Tanks.Pour(_yields[i].Pigment, amount);
                _result.AddPaintPoured(_yields[i].Pigment, amount, Tanks[_yields[i].Pigment]);
            }

            // 7в. Змішувач (§4): набралось на виплеск — спрацьовує сам; стільки разів,
            //     скільки набралось. Три струмені (TankDrained) — потім виплеск (MixerFired).
            while (Mixer.CanFire(Tanks))
            {
                var splash = Mixer.Fire(Tanks, _taken);
                for (var i = 0; i < Pigments.Count; i++)
                    if (_taken[i] > 0)
                        _result.AddTankDrained(Pigments.Base[i], _taken[i], Tanks.Levels[i]);
                _result.AddMixerFired(splash.Hue, splash.Amount);
                Splashes++;
                _splashesByHue[(int)splash.Hue]++;
                LastSplashHue = splash.Hue;

                // 7г. Виплеск лягає на картинку (§5) — сам, у зону свого відтінку.
                var missed = Picture.Apply(splash.Hue, splash.Amount, _result);
                if (missed > 0)
                {
                    _result.AddSplashMissed(splash.Hue, missed);
                    PaintMissed += missed;
                }

                if (Picture.IsComplete)
                {
                    _result.AddPictureCompleted(Picture.CatalogIndex);
                    PicturesCompleted++;
                    _collected.Add(Picture.CatalogIndex);
                    Picture = DrawPicture(exclude: Picture.CatalogIndex);
                    _result.AddPictureStarted(Picture.CatalogIndex);
                }
            }

            LinesCleared += count;
            PureLinesCleared += _result.PureLinesCleared;
            PaintYielded += _result.PaintYielded;
            return score;
        }

        /// <summary>Наступна картинка з колоди за рідкістю (§6), крім щойно закінченої.</summary>
        private PictureProgress DrawPicture(int exclude)
        {
            var index = _deck.Draw(Random, exclude);
            return new PictureProgress(Pictures[index], index, Balance);
        }

        /// <summary>Рівні баків для тайбрейка мішаної лінії: нічия віддає колір, якого менше.</summary>
        private IReadOnlyList<int> TankLevels() => Tanks.Levels;

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
