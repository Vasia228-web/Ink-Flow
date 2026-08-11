using InkFlow.Meta;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>Аргументи переходу на екран.</summary>
    public class ScreenArgs
    {
        public static readonly ScreenArgs Empty = new ScreenArgs();
    }

    /// <summary>
    /// Базовий екран (§9). Екрани всередині Meta — це ПРЕФАБИ, а не сцени:
    /// завантаження сцени на слабкому Android коштує 200-400 мс і чорний кадр,
    /// а перехід між вкладками має бути миттєвим.
    /// </summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        /// <summary>
        /// Реальний стан гравця. Null — екран відкрито окремою сценою-майстернею;
        /// у цьому режимі він показує мокові дані й НІЧОГО не зберігає.
        ///
        /// Екземпляр один на застосунок: якби кожен екран зробив собі копію,
        /// після покупки в магазині сусідній екран показував би старі числа.
        /// </summary>
        protected PlayerState? State { get; private set; }

        /// <summary>Підставляє композиційний корінь через <see cref="AppRouter"/>.</summary>
        public virtual void BindState(PlayerState state) => State = state;

        public virtual void OnEnter(ScreenArgs args) => gameObject.SetActive(true);

        public virtual void OnExit() => gameObject.SetActive(false);
    }
}
