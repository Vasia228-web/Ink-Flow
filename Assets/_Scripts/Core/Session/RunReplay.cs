using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Один запис реплею: розміщення фігури з лотка в якір.</summary>
    public readonly struct ReplayStep
    {
        public ReplayStep(int trayIndex, GridPos anchor)
        {
            TrayIndex = trayIndex;
            Anchor = anchor;
        }

        public int TrayIndex { get; }
        public GridPos Anchor { get; }

        public override string ToString() => $"place #{TrayIndex}@{Anchor}";
    }

    /// <summary>
    /// Сід + послідовність дій = повне відтворення партії. Дає безкоштовний баг-репорт
    /// («надішли сід») і майбутню перевірку рекордів Нескінченного.
    /// </summary>
    public sealed class RunReplay
    {
        private readonly List<ReplayStep> _steps = new List<ReplayStep>(128);

        public RunReplay(uint seed)
        {
            Seed = seed;
        }

        public uint Seed { get; }

        public IReadOnlyList<ReplayStep> Steps => _steps;

        public void Record(int trayIndex, GridPos anchor) => _steps.Add(new ReplayStep(trayIndex, anchor));

        public void Clear() => _steps.Clear();

        /// <summary>Програє записані дії в іншій сесії — має дати байт-в-байт той самий результат.</summary>
        public void Replay(RunSession session)
        {
            if (session is null) throw new ArgumentNullException(nameof(session));
            foreach (var step in _steps)
                session.TryPlace(step.TrayIndex, step.Anchor);
        }
    }
}
