namespace InkFlow.Core
{
    /// <summary>Що гравець зробив жестом.</summary>
    public enum GestureOutcome : byte
    {
        /// <summary>Нічого: жест не склався або лише змінив підсвітку.</summary>
        None = 0,

        /// <summary>Є хід: From → To.</summary>
        Move = 1,

        /// <summary>Клітинку взято в тап-тап вибір.</summary>
        Selected = 2,

        /// <summary>Вибір знято.</summary>
        Cleared = 3
    }

    /// <summary>Результат жесту: що робити далі.</summary>
    public readonly struct GestureResult
    {
        public GestureResult(GestureOutcome outcome, GridPos from, GridPos to)
        {
            Outcome = outcome;
            From = from;
            To = to;
        }

        public GestureOutcome Outcome { get; }
        public GridPos From { get; }
        public GridPos To { get; }

        public static readonly GestureResult None =
            new GestureResult(GestureOutcome.None, default, default);
    }

    /// <summary>
    /// Правила жесту, без жодного пікселя й без UnityEngine.
    ///
    /// Два способи зробити хід, обидва з майстер-доку:
    ///  1. Свайп: затиснути краплю, потягнути далі за поріг, відпустити.
    ///  2. Тап-тап: тапнути A, тапнути сусідню B — для тих, кому важкий drag.
    ///
    /// Живе в Core саме тому, що дощок дві: світова (SpriteRenderer) і екранна (UGUI).
    /// Поки правило «що вважати свайпом» жило в кожній із них окремо, вони могли
    /// розійтись — і гра поводилась би по-різному залежно від екрана.
    /// </summary>
    public sealed class SwipeGesture
    {
        /// <summary>Поріг свайпу як частка розміру клітинки.</summary>
        public const float DefaultThresholdFraction = 0.2f;

        private readonly float _thresholdFraction;
        private bool _hasSelection;
        private GridPos _selection;

        public SwipeGesture(float thresholdFraction = DefaultThresholdFraction)
        {
            _thresholdFraction = thresholdFraction > 0f ? thresholdFraction : DefaultThresholdFraction;
        }

        /// <summary>Чи є зараз вибрана клітинка (її треба підсвічувати).</summary>
        public bool HasSelection => _hasSelection;

        public GridPos Selection => _selection;

        /// <summary>
        /// Жест завершився. Уся геометрія вже переведена в одиниці клітинки:
        /// <paramref name="dx"/>/<paramref name="dy"/> — зсув у тих самих одиницях,
        /// що й <paramref name="cellSize"/>, з віссю Y ВГОРУ (як у моделі).
        /// </summary>
        /// <param name="pressedCell">Клітинка під початком жесту (якщо була).</param>
        /// <param name="hasPressedCell">Чи взагалі почався жест на клітинці з краплею.</param>
        /// <param name="releasedCell">Клітинка під кінцем жесту.</param>
        /// <param name="hasReleasedCell">Чи кінець жесту потрапив у сітку.</param>
        /// <param name="releasedOccupied">Чи в кінцевій клітинці є крапля.</param>
        public GestureResult Release(
            GridPos pressedCell, bool hasPressedCell,
            GridPos releasedCell, bool hasReleasedCell, bool releasedOccupied,
            float dx, float dy, float cellSize)
        {
            var threshold = _thresholdFraction * cellSize;
            var isSwipe = dx * dx + dy * dy >= threshold * threshold;

            if (isSwipe)
            {
                if (!hasPressedCell)
                    return Clear();

                // Напрямок береться з ДОМІНУЮЧОЇ осі, а не з клітинки під пальцем:
                // діагональний зсув мусить дати рівно один ортогональний хід,
                // інакше палець вирішував би за гравця.
                ClearSilently();
                var direction = DominantDirection(dx, dy);
                return new GestureResult(GestureOutcome.Move, pressedCell, pressedCell.Neighbor(direction));
            }

            return Tap(releasedCell, hasReleasedCell, releasedOccupied);
        }

        /// <summary>Короткий дотик: другий крок тап-тапу або новий вибір.</summary>
        public GestureResult Tap(GridPos cell, bool insideGrid, bool occupied)
        {
            if (!insideGrid)
                return Clear();

            if (_hasSelection)
            {
                if (cell == _selection)
                    return Clear();

                if (GridPos.AreAdjacent(_selection, cell))
                {
                    var from = _selection;
                    ClearSilently();
                    return new GestureResult(GestureOutcome.Move, from, cell);
                }
            }

            if (!occupied)
                return Clear();

            var previous = _hasSelection;
            _hasSelection = true;
            _selection = cell;
            _ = previous;
            return new GestureResult(GestureOutcome.Selected, cell, cell);
        }

        /// <summary>Скидає вибір ззовні — наприклад, коли партія закінчилась.</summary>
        public GestureResult Clear()
        {
            if (!_hasSelection)
                return GestureResult.None;
            var previous = _selection;
            ClearSilently();
            return new GestureResult(GestureOutcome.Cleared, previous, previous);
        }

        private void ClearSilently()
        {
            _hasSelection = false;
            _selection = default;
        }

        public static Direction DominantDirection(float dx, float dy)
        {
            var ax = dx < 0f ? -dx : dx;
            var ay = dy < 0f ? -dy : dy;
            if (ax >= ay)
                return dx > 0f ? Direction.Right : Direction.Left;
            return dy > 0f ? Direction.Up : Direction.Down;
        }
    }
}
