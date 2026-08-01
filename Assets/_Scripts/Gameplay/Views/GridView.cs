using System.Collections;
using System.Collections.Generic;
using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// ЄДИНИЙ, хто перетворює стрічку подій Core у видовище (§8).
    /// Модель уже у фінальному стані — в'ю відтворює шлях до нього подія за подією,
    /// а наприкінці робить синхронізуючий Repaint, щоб розбіжність була неможливою.
    /// Координати: X — колонка, Y — ряд знизу вгору (як у моделі).
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        [SerializeField] private CellPool cellPool;
        [SerializeField] private ParticlePool particlePool;
        [SerializeField] private FeelConfig feel;
        [SerializeField] private ShakeController shaker;
        [SerializeField] private ChainFeedback feedback;

        [Header("Геометрія")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float cellGap = 0.08f;

        [Header("Палітра")]
        [Tooltip("Кольори за індексом InkColor: [0] None не використовується.")]
        [SerializeField]
        private Color[] palette =
        {
            new Color32(40, 42, 60, 0),      // None
            new Color32(255, 64, 129, 255),  // Magenta
            new Color32(0, 191, 165, 255),   // Cyan
            new Color32(255, 171, 64, 255),  // Amber
            new Color32(174, 234, 0, 255),   // Lime
            new Color32(124, 77, 255, 255),  // Violet
            new Color32(244, 143, 177, 255)  // Rose
        };

        private readonly Dictionary<GridPos, CellView> _views = new Dictionary<GridPos, CellView>();
        private readonly List<GridPos> _stale = new List<GridPos>(64);
        private GridModel _grid;
        private int _burstThreshold = 10;

        public float CellSize => cellSize;
        private float Spacing => cellSize + cellGap;

        public void Bind(GameSession session)
        {
            _grid = session.Grid;
            _burstThreshold = session.Balance.BurstThreshold;
            Repaint();
        }

        /// <summary>Повна синхронізація в'ю зі станом моделі.</summary>
        public void Repaint()
        {
            if (_grid == null)
                return;

            _stale.Clear();
            foreach (var pair in _views)
                if (!_grid.Contains(pair.Key) || IsInvisible(_grid[pair.Key]))
                    _stale.Add(pair.Key);

            foreach (var pos in _stale)
            {
                cellPool.Release(_views[pos]);
                _views.Remove(pos);
            }

            foreach (var pos in _grid.AllPositions())
            {
                var cell = _grid[pos];
                if (!IsInvisible(cell))
                    ShowCell(pos, cell);
            }
        }

        /// <summary>Порожню клітинку без модифікаторів не показуємо взагалі.</summary>
        private static bool IsInvisible(in Cell cell) => cell.IsEmpty && cell.Flags == CellFlags.None;

        /// <summary>
        /// Програє стрічку подій із таймінгами. Швидкість — з FeelConfig, тож
        /// AnimationSpeed = 0 дає миттєвий режим (тести, «швидкий рестарт &lt; 300 мс»).
        /// </summary>
        public IEnumerator PlayEvents(MoveResult result)
        {
            if (!result.Accepted)
            {
                yield return PlayReject(result);
                Repaint();
                yield break;
            }

            var animatedLinks = 0;

            foreach (var e in result.Events)
            {
                switch (e.Type)
                {
                    case GameEventType.Merge:
                        yield return PlayMerge(e);
                        break;

                    case GameEventType.Burst:
                        animatedLinks = e.ChainIndex + 1;
                        yield return PlayBurst(e, animate: e.ChainIndex < feel.MaxAnimatedLinks);
                        break;

                    case GameEventType.Paint:
                    case GameEventType.Repaint:
                        PlayAppear(e, animate: e.ChainIndex < feel.MaxAnimatedLinks);
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

                    case GameEventType.Splash:
                        if (particlePool != null && e.ChainIndex < feel.MaxAnimatedLinks)
                            particlePool.PlayBurst(CellToWorld(e.Position), ColorOf(e.Color), 0.5f);
                        break;

                    case GameEventType.BossAction:
                    case GameEventType.BossSegmentPainted:
                    case GameEventType.BossSegmentRepainted:
                        // Бос малюється власним BossView — він слухає ті самі події.
                        break;
                }
            }

            _ = animatedLinks;
            Repaint(); // фінальна синхронізація: в'ю не має права розійтися з моделлю
        }

        private IEnumerator PlayMerge(GameEvent e)
        {
            feedback?.PlayMerge();

            if (_views.TryGetValue(e.Source, out var moving))
            {
                moving.SetNearMiss(false);
                yield return Tween.Move(
                    moving.transform, CellToWorld(e.Source), CellToWorld(e.Position), feel.MergeFlowDuration);
                ReleaseAt(e.Source);
            }

            var view = ShowCell(e.Position, new Cell(e.Color, e.Value));
            yield return Tween.Pulse(view.transform, 1f, feel.MergePulseScale, feel.MergePulseDuration);
        }

        private IEnumerator PlayReject(MoveResult result)
        {
            // Відхилений свайп: коротке «відскакування» краплі в бік цілі й назад.
            // Хід не витрачається — анімація має це підтверджувати, а не імітувати дію.
            if (result.Events.Count == 0)
                yield break;

            var e = result.Events[0];
            if (!_views.TryGetValue(e.Source, out var view))
                yield break;

            var origin = CellToWorld(e.Source);
            var peak = Vector3.LerpUnclamped(origin, CellToWorld(e.Position), feel.RejectBounceFraction);
            yield return Tween.Bounce(view.transform, origin, peak, feel.RejectBounceDuration);
        }

        private IEnumerator PlayBurst(GameEvent e, bool animate)
        {
            feedback?.PlayBurst(e.ChainIndex);

            var linkNumber = e.ChainIndex + 1;
            if (shaker != null && linkNumber >= feel.ShakeFromChainLink)
                shaker.Shake(0.5f + 0.25f * (linkNumber - feel.ShakeFromChainLink));

            if (_views.TryGetValue(e.Position, out var view))
            {
                view.SetNearMiss(false);
                if (animate)
                    yield return Tween.Scale(view.transform, Vector3.one, Vector3.zero, feel.BurstShrinkDuration);
                ReleaseAt(e.Position);
            }

            if (particlePool != null && animate)
            {
                // Важчий вибух — помітно більший сплеск (feel росте з силою, §5.3).
                var scale = Mathf.Clamp(0.8f + e.Value / 40f, 0.8f, 2.5f);
                particlePool.PlayBurst(CellToWorld(e.Position), ColorOf(e.Color), scale);
            }

            if (animate && feel.InterBurstDelay > 0f)
                yield return new WaitForSeconds(feel.InterBurstDelay);
        }

        private void PlayAppear(GameEvent e, bool animate)
        {
            var isNew = !_views.ContainsKey(e.Position);
            var view = ShowCell(e.Position, new Cell(e.Color, e.Value));
            if (animate && isNew)
                StartCoroutine(Tween.Scale(view.transform, Vector3.zero, Vector3.one, feel.PaintPopDuration));
        }

        private void ShowAt(GridPos pos)
        {
            if (_grid == null || !_grid.Contains(pos))
                return;
            var cell = _grid[pos];
            if (IsInvisible(cell))
                ReleaseAt(pos);
            else
                ShowCell(pos, cell);
        }

        private CellView ShowCell(GridPos pos, in Cell cell)
        {
            if (!_views.TryGetValue(pos, out var view))
            {
                view = cellPool.Get();
                view.transform.position = CellToWorld(pos);
                _views.Add(pos, view);
            }

            view.Show(cell, ColorOf(cell.Color));
            // Near-miss: крапля на порозі вибуху пульсує — гравець бачить «майже».
            view.SetNearMiss(!cell.IsEmpty && cell.Density >= feel.NearMissFraction * _burstThreshold);
            return view;
        }

        private void ReleaseAt(GridPos pos)
        {
            if (!_views.TryGetValue(pos, out var view))
                return;
            cellPool.Release(view);
            _views.Remove(pos);
        }

        public void SetSelected(GridPos pos, bool selected)
        {
            if (_views.TryGetValue(pos, out var view))
                view.SetSelected(selected);
        }

        // ---------- Геометрія ----------

        public Vector3 CellToWorld(GridPos pos)
        {
            var origin = OriginOffset();
            return transform.position + new Vector3(pos.X * Spacing + origin.x, pos.Y * Spacing + origin.y, 0f);
        }

        public bool TryWorldToCell(Vector2 world, out GridPos pos)
        {
            pos = default;
            if (_grid == null)
                return false;

            var origin = OriginOffset();
            var local = world - (Vector2)transform.position - origin;
            var x = Mathf.RoundToInt(local.x / Spacing);
            var y = Mathf.RoundToInt(local.y / Spacing);
            var candidate = new GridPos(x, y);
            if (!_grid.Contains(candidate))
                return false;

            pos = candidate;
            return true;
        }

        public bool TryWorldToDraggableCell(Vector2 world, out GridPos pos) =>
            TryWorldToCell(world, out pos) && !_grid[pos].IsEmpty;

        public bool IsOccupied(GridPos pos) =>
            _grid != null && _grid.Contains(pos) && !_grid[pos].IsEmpty;

        private Vector2 OriginOffset()
        {
            var halfX = (_grid?.Width ?? 1) - 1;
            var halfY = (_grid?.Height ?? 1) - 1;
            return new Vector2(-halfX * Spacing * 0.5f, -halfY * Spacing * 0.5f);
        }

        public Color ColorOf(InkColor color)
        {
            var index = (int)color;
            return index >= 0 && index < palette.Length ? palette[index] : Color.white;
        }
    }
}
