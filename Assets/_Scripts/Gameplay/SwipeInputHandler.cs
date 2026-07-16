using System;
using InkFlow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Обробка вводу через новий Input System. Одні й ті самі дії для Editor (mouse)
    /// і мобільного білду (touch) — без окремих гілок коду.
    ///
    /// Два способи зробити хід:
    ///  1. Свайп: затиснути на клітинці A, потягнути (поріг ~20% розміру клітинки),
    ///     відпустити — напрямок визначається по домінантній осі drag-вектора.
    ///  2. Tap-tap (accessibility): тапнути A (вибір з підсвіткою), тапнути сусідню B.
    /// </summary>
    public sealed class SwipeInputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField] private GridView gridView;
        [SerializeField] private Camera worldCamera;

        [Tooltip("Поріг спрацювання свайпу як частка розміру клітинки.")]
        [SerializeField, Range(0.05f, 0.9f)] private float swipeThresholdCellFraction = 0.2f;

        /// <summary>Гравець просить хід: клітинка + напрямок. Валідність вирішує GameSession.</summary>
        public event Action<GridPos, Direction> MoveRequested;

        /// <summary>true — жести ігноруються (йде анімація ходу). Ставить GridController.</summary>
        public bool InputLocked { get; set; }

        private InputActionMap _map;
        private InputAction _press;
        private InputAction _point;

        private bool _dragCandidate;
        private GridPos _dragStart;
        private Vector2 _pressWorld;

        private bool _hasSelection;
        private GridPos _selection;

        private void Awake()
        {
            _map = actionsAsset.FindActionMap("Gameplay", throwIfNotFound: true);
            _press = _map.FindAction("Press", throwIfNotFound: true);
            _point = _map.FindAction("Point", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _press.started += OnPressStarted;
            _press.canceled += OnPressReleased;
            _map.Enable();
        }

        private void OnDisable()
        {
            _map.Disable();
            _press.started -= OnPressStarted;
            _press.canceled -= OnPressReleased;
        }

        private void OnPressStarted(InputAction.CallbackContext _)
        {
            if (InputLocked)
            {
                _dragCandidate = false;
                return;
            }

            _pressWorld = PointerWorld();
            _dragCandidate = gridView.TryWorldToOccupiedCell(_pressWorld, out _dragStart);
        }

        private void OnPressReleased(InputAction.CallbackContext _)
        {
            if (InputLocked)
            {
                _dragCandidate = false;
                return;
            }

            var releaseWorld = PointerWorld();
            var delta = releaseWorld - _pressWorld;
            var isSwipe = delta.magnitude >= swipeThresholdCellFraction * gridView.CellSize;

            if (isSwipe)
            {
                // Свайп рахується лише якщо жест почався на непорожній клітинці.
                if (_dragCandidate)
                {
                    ClearSelection();
                    MoveRequested?.Invoke(_dragStart, DominantDirection(delta));
                }
            }
            else
            {
                HandleTap(releaseWorld);
            }

            _dragCandidate = false;
        }

        private void HandleTap(Vector2 world)
        {
            if (!gridView.TryWorldToCell(world, out var cell))
            {
                ClearSelection(); // тап поза сіткою знімає вибір
                return;
            }

            if (_hasSelection)
            {
                if (cell == _selection)
                {
                    ClearSelection();
                    return;
                }

                if (GameRules.AreAdjacent(_selection, cell))
                {
                    var from = _selection;
                    ClearSelection();
                    MoveRequested?.Invoke(from, DirectionTo(from, cell));
                    return;
                }
            }

            // Нема вибору або тап далеко: вибрати непорожню клітинку чи зняти вибір.
            if (gridView.IsOccupied(cell))
                Select(cell);
            else
                ClearSelection();
        }

        private void Select(GridPos cell)
        {
            ClearSelection();
            _hasSelection = true;
            _selection = cell;
            gridView.SetSelected(cell, true);
        }

        private void ClearSelection()
        {
            if (!_hasSelection)
                return;
            gridView.SetSelected(_selection, false);
            _hasSelection = false;
        }

        private Vector2 PointerWorld()
        {
            var screen = _point.ReadValue<Vector2>();
            var world = worldCamera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            return world;
        }

        private static Direction DominantDirection(Vector2 delta) =>
            Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x > 0 ? Direction.Right : Direction.Left)
                : (delta.y > 0 ? Direction.Up : Direction.Down);

        private static Direction DirectionTo(GridPos from, GridPos to)
        {
            if (to.Row > from.Row) return Direction.Up;
            if (to.Row < from.Row) return Direction.Down;
            return to.Col > from.Col ? Direction.Right : Direction.Left;
        }
    }
}
