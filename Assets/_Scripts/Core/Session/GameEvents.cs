using System;

namespace InkFlow.Core
{
    /// <summary>Команди, які UI шле в геймплей. UI не викликає Gameplay напряму (§2 правило 2).</summary>
    public interface IGameCommands
    {
        /// <summary>Миттєвий рестарт рівня (< 300 мс, без завантаження сцени).</summary>
        void Restart();

        /// <summary>Підказка від застою: підсвітити одну доступну пару.</summary>
        void RequestHint();
    }

    /// <summary>
    /// Місток між шарами: Gameplay публікує стани, UI підписується (§2 правило 2).
    /// Свідома заміна DI-контейнера — один екран геймплею не вартий контейнера (§1).
    /// GameBootstrap викликає Clear() при вивантаженні сцени.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Нова партія готова.</summary>
        public static event Action<GameSession>? SessionStarted;

        /// <summary>Хід застосовано і програно (після анімацій).</summary>
        public static event Action<MoveResult>? MovePlayed;

        /// <summary>Партія завершилась: Won / Lost / Deadlock.</summary>
        public static event Action<GameState>? SessionEnded;

        /// <summary>Підказка: підсвітити пару.</summary>
        public static event Action<GridPos, GridPos>? HintShown;

        public static GameSession? CurrentSession { get; private set; }
        public static IGameCommands? Commands { get; set; }

        public static void RaiseSessionStarted(GameSession session)
        {
            CurrentSession = session;
            SessionStarted?.Invoke(session);
        }

        public static void RaiseMovePlayed(MoveResult result) => MovePlayed?.Invoke(result);

        public static void RaiseSessionEnded(GameState state) => SessionEnded?.Invoke(state);

        public static void RaiseHint(GridPos from, GridPos to) => HintShown?.Invoke(from, to);

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
