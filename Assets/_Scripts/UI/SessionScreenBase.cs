using InkFlow.Core;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Спільна логіка Win/Lose екранів: показати панель, коли сесія перейшла
    /// в цільовий стан, сховати після Reset. Панель — легкий банер, НЕ модалка
    /// з затримкою: кнопка Retry в HUD лишається активною одразу.
    /// </summary>
    public abstract class SessionScreenBase : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        private GameSession _session;

        protected abstract SessionState TargetState { get; }

        private void OnEnable()
        {
            GameEvents.SessionStarted += Bind;
            if (GameEvents.CurrentSession != null)
                Bind(GameEvents.CurrentSession);
            else
                panel.SetActive(false);
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= Bind;
            Detach();
        }

        private void Bind(GameSession session)
        {
            Detach();
            _session = session;
            _session.StateChanged += OnStateChanged;
            _session.SessionReset += OnSessionReset;
            panel.SetActive(_session.State == TargetState);
        }

        private void Detach()
        {
            if (_session == null)
                return;
            _session.StateChanged -= OnStateChanged;
            _session.SessionReset -= OnSessionReset;
            _session = null;
        }

        private void OnStateChanged(SessionState state) =>
            panel.SetActive(state == TargetState);

        private void OnSessionReset() => panel.SetActive(false);
    }
}
