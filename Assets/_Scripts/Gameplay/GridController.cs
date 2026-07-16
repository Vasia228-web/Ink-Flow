using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Зшиває шари: слухає інпут, ганяє хід через GameSession (чиста логіка)
    /// і просить GridView перемалюватись. Retry — миттєвий Reset сесії без
    /// перезавантаження сцени.
    /// </summary>
    public sealed class GridController : MonoBehaviour
    {
        [SerializeField] private GridView view;
        [SerializeField] private SwipeInputHandler input;

        private GameSession _session;

        public void StartSession(LevelData level)
        {
            DetachSession();
            _session = new GameSession(level);
            _session.SessionReset += OnSessionReset;
            GameEvents.RaiseSessionStarted(_session);
            view.Repaint(_session.Grid);
        }

        private void OnEnable()
        {
            input.MoveRequested += OnMoveRequested;
            GameEvents.RetryRequested += OnRetryRequested;
        }

        private void OnDisable()
        {
            input.MoveRequested -= OnMoveRequested;
            GameEvents.RetryRequested -= OnRetryRequested;
        }

        private void OnDestroy() => DetachSession();

        private void OnMoveRequested(GridPos from, Direction direction)
        {
            if (_session == null)
                return;

            _session.TryMove(from, direction);
            view.Repaint(_session.Grid);
        }

        private void OnRetryRequested() => _session?.Reset();

        private void OnSessionReset() => view.Repaint(_session.Grid);

        private void DetachSession()
        {
            if (_session != null)
                _session.SessionReset -= OnSessionReset;
            _session = null;
        }
    }
}
