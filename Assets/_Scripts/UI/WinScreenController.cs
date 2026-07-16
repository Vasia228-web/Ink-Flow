using InkFlow.Core;

namespace InkFlow.UI
{
    /// <summary>Банер перемоги рівня.</summary>
    public sealed class WinScreenController : SessionScreenBase
    {
        protected override SessionState TargetState => SessionState.Won;
    }
}
