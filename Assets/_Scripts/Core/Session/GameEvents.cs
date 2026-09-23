using System;

namespace InkFlow.Core
{
    /// <summary>Команди, які UI шле в геймплей. UI не викликає правила напряму.</summary>
    public interface IGameCommands
    {
        /// <summary>Миттєвий рестарт (&lt; 300 мс, без завантаження сцени).</summary>
        void Restart();

        /// <summary>Підказка від застою: підсвітити одну валідну позицію.</summary>
        void RequestHint();
    }

    /// <summary>
    /// Місток між шарами: сесія публікує стани, UI підписується. Свідома заміна
    /// DI-контейнера — один екран партії не вартий контейнера.
    /// Композиційний корінь викликає Clear() при вивантаженні сцени.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Нова партія готова.</summary>
        public static event Action<RunSession>? SessionStarted;

        /// <summary>Хід застосовано і програно в'ю (після анімацій).</summary>
        public static event Action<MoveResult>? MovePlayed;

        /// <summary>Партія завершилась.</summary>
        public static event Action<GameState>? SessionEnded;

        /// <summary>Підказка: поставити фігуру trayIndex у цю позицію.</summary>
        public static event Action<int, GridPos>? HintShown;

        public static RunSession? CurrentSession { get; private set; }
        public static IGameCommands? Commands { get; set; }

        public static void RaiseSessionStarted(RunSession session)
        {
            CurrentSession = session;
            SessionStarted?.Invoke(session);
        }

        public static void RaiseMovePlayed(MoveResult result) => MovePlayed?.Invoke(result);

        public static void RaiseSessionEnded(GameState state) => SessionEnded?.Invoke(state);

        public static void RaiseHint(int trayIndex, GridPos anchor) => HintShown?.Invoke(trayIndex, anchor);

        public static void Clear()
        {
            SessionStarted = null;
            MovePlayed = null;
            SessionEnded = null;
            HintShown = null;
            CurrentSession = null;
            Commands = null;
        }
    }
}
