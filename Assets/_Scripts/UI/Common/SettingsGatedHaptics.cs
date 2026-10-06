using InkFlow.Meta;
using InkFlow.Platform;

namespace InkFlow.UI
{
    /// <summary>
    /// Гаптика за перемикачем «Вібрація» (§15): коли вимкнено — жодного дзижчання, увімкнено —
    /// усе йде в платформну реалізацію. Обгортка, а не прапорець у кожному виклику: місць, що
    /// б'ють гаптикою, уже кілька, і кожне з них забуло б перевірити налаштування по-своєму.
    /// </summary>
    public sealed class SettingsGatedHaptics : IHapticService
    {
        private readonly IHapticService _inner;
        private readonly PlayerState _state;

        public SettingsGatedHaptics(IHapticService inner, PlayerState state)
        {
            _inner = inner ?? new NullHaptics();
            _state = state ?? throw new System.ArgumentNullException(nameof(state));
        }

        private bool On => _state.Settings.Vibration;

        public void Light()
        {
            if (On) _inner.Light();
        }

        public void Medium()
        {
            if (On) _inner.Medium();
        }

        public void Heavy()
        {
            if (On) _inner.Heavy();
        }

        public void Chain(int depth)
        {
            if (On) _inner.Chain(depth);
        }
    }
}
