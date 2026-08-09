using System.Collections;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InkFlow.UI
{
    /// <summary>
    /// Ігрове поле: єдиний, хто перетворює стрічку подій Core у видовище.
    ///
    /// Модель на момент виклику вже у ФІНАЛЬНОМУ стані — дошка лише відтворює
    /// шлях до нього подія за подією, а наприкінці робить синхронізуючий Repaint.
    /// Розійтись із моделлю вона тому не може навіть теоретично.
    ///
    /// Сітка в моделі строго квадратна. Косметичний зсув (<see cref="BoardJitter"/>)
    /// рахується один раз на клітинку й лише зміщує краплю всередині її ж клітинки:
    /// сусідство, свайпи й хрест вибуху про нього не знають.
    /// </summary>
    public sealed class BoardView : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private DropPool pool;
        [SerializeField] private RectTransform canvasRect;
        private readonly Dictionary<GridPos, DropView> _drops = new Dictionary<GridPos, DropView>(64);
        private readonly List<GridPos> _stale = new List<GridPos>(64);

        private GameSession? _session;
        private BoardGeometry _geometry;
        private int _seed;
        private float _scale = 1f;

        private readonly SwipeGesture _gesture = new SwipeGesture();
        private Coroutine? _shake;
        private Vector2 _pressLocal;
        private GridPos _pressCell;
        private bool _pressInside;

        /// <summary>Гравець просить хід. Валідність вирішує Core.</summary>
        public InputRouter Router { get; } = new InputRouter();

        public GridModel? Grid => _session?.Grid;

        public void Bind(GameSession session, int seed)
        {
            _session = session;
            _seed = seed;
            _geometry = BoardGeometry.For(session.Grid.Width, session.Grid.Height);
            _gesture.Clear();
            Repaint();
        }

        /// <summary>
        /// Одиниць канваса на px макета. Рахується з фактичної ширини полотна,
        /// а не з константи: поле — квадрат, вписаний у ширину екрана, і на
        /// вузькому пристрої воно стискається разом із нею.
        /// </summary>
        private float Scale
        {
            get
            {
                if (canvasRect == null)
                    return _scale;
                var width = canvasRect.rect.width;
                if (width > 1f)
                    _scale = width / BoardGeometry.Canvas;
                return _scale;
            }
        }

        /// <summary>Позиція центру клітинки в координатах полотна.</summary>
        public Vector2 CellToLocal(GridPos pos)
        {
            var scale = Scale;
            var jx = BoardJitter.OffsetX(_seed, pos.X, pos.Y) * _geometry.Step;
            var jy = BoardJitter.OffsetY(_seed, pos.X, pos.Y) * _geometry.Step;

            // Полотно має півот у лівому верхньому куті, тому Y від'ємний униз.
            return new Vector2(
                (_geometry.CenterX(pos.X) + jx) * scale,
                -(_geometry.CenterY(pos.Y) - jy) * scale);
        }

        /// <summary>
        /// Точка полотна → клітинка. Зворотне перетворення НЕ враховує джиттер:
        /// клітинка лишається строгим квадратом, інакше зона влучання рухалась би
        /// разом із косметикою і свайп біля межі спрацьовував би не туди.
        /// </summary>
        public bool LocalToCell(Vector2 local, out GridPos pos)
        {
            var scale = Scale;
            var x = local.x / scale - _geometry.InsetX;
            var y = -local.y / scale - _geometry.InsetY;

            var column = Mathf.FloorToInt(x / _geometry.Step);
            var rowFromTop = Mathf.FloorToInt(y / _geometry.Step);
            var row = _geometry.Height - 1 - rowFromTop;

            pos = new GridPos(column, row);
            return _session != null && _session.Grid.Contains(pos);
        }

        // ── Малювання ──

        /// <summary>Повна синхронізація дошки зі станом моделі.</summary>
        public void Repaint()
        {
            if (_session == null || pool == null)
                return;

            var grid = _session.Grid;

            _stale.Clear();
            foreach (var pair in _drops)
                if (!grid.Contains(pair.Key) || grid[pair.Key].IsEmpty)
                    _stale.Add(pair.Key);

            foreach (var pos in _stale)
                Release(pos);

            foreach (var pos in grid.AllPositions())
            {
                var cell = grid[pos];
                if (!cell.IsEmpty)
                    Show(pos, cell);
            }
        }

        private DropView? Show(GridPos pos, in Cell cell)
        {
            if (!_drops.TryGetValue(pos, out var view))
            {
                view = pool.Get();
                if (view == null)
                    return null;
                // Розмір числа залежить від сітки, тож налаштовуємо при кожній видачі
                // з пулу, а не один раз у префабі.
                view.ConfigureForBoard(_geometry.Font * Scale);
                _drops[pos] = view;
            }

            var rect = (RectTransform)view.transform;
            var size = _geometry.Blob * Scale;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = CellToLocal(pos);

            view.Show(cell.Color, cell.Density);
            view.SetNearMiss(MergeRules.IsNearMiss(cell.Density, design.NearMissFraction, _session!.Balance));
            return view;
        }

        private void Release(GridPos pos)
        {
            if (!_drops.TryGetValue(pos, out var view))
                return;
            pool.Release(view);
            _drops.Remove(pos);
        }

        // ── Програвання ходу ──

        /// <summary>
        /// Програє стрічку подій із таймінгами. Швидкість — з дизайн-системи,
        /// тож нульова тривалість дає миттєвий режим (рестарт &lt; 300 мс).
        /// </summary>
        public IEnumerator PlayEvents(MoveResult result)
        {
            if (!result.Accepted)
            {
                yield return PlayReject(result);
                Repaint();
                yield break;
            }

            foreach (var e in result.Events)
            {
                switch (e.Type)
                {
                    case GameEventType.Merge:
                        yield return PlayMerge(e);
                        break;

                    case GameEventType.Burst:
                        yield return PlayBurst(e);
                        break;

                    case GameEventType.Paint:
                    case GameEventType.Repaint:
                        PlayAppear(e);
                        break;

                    case GameEventType.Grow:
                    case GameEventType.Blur:
                        ShowAt(e.Position);
                        break;

                    case GameEventType.Thaw:
                    case GameEventType.BlotCleared:
                    case GameEventType.Refill:
                        ShowAt(e.Position);
                        break;
                }
            }

            Repaint(); // фінальна синхронізація: дошка не має права розійтися з моделлю
        }

        private IEnumerator PlayMerge(GameEvent e)
        {
            // Крапля-джерело фізично ЇДЕ до цілі, а не зникає: без цього гравець
            // не бачить, що саме злилось, коли ходів багато й вони швидкі.
            if (_drops.TryGetValue(e.Source, out var moving))
            {
                moving.SetNearMiss(false);
                var rect = (RectTransform)moving.transform;

                // Твін іде в localPosition — anchoredPosition щокадру слав би
                // OnRectTransformDimensionsChange і бруднив графіку. Ці простори
                // зсунуті одне відносно одного на сталу величину, тому рухаємось
                // по ДЕЛЬТІ між клітинками: вона однакова в обох.
                var from = rect.localPosition;
                var shift = (Vector3)(CellToLocal(e.Position) - CellToLocal(e.Source));

                var duration = design.BoardMergeDuration;
                for (var t = 0f; t < duration; t += Time.deltaTime)
                {
                    var k = design.CurveLand.Evaluate(Mathf.Clamp01(t / duration));
                    rect.localPosition = from + shift * k;
                    yield return null;
                }

                Release(e.Source);
            }

            var view = Show(e.Position, new Cell(e.Color, e.Value));
            view?.PlayLand();
        }

        private IEnumerator PlayReject(MoveResult result)
        {
            // Відхилений свайп: коротке відскакування в бік цілі й назад.
            // Хід не витрачається — анімація має це підтверджувати, а не імітувати дію.
            if (result.Events.Count == 0)
                yield break;

            var e = result.Events[0];
            if (!_drops.TryGetValue(e.Source, out var view))
                yield break;

            var rect = (RectTransform)view.transform;
            var origin = rect.localPosition;
            var peak = origin + (Vector3)(CellToLocal(e.Position) - CellToLocal(e.Source))
                * design.BoardRejectFraction;

            var duration = design.BoardRejectDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                rect.localPosition = Vector3.LerpUnclamped(origin, peak, k);
                yield return null;
            }

            rect.localPosition = origin;
        }

        private IEnumerator PlayBurst(GameEvent e)
        {
            if (_drops.TryGetValue(e.Position, out var view))
            {
                // Анімацію веде сама крапля: її LateUpdate щокадру пише localScale,
                // тож зовнішній твін масштабу вона просто затирала б.
                yield return view.BurstRoutine(design.BoardBurstDuration);
                Release(e.Position);
            }

            Shake(e.ChainIndex + 1);

            if (design.BoardInterBurstDelay > 0f)
                yield return new WaitForSeconds(design.BoardInterBurstDelay);
        }

        private void PlayAppear(GameEvent e)
        {
            var view = ShowAt(e.Position);
            view?.PlayLand();
        }

        private DropView? ShowAt(GridPos pos)
        {
            if (_session == null || !_session.Grid.Contains(pos))
                return null;

            var cell = _session.Grid[pos];
            if (cell.IsEmpty)
            {
                Release(pos);
                return null;
            }

            return Show(pos, cell);
        }

        // ── Жести ──

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Router.Locked || _session == null || _session.IsOver)
                return;

            _pressLocal = ToLocal(eventData);
            _pressInside = LocalToCell(_pressLocal, out _pressCell) && !_session.Grid[_pressCell].IsEmpty;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Router.Locked || _session == null || _session.IsOver)
                return;

            var releaseLocal = ToLocal(eventData);
            var delta = releaseLocal - _pressLocal;
            var insideRelease = LocalToCell(releaseLocal, out var releaseCell);
            var occupied = insideRelease && !_session.Grid[releaseCell].IsEmpty;

            var result = _gesture.Release(
                _pressCell, _pressInside,
                releaseCell, insideRelease, occupied,
                // Y на екрані росте вниз, у моделі — вгору. Перевертаємо ТУТ,
                // щоб правило жесту в Core працювало в координатах моделі.
                delta.x, -delta.y,
                _geometry.Step * Scale);

            _pressInside = false;
            ApplyGesture(result);
        }

        private void ApplyGesture(GestureResult result)
        {
            switch (result.Outcome)
            {
                case GestureOutcome.Move:
                    Highlight(result.From, false);
                    Router.RequestMove(result.From, result.To);
                    break;

                case GestureOutcome.Selected:
                    Highlight(result.From, true);
                    break;

                case GestureOutcome.Cleared:
                    Highlight(result.From, false);
                    break;
            }
        }

        /// <summary>Знімає вибір — наприклад, коли партія закінчилась або йде рестарт.</summary>
        public void ClearSelection() => ApplyGesture(_gesture.Clear());

        private void Highlight(GridPos pos, bool on)
        {
            if (_drops.TryGetValue(pos, out var view))
                view.SetSelected(on);
        }

        /// <summary>Тряска поля з важкої ланки ланцюга. Полотно, не окремі краплі.</summary>
        private void Shake(int link)
        {
            if (canvasRect == null || link < design.BoardShakeFromLink)
                return;
            if (_shake != null)
                StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine(link - design.BoardShakeFromLink + 1));
        }

        private IEnumerator ShakeRoutine(int strength)
        {
            var origin = canvasRect!.localPosition;
            var amplitude = design.BoardShakeAmplitude * Mathf.Min(strength, 4);
            var duration = design.BoardShakeDuration;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var decay = 1f - t / duration;
                var offset = Mathf.Sin(t / duration * Mathf.PI * 7f) * amplitude * decay;
                canvasRect.localPosition = origin + new Vector3(offset, offset * 0.4f, 0f);
                yield return null;
            }

            canvasRect.localPosition = origin;
            _shake = null;
        }

        private Vector2 ToLocal(PointerEventData eventData)
        {
            var rect = canvasRect != null ? canvasRect : (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out var local);
            return local;
        }
    }
}
