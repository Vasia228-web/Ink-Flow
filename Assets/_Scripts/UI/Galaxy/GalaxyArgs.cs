using InkFlow.Core;
using InkFlow.Meta;

namespace InkFlow.UI
{
    /// <summary>
    /// Аргументи екрана Галактики: своя (редагована) або чужа з публічної вітрини (§16) — лише перегляд.
    /// Один екран на обидві ролі: копія розійшлася б із оригіналом на першій правці розкладки.
    /// </summary>
    public sealed class GalaxyArgs : ScreenArgs
    {
        public GalaxyArgs() { }

        public GalaxyArgs(PlayerId owner, bool readOnly)
        {
            Owner = owner;
            ReadOnly = readOnly;
        }

        private GalaxyArgs(PublicShowcase visitor)
        {
            Owner = new PlayerId(visitor.PlayerId);
            ReadOnly = true;
            Visitor = visitor;
        }

        // get-only, задається конструктором. `init` тут не можна: він вимагає
        // System.Runtime.CompilerServices.IsExternalInit, якого в .NET Standard 2.1
        // Unity немає, і збірка падає з CS0518.

        /// <summary>Чию галактику показуємо. За замовчуванням — свою.</summary>
        public PlayerId Owner { get; }

        /// <summary>
        /// Тільки перегляд: ховаються кнопка «Відкрити» й пагінація.
        /// Гортати чужу галактику можна, змінювати — ні.
        /// </summary>
        public bool ReadOnly { get; }

        /// <summary>Вітрина чужого гравця з хмари: планети з картинками, нік, аватар, вітринна картинка.</summary>
        public PublicShowcase? Visitor { get; }

        public static readonly GalaxyArgs Own = new GalaxyArgs(PlayerId.Self, false);

        /// <summary>Чужа галактика з Рейтингів (§16): лише перегляд, дані — з вітрини.</summary>
        public static GalaxyArgs ForVisitor(PublicShowcase visitor) => new GalaxyArgs(visitor);
    }
}
