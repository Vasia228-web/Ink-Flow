namespace InkFlow.Core
{
    /// <summary>Тип події ходу. Порядок значень не важливий, але дописувати краще в кінець.</summary>
    public enum GameEventType : byte
    {
        /// <summary>Дві краплі злились: Source → Position, Value = підсумкова густота.</summary>
        Merge,

        /// <summary>Крапля лопнула: Value = сила (фінальна густота), ChainIndex = номер у ланцюгу.</summary>
        Burst,

        /// <summary>Порожня клітинка пофарбована: Value = нова густота (= сила фарбування або 1 для бризки).</summary>
        Paint,

        /// <summary>Клітинка того ж кольору підросла: Value = нова густота.</summary>
        Grow,

        /// <summary>Розмивання чужого кольору: Value = густота, що лишилась (> 0).</summary>
        Blur,

        /// <summary>Чужа крапля розмита до нуля і перефарбована: Color = новий колір, Value = 1.</summary>
        Repaint,

        /// <summary>Бризка полетіла з Source у Position (сама дія на клітинку йде окремою подією).</summary>
        Splash,

        /// <summary>Лід розтанув від вибуху.</summary>
        Thaw,

        /// <summary>Кляксу боса змито сусіднім вибухом.</summary>
        BlotCleared,

        /// <summary>Частина вибуху вилилась за межі сітки: Position — куди саме (може бути поза сіткою).</summary>
        OutOfBounds,

        /// <summary>Вибух дострілив до Клякса через верхній край: Value = сила.</summary>
        BossHit,

        /// <summary>Сегмент боса пофарбовано: Extra = індекс сегмента, Color = новий колір.</summary>
        BossSegmentPainted,

        /// <summary>Сегмент боса перефарбовано чужим вибухом (псує 3★): Extra = індекс сегмента.</summary>
        BossSegmentRepainted,

        /// <summary>Бос виконав дію: Extra = (int)BossActionType, Position/Color — ціль дії.</summary>
        BossAction,

        /// <summary>Бос телеграфує намір на наступний хід: Extra = (int)BossActionType.</summary>
        BossTelegraph,

        /// <summary>Дозаправка Endless: клітинка залита новою краплею.</summary>
        Refill,

        /// <summary>Ланцюг обірвано запобіжником MaxChainBursts (§5.5) — має бути видно в логах і тестах.</summary>
        ChainTruncated,

        /// <summary>Після ходу не лишилось жодного дозволеного свайпу.</summary>
        Deadlock
    }

    /// <summary>
    /// Одна подія ходу. Struct-union замість ієрархії класів: за ходом із довгим ланцюгом
    /// це сотні подій, а бюджет — нуль GC-алокацій під час ходу (§12).
    /// В'ю розбирає їх через switch по Type (§8).
    /// </summary>
    public readonly struct GameEvent
    {
        public GameEventType Type { get; }

        /// <summary>Головна клітинка події.</summary>
        public GridPos Position { get; }

        /// <summary>Друга клітинка: звідки злиття / звідки бризка.</summary>
        public GridPos Source { get; }

        public InkColor Color { get; }

        /// <summary>Числове значення: густота, сила вибуху — залежно від Type (див. GameEventType).</summary>
        public int Value { get; }

        /// <summary>Номер вибуху в ланцюгу (0 — перший). Для подій поза ланцюгом — 0.</summary>
        public int ChainIndex { get; }

        /// <summary>Додатковий параметр: індекс сегмента боса, тип дії боса тощо.</summary>
        public int Extra { get; }

        private GameEvent(GameEventType type, GridPos position, GridPos source, InkColor color,
            int value, int chainIndex, int extra)
        {
            Type = type;
            Position = position;
            Source = source;
            Color = color;
            Value = value;
            ChainIndex = chainIndex;
            Extra = extra;
        }

        public static GameEvent Merge(GridPos from, GridPos to, InkColor color, int mergedDensity) =>
            new GameEvent(GameEventType.Merge, to, from, color, mergedDensity, 0, 0);

        public static GameEvent Burst(GridPos at, InkColor color, int force, int chainIndex) =>
            new GameEvent(GameEventType.Burst, at, at, color, force, chainIndex, 0);

        public static GameEvent Paint(GridPos at, InkColor color, int density, int chainIndex) =>
            new GameEvent(GameEventType.Paint, at, at, color, density, chainIndex, 0);

        public static GameEvent Grow(GridPos at, InkColor color, int density, int chainIndex) =>
            new GameEvent(GameEventType.Grow, at, at, color, density, chainIndex, 0);

        public static GameEvent Blur(GridPos at, InkColor color, int densityLeft, int chainIndex) =>
            new GameEvent(GameEventType.Blur, at, at, color, densityLeft, chainIndex, 0);

        public static GameEvent Repaint(GridPos at, InkColor newColor, int chainIndex) =>
            new GameEvent(GameEventType.Repaint, at, at, newColor, 1, chainIndex, 0);

        public static GameEvent Splash(GridPos from, GridPos to, InkColor color, int chainIndex) =>
            new GameEvent(GameEventType.Splash, to, from, color, 1, chainIndex, 0);

        public static GameEvent Thaw(GridPos at, int chainIndex) =>
            new GameEvent(GameEventType.Thaw, at, at, InkColor.None, 0, chainIndex, 0);

        public static GameEvent BlotCleared(GridPos at, int chainIndex) =>
            new GameEvent(GameEventType.BlotCleared, at, at, InkColor.None, 0, chainIndex, 0);

        public static GameEvent OutOfBounds(GridPos from, GridPos target, InkColor color, int chainIndex) =>
            new GameEvent(GameEventType.OutOfBounds, target, from, color, 0, chainIndex, 0);

        public static GameEvent BossHit(GridPos from, InkColor color, int force, int chainIndex) =>
            new GameEvent(GameEventType.BossHit, from, from, color, force, chainIndex, 0);

        public static GameEvent BossSegmentPainted(int segment, InkColor color, int chainIndex) =>
            new GameEvent(GameEventType.BossSegmentPainted, default, default, color, 0, chainIndex, segment);

        public static GameEvent BossSegmentRepainted(int segment, InkColor color, int chainIndex) =>
            new GameEvent(GameEventType.BossSegmentRepainted, default, default, color, 0, chainIndex, segment);

        public static GameEvent BossAction(BossActionType action, GridPos target, InkColor color, int segment) =>
            new GameEvent(GameEventType.BossAction, target, target, color, 0, 0, (int)action | (segment << 8));

        public static GameEvent BossTelegraph(BossActionType action) =>
            new GameEvent(GameEventType.BossTelegraph, default, default, InkColor.None, 0, 0, (int)action);

        public static GameEvent Refill(GridPos at, InkColor color, int density) =>
            new GameEvent(GameEventType.Refill, at, at, color, density, 0, 0);

        public static GameEvent ChainTruncated(GridPos at, int chainIndex) =>
            new GameEvent(GameEventType.ChainTruncated, at, at, InkColor.None, 0, chainIndex, 0);

        public static GameEvent Deadlock() =>
            new GameEvent(GameEventType.Deadlock, default, default, InkColor.None, 0, 0, 0);

        /// <summary>Тип дії боса, спакований у Extra.</summary>
        public BossActionType ActionType => (BossActionType)(Extra & 0xFF);

        /// <summary>Індекс сегмента боса, спакований у Extra (для BossAction).</summary>
        public int ActionSegment => Extra >> 8;

        public override string ToString() => $"{Type}@{Position} {Color}:{Value} chain#{ChainIndex}";
    }
}
