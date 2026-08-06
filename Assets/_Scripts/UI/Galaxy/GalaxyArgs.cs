using InkFlow.Meta;

namespace InkFlow.UI
{
    /// <summary>
    /// Аргументи екрана галактики.
    ///
    /// Другого екрана для «чужої галактики» не буде: перегляд із Рейтингів — це той
    /// самий екран із <see cref="ReadOnly"/>. Копія розійшлася б із оригіналом на
    /// першій же правці розкладки.
    /// </summary>
    public sealed class GalaxyArgs : ScreenArgs
    {
        public GalaxyArgs() { }

        public GalaxyArgs(PlayerId owner, bool readOnly)
        {
            Owner = owner;
            ReadOnly = readOnly;
        }

        // get-only, задається конструктором. `init` тут не можна: він вимагає
        // System.Runtime.CompilerServices.IsExternalInit, якого в .NET Standard 2.1
        // Unity немає, і збірка падає з CS0518.

        /// <summary>Чию галактику показуємо. За замовчуванням — свою.</summary>
        public PlayerId Owner { get; }

        /// <summary>
        /// Тільки перегляд: ховаються кнопка «Фарбувати» й пагінація.
        /// Гортати чужу галактику можна, змінювати — ні.
        /// </summary>
        public bool ReadOnly { get; }

        public static readonly GalaxyArgs Own = new GalaxyArgs(PlayerId.Self, false);
    }
}
