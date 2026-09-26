using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Повний опис того, що сталося за хід, у вигляді впорядкованого списку подій.
    /// Core не анімує — повертає стрічку; в'ю програє її з таймінгами.
    ///
    /// Буфери переиспользуються між ходами (нуль алокацій у сталому режимі): результат
    /// живе до наступного ходу, а хто хоче зберегти — копіює.
    /// </summary>
    public sealed class MoveResult
    {
        private readonly List<GameEvent> _events = new List<GameEvent>(64);
        private readonly List<GridPos> _cells = new List<GridPos>(256);

        public bool Accepted { get; private set; }
        public IReadOnlyList<GameEvent> Events => _events;

        public int LinesCleared { get; private set; }
        public int PureLinesCleared { get; private set; }

        /// <summary>Кроків картинки заповнено цього ходу.</summary>
        public int PixelsFilled { get; private set; }

        /// <summary>Кроків згоріло: родина вже не потрібна картинці (§5).</summary>
        public int PixelsWasted { get; private set; }

        public int ScoreGained { get; private set; }

        /// <summary>Картинок закінчено цього ходу.</summary>
        public int PicturesCompleted { get; private set; }

        /// <summary>Клітинка події за номером у її діапазоні.</summary>
        public GridPos Cell(in GameEvent e, int i) => _cells[e.CellStart + i];

        public int CountEvents(GameEventType type)
        {
            var n = 0;
            for (var i = 0; i < _events.Count; i++)
                if (_events[i].Type == type)
                    n++;
            return n;
        }

        public bool Has(GameEventType type) => CountEvents(type) > 0;

        /// <summary>Знімок подій — для тестів і реплеїв; у грі стрічку читають на місці.</summary>
        public GameEvent[] SnapshotEvents() => _events.ToArray();

        internal void Reset()
        {
            _events.Clear();
            _cells.Clear();
            Accepted = false;
            LinesCleared = 0;
            PureLinesCleared = 0;
            PixelsFilled = 0;
            PixelsWasted = 0;
            ScoreGained = 0;
            PicturesCompleted = 0;
        }

        internal void MarkAccepted() => Accepted = true;

        internal int PushCells(List<GridPos> cells)
        {
            var start = _cells.Count;
            for (var i = 0; i < cells.Count; i++)
                _cells.Add(cells[i]);
            return start;
        }

        internal int PushCell(GridPos cell)
        {
            _cells.Add(cell);
            return _cells.Count - 1;
        }

        internal void AddPiecePlaced(int trayIndex, int cellStart, int cellCount, byte color) =>
            _events.Add(new GameEvent(GameEventType.PiecePlaced, LineKind.Row, color, trayIndex, 0, 0f, false, cellStart, cellCount));

        /// <summary>Лінію зірвано; пікселі йдуть ПІСЛЯ неї, тож їхню кількість дописує <see cref="SetLinePixels"/>. Повертає індекс події.</summary>
        internal int AddLineCleared(LineKind kind, int index, int cellStart, int cellCount, byte dominant, bool isPure)
        {
            _events.Add(new GameEvent(GameEventType.LineCleared, kind, dominant, 0, index, 0f, isPure, cellStart, cellCount));
            LinesCleared++;
            if (isPure)
                PureLinesCleared++;
            return _events.Count - 1;
        }

        internal void SetLinePixels(int eventIndex, int pixels)
        {
            var e = _events[eventIndex];
            _events[eventIndex] = new GameEvent(e.Type, e.Kind, e.Color, pixels, e.Extra, e.Multiplier, e.IsPure, e.CellStart, e.CellCount);
        }

        internal void AddCombo(int lines, float multiplier) =>
            _events.Add(new GameEvent(GameEventType.ComboApplied, LineKind.Row, 0, lines, lines, multiplier, false, 0, 0));

        internal void AddScore(int gained, int total)
        {
            _events.Add(new GameEvent(GameEventType.ScoreGained, LineKind.Row, 0, gained, total, 0f, false, 0, 0));
            ScoreGained += gained;
        }

        internal void AddPixelFilled(int pixelIndex, byte color, bool fromPureLine, int sourceCellStart)
        {
            _events.Add(new GameEvent(GameEventType.PixelFilled, LineKind.Row, color, pixelIndex, 0, 0f, fromPureLine, sourceCellStart, 1));
            PixelsFilled++;
        }

        internal void AddPixelsWasted(int amount) => PixelsWasted += amount;

        internal void AddBoardRecolored(byte from, byte to, int cellStart, int cellCount) =>
            _events.Add(new GameEvent(GameEventType.BoardRecolored, LineKind.Row, to, from, to, 0f, false, cellStart, cellCount));

        internal void AddTrayRecolored(int slot, byte to) =>
            _events.Add(new GameEvent(GameEventType.TrayRecolored, LineKind.Row, to, slot, to, 0f, false, 0, 0));

        internal void AddPictureCompleted(int libraryIndex)
        {
            _events.Add(new GameEvent(GameEventType.PictureCompleted, LineKind.Row, 0, libraryIndex, 0, 0f, false, 0, 0));
            PicturesCompleted++;
        }

        internal void AddPictureStarted(int libraryIndex) =>
            _events.Add(new GameEvent(GameEventType.PictureStarted, LineKind.Row, 0, libraryIndex, 0, 0f, false, 0, 0));

        internal void AddRunContinued(int continues) =>
            _events.Add(new GameEvent(GameEventType.RunContinued, LineKind.Row, 0, continues, 0, 0f, false, 0, 0));

        internal void AddTrayRefilled(int round) =>
            _events.Add(new GameEvent(GameEventType.TrayRefilled, LineKind.Row, 0, round, 0, 0f, false, 0, 0));

        internal void AddTrayRescued() =>
            _events.Add(new GameEvent(GameEventType.TrayRescued, LineKind.Row, 0, 0, 0, 0f, false, 0, 0));

        internal void AddGameLost(int placements, int score) =>
            _events.Add(new GameEvent(GameEventType.GameLost, LineKind.Row, 0, placements, score, 0f, false, 0, 0));
    }
}
