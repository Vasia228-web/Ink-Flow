using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Повний опис ходу у вигляді впорядкованої стрічки подій.
    ///
    /// Головне архітектурне рішення проєкту, яке нове ядро зберігає без змін: Core не
    /// анімує — він повертає події, а в'ю програє їх із таймінгами. Завдяки цьому правила
    /// тестуються без жодного кадру рендеру, анімації прискорюються, не чіпаючи логіку,
    /// а бот ганяє тисячі партій за секунди.
    ///
    /// Екземпляр ПЕРЕВИКОРИСТОВУЄТЬСЯ сесією між ходами (нуль алокацій): хто хоче
    /// зберегти події довше — копіює їх собі.
    /// </summary>
    public sealed class MoveResult
    {
        private readonly List<GameEvent> _events = new List<GameEvent>(64);
        private readonly List<GridPos> _cells = new List<GridPos>(256);

        /// <summary>false — розміщення відхилено: стан НЕ змінився, фігура лишилась у лотку.</summary>
        public bool Accepted { get; private set; }

        public IReadOnlyList<GameEvent> Events => _events;

        /// <summary>Скільки ліній зірвано за цей хід — від цього залежить ланцюг і feel.</summary>
        public int LinesCleared { get; private set; }

        /// <summary>Скільки з них були чистими. Прямий вимір головної ідеї гри.</summary>
        public int PureLinesCleared { get; private set; }

        /// <summary>Скільки одиниць фарби видали лінії цього ходу (вже з ланцюгом).</summary>
        public int PaintYielded { get; private set; }

        /// <summary>Скільки разів спрацював змішувач цього ходу.</summary>
        public int Splashes { get; private set; }

        /// <summary>Відтінок останнього виплеску ходу; None, якщо змішувач мовчав.</summary>
        public Hue LastSplashHue { get; private set; }

        public int ScoreGained { get; private set; }

        /// <summary>Клітинка зрізу події — списки клітинок лежать спільним буфером.</summary>
        public GridPos Cell(in GameEvent e, int index) => _cells[e.CellStart + index];

        internal void Reset()
        {
            _events.Clear();
            _cells.Clear();
            Accepted = false;
            LinesCleared = 0;
            PureLinesCleared = 0;
            PaintYielded = 0;
            Splashes = 0;
            LastSplashHue = Hue.None;
            ScoreGained = 0;
        }

        internal void MarkAccepted() => Accepted = true;

        /// <summary>Кладе клітинки в спільний буфер і повертає початок зрізу.</summary>
        internal int PushCells(IReadOnlyList<GridPos> cells)
        {
            var start = _cells.Count;
            for (var i = 0; i < cells.Count; i++)
                _cells.Add(cells[i]);
            return start;
        }

        internal void AddPiecePlaced(int trayIndex, int cellStart, int cellCount, Pigment pigment) =>
            _events.Add(new GameEvent(GameEventType.PiecePlaced, LineKind.Row, pigment,
                0, trayIndex, 0f, false, cellStart, cellCount));

        internal void AddLineCleared(LineKind kind, int index, int cellStart, int cellCount,
            Pigment dominant, bool isPure, int amount)
        {
            _events.Add(new GameEvent(GameEventType.LineCleared, kind, dominant,
                amount, index, 0f, isPure, cellStart, cellCount));
            LinesCleared++;
            if (isPure)
                PureLinesCleared++;
            PaintYielded += amount;
        }

        internal void AddCombo(int lineCount, float multiplier) =>
            _events.Add(new GameEvent(GameEventType.ComboApplied, LineKind.Row, Pigment.None,
                0, lineCount, multiplier, false, 0, 0));

        internal void AddScore(int gained, int total)
        {
            ScoreGained += gained;
            _events.Add(new GameEvent(GameEventType.ScoreGained, LineKind.Row, Pigment.None,
                gained, total, 0f, false, 0, 0));
        }

        internal void AddPaintPoured(Pigment pigment, int amount, int levelAfter) =>
            _events.Add(new GameEvent(GameEventType.PaintPoured, LineKind.Row, pigment,
                amount, levelAfter, 0f, false, 0, 0));

        internal void AddTankDrained(Pigment pigment, int amount, int levelAfter) =>
            _events.Add(new GameEvent(GameEventType.TankDrained, LineKind.Row, pigment,
                amount, levelAfter, 0f, false, 0, 0));

        internal void AddMixerFired(Hue hue, int amount)
        {
            _events.Add(new GameEvent(GameEventType.MixerFired, LineKind.Row, Pigment.None,
                amount, (int)hue, 0f, false, 0, 0));
            Splashes++;
            LastSplashHue = hue;
        }

        internal void AddTrayRefilled(int round) =>
            _events.Add(new GameEvent(GameEventType.TrayRefilled, LineKind.Row, Pigment.None,
                round, 0, 0f, false, 0, 0));

        internal void AddTrayRescued() =>
            _events.Add(new GameEvent(GameEventType.TrayRescued, LineKind.Row, Pigment.None,
                0, 0, 0f, false, 0, 0));

        internal void AddGameLost(int placementCount, int score) =>
            _events.Add(new GameEvent(GameEventType.GameLost, LineKind.Row, Pigment.None,
                score, placementCount, 0f, false, 0, 0));

        /// <summary>Знімок подій — для тестів і логів (алокує, у грі не використовується).</summary>
        public GameEvent[] SnapshotEvents() => _events.ToArray();

        public int CountEvents(GameEventType type)
        {
            var count = 0;
            for (var i = 0; i < _events.Count; i++)
                if (_events[i].Type == type)
                    count++;
            return count;
        }

        public bool Has(GameEventType type) => CountEvents(type) > 0;
    }
}
