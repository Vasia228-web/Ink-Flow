using System.Collections.Generic;
using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Базовий рендер сітки (Фаза 1, без анімацій): після кожного ходу передиффує
    /// стан GridModel і оновлює CellView через CellPool — жодних Instantiate/Destroy.
    /// Сітка відцентрована навколо transform; row 0 моделі — нижній ряд світу.
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        [SerializeField] private CellPool pool;

        [Tooltip("Розмір клітинки у world units.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Проміжок між клітинками у world units.")]
        [SerializeField] private float cellGap = 0.08f;

        [Tooltip("Палітра кольорів фарби; індекс = Cell.Color з моделі.")]
        [SerializeField] private Color[] colorPalette =
        {
            new Color32(61, 90, 254, 255),   // 0 індиго
            new Color32(255, 64, 129, 255),  // 1 маджента
            new Color32(0, 191, 165, 255),   // 2 бірюзовий
            new Color32(255, 171, 64, 255),  // 3 помаранчевий
            new Color32(124, 77, 255, 255),  // 4 фіолетовий
            new Color32(174, 234, 0, 255)    // 5 лайм
        };

        private readonly Dictionary<GridPos, CellView> _activeViews = new Dictionary<GridPos, CellView>();
        private readonly List<GridPos> _toRelease = new List<GridPos>(64);
        private GridModel _grid;
        private int _burstThreshold;

        public float CellSize => cellSize;
        private float Spacing => cellSize + cellGap;

        /// <summary>Викликається на старті сесії: поріг потрібен для near-miss підсвітки.</summary>
        public void SetLevel(LevelData level) => _burstThreshold = level.BurstThreshold;

        /// <summary>Повна перемальовка під поточний стан моделі.</summary>
        public void Repaint(GridModel grid)
        {
            _grid = grid;

            _toRelease.Clear();
            foreach (var entry in _activeViews)
                if (!grid.IsInside(entry.Key) || grid[entry.Key].IsEmpty)
                    _toRelease.Add(entry.Key);

            foreach (var pos in _toRelease)
            {
                pool.Release(_activeViews[pos]);
                _activeViews.Remove(pos);
            }

            foreach (var pos in grid.AllPositions())
            {
                var cell = grid[pos];
                if (!cell.IsEmpty)
                    ShowCell(pos, cell);
            }
        }

        // ---------- Поштучний доступ для GridAnimator ----------

        /// <summary>Отримати-або-заспавнити в'ю клітинки і показати стан cell.</summary>
        public CellView ShowCell(GridPos pos, Cell cell)
        {
            if (!_activeViews.TryGetValue(pos, out var view))
            {
                view = pool.Get();
                view.transform.position = CellToWorld(pos);
                _activeViews.Add(pos, view);
            }

            view.Show(cell, ColorForIndex(cell.Color));
            // Near-miss: >= 80% порогу — клітинка «на межі», пульсує світлішим (ТЗ Фази 2).
            view.SetNearMiss(_burstThreshold > 0 && cell.Density >= 0.8f * _burstThreshold);
            return view;
        }

        public bool TryGetView(GridPos pos, out CellView view) =>
            _activeViews.TryGetValue(pos, out view);

        /// <summary>Повернути в'ю клітинки в пул (напр. джерело merge після переливання).</summary>
        public void ReleaseAt(GridPos pos)
        {
            if (!_activeViews.TryGetValue(pos, out var view))
                return;
            pool.Release(view);
            _activeViews.Remove(pos);
        }

        public void SetSelected(GridPos pos, bool selected)
        {
            if (_activeViews.TryGetValue(pos, out var view))
                view.SetSelected(selected);
        }

        public Vector3 CellToWorld(GridPos pos)
        {
            var origin = OriginOffset();
            return transform.position + new Vector3(pos.Col * Spacing + origin.x, pos.Row * Spacing + origin.y, 0f);
        }

        /// <summary>Світова точка → клітинка сітки (false, якщо поза сіткою).</summary>
        public bool TryWorldToCell(Vector2 world, out GridPos pos)
        {
            pos = default;
            if (_grid == null)
                return false;

            var origin = OriginOffset();
            var local = world - (Vector2)transform.position - origin;
            var col = Mathf.RoundToInt(local.x / Spacing);
            var row = Mathf.RoundToInt(local.y / Spacing);
            if (!_grid.IsInside(row, col))
                return false;

            pos = new GridPos(row, col);
            return true;
        }

        /// <summary>Як TryWorldToCell, але тільки для непорожніх клітинок (старт свайпу/вибору).</summary>
        public bool TryWorldToOccupiedCell(Vector2 world, out GridPos pos) =>
            TryWorldToCell(world, out pos) && !_grid[pos].IsEmpty;

        public bool IsOccupied(GridPos pos) =>
            _grid != null && _grid.IsInside(pos) && !_grid[pos].IsEmpty;

        private Vector2 OriginOffset()
        {
            var size = _grid?.Size ?? 0;
            var half = (size - 1) * Spacing * 0.5f;
            return new Vector2(-half, -half);
        }

        /// <summary>Колір палітри за індексом моделі (потрібен і часткам вибуху).</summary>
        public Color ColorForIndex(int colorIndex) =>
            colorPalette.Length > 0
                ? colorPalette[Mathf.Clamp(colorIndex, 0, colorPalette.Length - 1)]
                : Color.white;
    }
}
