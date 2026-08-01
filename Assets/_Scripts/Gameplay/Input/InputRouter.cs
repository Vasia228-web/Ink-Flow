using System;
using InkFlow.Core;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Єдина точка, через яку наміри гравця доходять до сесії.
    /// Під час програвання подій інпут заблокований (§8) — інакше гравець устигав би
    /// зробити хід поверх незавершеного ланцюга і в'ю розійшлося б із моделлю.
    /// </summary>
    public sealed class InputRouter
    {
        /// <summary>true — жести ігноруються (йде анімація ходу).</summary>
        public bool Locked { get; set; }

        /// <summary>Гравець просить хід. Валідність вирішує Core.</summary>
        public event Action<GridPos, GridPos>? MoveRequested;

        public void RequestMove(GridPos from, GridPos to)
        {
            if (Locked)
                return;
            MoveRequested?.Invoke(from, to);
        }
    }
}
