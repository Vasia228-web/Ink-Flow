using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Один хід у реплеї.</summary>
    public readonly struct ReplayMove
    {
        public GridPos From { get; }
        public GridPos To { get; }

        public ReplayMove(GridPos from, GridPos to)
        {
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// Сід + список ходів = повне відтворення партії (§6). Дає безкоштовний баг-репорт
    /// («надішли сід») і майбутню античит-перевірку рекордів у Нескінченному.
    /// </summary>
    public sealed class RunReplay
    {
        private readonly List<ReplayMove> _moves = new List<ReplayMove>(128);

        public RunReplay(uint seed)
        {
            Seed = seed;
        }

        public uint Seed { get; }

        public IReadOnlyList<ReplayMove> Moves => _moves;

        public void Record(GridPos from, GridPos to) => _moves.Add(new ReplayMove(from, to));

        public void Clear() => _moves.Clear();

        /// <summary>Програє записані ходи в іншій сесії — має дати той самий результат.</summary>
        public void Replay(GameSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            foreach (var move in _moves)
                session.ApplyMove(move.From, move.To);
        }
    }
}
