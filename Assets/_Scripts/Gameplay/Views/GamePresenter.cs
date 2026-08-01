using System.Collections;
using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Зшиває шари партії: інпут → сесія (Core) → програвання подій у в'ю.
    /// Сам нічого не вирішує про правила — усе рішення приймає Core.
    /// </summary>
    public sealed class GamePresenter : MonoBehaviour, IGameCommands
    {
        [SerializeField] private GridView gridView;
        [SerializeField] private SwipeInput swipeInput;
        [SerializeField] private BossView bossView;

        [Tooltip("Через скільки секунд без ходу підсвітити доступну пару (підказка від застою).")]
        [SerializeField, Min(0f)] private float hintDelaySeconds = 5f;

        private readonly InputRouter _router = new InputRouter();
        private GameSession _session;
        private Coroutine _playback;
        private float _idleTimer;
        private bool _hintShown;

        public GameSession Session => _session;

        private void Awake()
        {
            swipeInput.Bind(_router);
            GameEvents.Commands = this;
        }

        private void OnEnable() => _router.MoveRequested += OnMoveRequested;

        private void OnDisable() => _router.MoveRequested -= OnMoveRequested;

        /// <summary>Запускає партію. Викликає композиційний корінь (GameBootstrap).</summary>
        public void StartSession(GameSession session)
        {
            _session = session;
            _router.Locked = false;
            _idleTimer = 0f;
            _hintShown = false;

            gridView.Bind(session);
            if (bossView != null)
                bossView.Bind(session as BossSession);

            GameEvents.RaiseSessionStarted(session);
        }

        private void OnMoveRequested(GridPos from, GridPos to)
        {
            if (_session == null || _session.IsOver || _router.Locked)
                return;

            var result = _session.ApplyMove(from, to);
            _idleTimer = 0f;
            _hintShown = false;

            _router.Locked = true;
            _playback = StartCoroutine(PlayAndUnlock(result));
        }

        private IEnumerator PlayAndUnlock(MoveResult result)
        {
            yield return gridView.PlayEvents(result);

            if (bossView != null)
                bossView.Refresh();

            GameEvents.RaiseMovePlayed(result);
            _router.Locked = false;
            _playback = null;

            if (_session.IsOver)
                GameEvents.RaiseSessionEnded(_session.State);
        }

        private void Update()
        {
            // Підказка від застою: рятує новачків від відчуття тупика там, де його немає.
            if (_session == null || _session.IsOver || _router.Locked || _hintShown || hintDelaySeconds <= 0f)
                return;

            _idleTimer += Time.deltaTime;
            if (_idleTimer < hintDelaySeconds)
                return;

            if (DeadlockDetector.TryFindMove(_session.Grid, out var from, out var to))
            {
                gridView.SetSelected(from, true);
                gridView.SetSelected(to, true);
                GameEvents.RaiseHint(from, to);
            }

            _hintShown = true;
        }

        // ---------- IGameCommands (наміри від UI) ----------

        /// <summary>Миттєвий рестарт: реініціалізація моделі без завантаження сцени.</summary>
        public void Restart()
        {
            if (_session == null)
                return;

            if (_playback != null)
            {
                StopCoroutine(_playback);
                _playback = null;
            }

            _session.Reset();
            _router.Locked = false;
            _idleTimer = 0f;
            _hintShown = false;
            gridView.Repaint();
            if (bossView != null)
                bossView.Refresh();
            GameEvents.RaiseSessionStarted(_session);
        }

        public void RequestHint()
        {
            if (_session == null || !DeadlockDetector.TryFindMove(_session.Grid, out var from, out var to))
                return;
            gridView.SetSelected(from, true);
            gridView.SetSelected(to, true);
            GameEvents.RaiseHint(from, to);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(GameEvents.Commands, this))
                GameEvents.Commands = null;
        }
    }
}
