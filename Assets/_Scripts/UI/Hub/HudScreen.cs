using InkFlow.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// HUD партії: ходи, ціль, рахунок, миттєвий ↺ і чесні банери фіналу.
    /// UI бачить лише Core: стан приходить подіями, наміри йдуть командами (§2 правило 2).
    /// </summary>
    public sealed class HudScreen : ScreenBase
    {
        [SerializeField] private TMP_Text movesLabel;
        [SerializeField] private TMP_Text goalLabel;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private Button retryButton;

        [Header("Фінал")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;
        [SerializeField] private TMP_Text outcomeLabel;
        [SerializeField] private TMP_Text starsLabel;

        private GameSession? _session;

        private void OnEnable()
        {
            GameEvents.SessionStarted += OnSessionStarted;
            GameEvents.MovePlayed += OnMovePlayed;
            GameEvents.SessionEnded += OnSessionEnded;
            retryButton.onClick.AddListener(OnRetryClicked);

            if (GameEvents.CurrentSession != null)
                OnSessionStarted(GameEvents.CurrentSession);
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= OnSessionStarted;
            GameEvents.MovePlayed -= OnMovePlayed;
            GameEvents.SessionEnded -= OnSessionEnded;
            retryButton.onClick.RemoveListener(OnRetryClicked);
        }

        private void OnSessionStarted(GameSession session)
        {
            _session = session;
            HidePanels();
            Refresh();
        }

        private void OnMovePlayed(MoveResult result) => Refresh();

        private void OnSessionEnded(GameState state)
        {
            // Жодних модалок із затримкою: ↺ лишається активним і натискається одразу.
            switch (state)
            {
                case GameState.Won:
                    Show(winPanel, "Перемога!");
                    if (starsLabel != null && _session is PuzzleSession puzzle)
                        starsLabel.text = new string('★', puzzle.Stars).PadRight(3, '☆');
                    break;
                case GameState.Lost:
                    Show(losePanel, "Ходи скінчились");
                    break;
                case GameState.Deadlock:
                    // Тупик називаємо своїм ім'ям — гравець має розуміти, що поле мертве.
                    Show(losePanel, "Тупік — ходів більше немає");
                    break;
            }
        }

        private void Show(GameObject panel, string text)
        {
            HidePanels();
            if (panel != null)
                panel.SetActive(true);
            if (outcomeLabel != null)
                outcomeLabel.text = text;
        }

        private void HidePanels()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
            if (starsLabel != null) starsLabel.text = string.Empty;
        }

        private void Refresh()
        {
            if (_session == null)
                return;

            if (movesLabel != null)
                movesLabel.text = _session is EndlessSession
                    ? $"Ходи: {_session.AcceptedMoves}"
                    : $"Ходи: {_session.MovesLeft}";

            if (scoreLabel != null)
                scoreLabel.text = $"Очки: {_session.Score}";

            if (goalLabel != null)
                goalLabel.text = GoalText();
        }

        private string GoalText() => _session switch
        {
            BossSession boss => $"Клякс: {boss.Boss.CountPainted()}/{boss.Boss.SegmentCount}",
            EndlessSession => "Грай, поки є хід",
            PuzzleSession => "Ціль: один колір або одна крапля",
            _ => string.Empty
        };

        private void OnRetryClicked() => GameEvents.Commands?.Restart();
    }
}
