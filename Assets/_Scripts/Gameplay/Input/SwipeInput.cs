using InkFlow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Новий Input System, один .inputactions на всі платформи: миша в редакторі й тач
    /// на пристрої йдуть однаковим кодом, без жодного #if (§1).
    ///
    /// Два способи зробити хід:
    ///  1. Свайп: затиснути краплю, потягнути (поріг ~20% клітинки), відпустити.
    ///  2. Тап-тап: тапнути A, тапнути сусідню B — доступність для тих, кому важкий drag.
    /// </summary>
    public sealed class SwipeInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField] private GridView gridView;
        [SerializeField] private Camera worldCamera;

        [Tooltip("Поріг спрацювання свайпу як частка розміру клітинки.")]
        [SerializeField, Range(0.05f, 0.9f)] private float swipeThresholdCellFraction = 0.2f;

        private InputActionMap _map;
        private InputAction _press;
        private InputAction _point;

        private InputRouter _router;
        private bool _dragCandidate;
        private GridPos _dragStart;
        private Vector2 _pressWorld;
        private bool _hasSelection;
        private GridPos _selection;

        public void Bind(InputRouter router) => _router = router;

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
            if (IsLocked)
            {
                _dragCandidate = false;
                return;
            }

            _pressWorld = PointerWorld();
            _dragCandidate = gridView.TryWorldToDraggableCell(_pressWorld, out _dragStart);
        }

        private void OnPressReleased(InputAction.CallbackContext _)
        {
            if (IsLocked)
            {
                _dragCandidate = false;
                return;
            }

            var releaseWorld = PointerWorld();
            var delta = releaseWorld - _pressWorld;

            if (delta.magnitude >= swipeThresholdCellFraction * gridView.CellSize)
            {
                if (_dragCandidate)
                {
                    ClearSelection();
                    var direction = DominantDirection(delta);
                    _router?.RequestMove(_dragStart, _dragStart.Neighbor(direction));
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
                ClearSelection();
                return;
            }

            if (_hasSelection)
            {
                if (cell == _selection)
                {
                    ClearSelection();
                    return;
                }

                if (GridPos.AreAdjacent(_selection, cell))
                {
                    var from = _selection;
                    ClearSelection();
                    _router?.RequestMove(from, cell);
                    return;
                }
            }

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

        private bool IsLocked => _router == null || _router.Locked;

        private Vector2 PointerWorld()
        {
            var screen = _point.ReadValue<Vector2>();
            return worldCamera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
        }

        private static Direction DominantDirection(Vector2 delta) =>
            Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x > 0 ? Direction.Right : Direction.Left)
                : (delta.y > 0 ? Direction.Up : Direction.Down);
    }
}
