namespace InkFlow.Core
{
    /// <summary>Тип події ходу. Дописувати тільки в кінець: значення потрапляють у логи.</summary>
    public enum GameEventType : byte
    {
        /// <summary>Фігуру поставлено: Extra = індекс у лотку, Pigment = колір, клітинки — у зрізі.</summary>
        PiecePlaced = 0,

        /// <summary>Лінію зірвано: Extra = індекс лінії, Kind, Pigment = домінантний, Value = вихід фарби (з ланцюгом), IsPure.</summary>
        LineCleared = 1,

        /// <summary>Ланцюг за хід: Extra = скільки ліній, Multiplier = множник.</summary>
        ComboApplied = 2,

        /// <summary>Лоток поповнено (самі фігури — у Tray сесії). Value = раунд.</summary>
        TrayRefilled = 3,

        /// <summary>Мішок мусив зменшити фігури, щоб хоч одна влізла. Діагностика.</summary>
        TrayRescued = 4,

        /// <summary>Партію програно: Extra = розміщень зроблено, Value = очки.</summary>
        GameLost = 5,

        /// <summary>Очки за хід: Value = скільки додано, Extra = разом.</summary>
        ScoreGained = 6,

        /// <summary>Фарба влилась у бак: Pigment, Value = скільки, Extra = рівень бака після.</summary>
        PaintPoured = 7,

        /// <summary>Змішувач забрав із бака: Pigment, Value = скільки, Extra = рівень бака після.</summary>
        TankDrained = 8,

        /// <summary>Виплеск змішувача: Value = скільки фарби, Extra = (int)<see cref="Hue"/>.</summary>
        MixerFired = 9,

        /// <summary>Фарба лягла в зону картинки: Value = зона, Extra = рівень зони після, CellStart = стеля зони, CellCount = скільки лягло, IsPure = зону закінчено.</summary>
        ZoneFilled = 10,

        /// <summary>Усі зони залиті: Value = індекс картинки в колоді.</summary>
        PictureCompleted = 11,

        /// <summary>Виплеску нікуди лягти — фарба пропала: Value = скільки, Extra = (int)<see cref="Hue"/>.</summary>
        SplashMissed = 12,

        /// <summary>Прийшла наступна картинка: Value = індекс картинки в колоді.</summary>
        PictureStarted = 13,

        /// <summary>Забіг продовжено після програшу (§9, раз за забіг): поле очищено, Value = скільки разів продовжували.</summary>
        RunContinued = 14
    }

    /// <summary>
    /// Одна подія ходу. Struct-union замість ієрархії класів: за хід із ланцюгом це
    /// десятки подій, а бюджет — нуль GC-алокацій під час партії.
    ///
    /// Списки клітинок НЕ лежать усередині події: вони живуть спільним буфером у
    /// <see cref="MoveResult"/>, а подія несе лише зріз (CellStart, CellCount).
    /// </summary>
    public readonly struct GameEvent
    {
        public GameEventType Type { get; }
        public LineKind Kind { get; }
        public Pigment Pigment { get; }

        /// <summary>Числове значення: вихід фарби, очки, раунд.</summary>
        public int Value { get; }

        /// <summary>Другий цілий параметр: індекс лотка, індекс лінії, кількість ліній.</summary>
        public int Extra { get; }

        /// <summary>Дробовий параметр: множник ланцюга.</summary>
        public float Multiplier { get; }

        /// <summary>Лінія була одноколірною — головна відмінність, яку має бачити гравець (§3).</summary>
        public bool IsPure { get; }

        /// <summary>Початок зрізу клітинок у буфері MoveResult.</summary>
        public int CellStart { get; }

        public int CellCount { get; }

        internal GameEvent(GameEventType type, LineKind kind, Pigment pigment, int value, int extra,
            float multiplier, bool isPure, int cellStart, int cellCount)
        {
            Type = type;
            Kind = kind;
            Pigment = pigment;
            Value = value;
            Extra = extra;
            Multiplier = multiplier;
            IsPure = isPure;
            CellStart = cellStart;
            CellCount = cellCount;
        }

        public override string ToString() => $"{Type} {Pigment} v={Value} x={Extra}";
    }
}
