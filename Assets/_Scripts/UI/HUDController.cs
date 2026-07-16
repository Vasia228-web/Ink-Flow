using InkFlow.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// HUD: лічильник ходів, ціль рівня, кнопка Retry.
    /// UI-асембля бачить тільки Core: сесію отримує через GameEvents,
    /// Retry просить теж через GameEvents — без посилань на Gameplay.
    /// Retry миттєвий: реініціалізація GridModel, без перезавантаження сцени.
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] private TMP_Text movesLabel;
        [SerializeField] private TMP_Text goalLabel;
        [SerializeField] private Button retryButton;

        private GameSession _session;

        private void OnEnable()
        {
            GameEvents.SessionStarted += Bind;
            retryButton.onClick.AddListener(GameEvents.RequestRetry);
            if (GameEvents.CurrentSession != null)
                Bind(GameEvents.CurrentSession);
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= Bind;
            retryButton.onClick.RemoveListener(GameEvents.RequestRetry);
            Detach();
        }

        private void Bind(GameSession session)
        {
            Detach();
            _session = session;
            _session.MoveApplied += OnMoveApplied;
            _session.SessionReset += Refresh;
            Refresh();
        }

        private void Detach()
        {
            if (_session == null)
                return;
            _session.MoveApplied -= OnMoveApplied;
            _session.SessionReset -= Refresh;
            _session = null;
        }

        private void OnMoveApplied(MoveResult _) => Refresh();

        private void Refresh()
        {
            if (_session == null)
                return;

            movesLabel.text = $"Ходи: {_session.MovesLeft}";
            goalLabel.text = _session.Level.WinCondition switch
            {
                WinConditionType.ClearSingleColor => "Ціль: один колір",
                WinConditionType.ClearSingleCell => "Ціль: одна крапля",
                WinConditionType.ScoreAttack =>
                    $"Очки: {_session.Score}/{_session.Level.TargetScore}",
                _ => string.Empty
            };
        }
    }
}
