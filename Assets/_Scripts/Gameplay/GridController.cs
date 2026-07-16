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
        [SerializeField] private GridAnimator animator;

        private GameSession _session;

        public void StartSession(LevelData level)
        {
            DetachSession();
            _session = new GameSession(level);
            _session.SessionReset += OnSessionReset;
            view.SetLevel(level);
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
            if (_session == null || (animator != null && animator.IsAnimating))
                return;

            var result = _session.TryMove(from, direction);

            if (animator == null)
            {
                view.Repaint(_session.Grid);
                return;
            }

            // Модель уже у фінальному стані; аніматор програє перехід,
            // а по завершенні в'ю досинхронізовується повним Repaint.
            input.InputLocked = true;
            animator.Play(result, _session.Grid, () =>
            {
                input.InputLocked = false;
                view.Repaint(_session.Grid);
            });
        }

        private void OnRetryRequested()
        {
            if (_session == null)
                return;

            // Retry посеред ланцюга: обриваємо анімацію, Reset сам викличе Repaint.
            if (animator != null)
                animator.Interrupt();
            input.InputLocked = false;
            _session.Reset();
        }

        private void OnSessionReset() => view.Repaint(_session.Grid);

        private void DetachSession()
        {
            if (_session != null)
                _session.SessionReset -= OnSessionReset;
            _session = null;
        }
    }
}
