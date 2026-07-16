using InkFlow.Core;

namespace InkFlow.UI
{
    /// <summary>Банер поразки. Без модалки з затримкою — Retry доступний одразу.</summary>
    public sealed class LoseScreenController : SessionScreenBase
    {
        protected override SessionState TargetState => SessionState.Lost;
    }
}
