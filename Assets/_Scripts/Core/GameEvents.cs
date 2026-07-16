using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Мінімальний статичний «місток» між асемблями: UI посилається тільки на Core,
    /// тому Gameplay публікує сесію сюди, а UI звідси її читає. Це свідома заміна
    /// DI-фреймворку для проєкту такого розміру (див. CLAUDE.md).
    /// GameManager викликає Clear() в OnDestroy, щоб не лишати підписок між запусками.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Нова сесія створена і готова (після завантаження рівня або Retry).</summary>
        public static event Action<GameSession> SessionStarted;

        /// <summary>UI просить перезапустити рівень (кнопка Retry).</summary>
        public static event Action RetryRequested;

        public static GameSession CurrentSession { get; private set; }

        public static void RaiseSessionStarted(GameSession session)
        {
            CurrentSession = session;
            SessionStarted?.Invoke(session);
        }

        public static void RequestRetry() => RetryRequested?.Invoke();

        public static void Clear()
        {
            SessionStarted = null;
            RetryRequested = null;
            CurrentSession = null;
        }
    }
}
