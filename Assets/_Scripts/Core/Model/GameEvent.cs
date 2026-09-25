namespace InkFlow.Core
{
    /// <summary>
    /// Типи подій стрічки ходу. Значення не переставляти — на них можуть спиратись
    /// зовнішні інструменти. Порядок у стрічці — порядок кроків ходу в RunSession.
    /// </summary>
    public enum GameEventType : byte
    {
        /// <summary>Фігуру поставлено: Value = комірка лотка, клітинки — у буфері.</summary>
        PiecePlaced = 0,

        /// <summary>Лінію зірвано: Kind/Extra = індекс, Color = головний колір, IsPure, Value = пікселів дала.</summary>
        LineCleared = 1,

        /// <summary>Ланцюг: Value = ліній за хід, Multiplier.</summary>
        ComboApplied = 2,

        /// <summary>Лоток поповнено: Value = номер раунду.</summary>
        TrayRefilled = 3,

        /// <summary>Мішок мусив зменшити фігури, щоб хоч одна влізла.</summary>
        TrayRescued = 4,

        /// <summary>Програш: Value = розміщень, Extra = рахунок.</summary>
        GameLost = 5,

        /// <summary>Очки за хід: Value = скільки додано, Extra = разом.</summary>
        ScoreGained = 6,

        /// <summary>Піксель картинки заповнено: Value = індекс пікселя, Color, IsPure = з чистої лінії, клітинка-джерело — у буфері (одна).</summary>
        PixelFilled = 7,

        /// <summary>Клітинки поля перефарбовано: Value = старий колір, Extra = новий, клітинки — у буфері.</summary>
        BoardRecolored = 8,

        /// <summary>Фігуру в лотку перефарбовано: Value = комірка, Extra = новий колір.</summary>
        TrayRecolored = 9,

        /// <summary>Усі пікселі заповнено: Value = індекс картинки в бібліотеці.</summary>
        PictureCompleted = 10,

        /// <summary>Прийшла наступна картинка: Value = індекс картинки в бібліотеці.</summary>
        PictureStarted = 11,

        /// <summary>Забіг продовжено після програшу: поле очищено, Value = скільки разів продовжували.</summary>
        RunContinued = 12
    }

    /// <summary>
    /// Одна подія стрічки. Структура, щоб стрічка не алокувала; клітинки лежать у
    /// спільному буфері <see cref="MoveResult"/> (CellStart, CellCount).
    /// </summary>
    public readonly struct GameEvent
    {
        internal GameEvent(GameEventType type, LineKind kind, byte color, int value, int extra,
            float multiplier, bool isPure, int cellStart, int cellCount)
        {
            Type = type;
            Kind = kind;
            Color = color;
            Value = value;
            Extra = extra;
            Multiplier = multiplier;
            IsPure = isPure;
            CellStart = cellStart;
            CellCount = cellCount;
        }

        public GameEventType Type { get; }
        public LineKind Kind { get; }

        /// <summary>Індекс кольору майстер-палітри (0 — не стосується).</summary>
        public byte Color { get; }

        public int Value { get; }
        public int Extra { get; }
        public float Multiplier { get; }
        public bool IsPure { get; }
        public int CellStart { get; }
        public int CellCount { get; }

        public override string ToString() => $"{Type}(v={Value}, x={Extra}, c={Color})";
    }
}
