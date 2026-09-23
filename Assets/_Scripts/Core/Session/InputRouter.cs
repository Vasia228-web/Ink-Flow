using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Єдина точка, через яку наміри гравця доходять до сесії.
    ///
    /// Під час програвання подій інпут заблокований — інакше гравець устигав би
    /// поставити другу фігуру поверх незавершеного зриву, і в'ю розійшлося б із моделлю.
    ///
    /// Живе в Core, а не в UI, бо блокування — правило, а не оформлення: без нього
    /// стрічка подій перестає бути стрічкою.
    /// </summary>
    public sealed class InputRouter
    {
        /// <summary>true — жести ігноруються (йде анімація ходу).</summary>
        public bool Locked { get; set; }

        /// <summary>Гравець відпустив фігуру над полем. Валідність вирішує Core.</summary>
        public event Action<int, GridPos>? PlaceRequested;

        public void RequestPlace(int trayIndex, GridPos anchor)
        {
            if (Locked)
                return;
            PlaceRequested?.Invoke(trayIndex, anchor);
        }
    }
}
