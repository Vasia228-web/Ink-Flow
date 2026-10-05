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

        /// <summary>
        /// true, поки <see cref="OnEnter"/> вмикає об'єкт. Екран читає це в <c>OnEnable</c>:
        /// у Play Mode <c>SetActive(true)</c> одразу кличе <c>OnEnable</c>, той ставив би
        /// <c>Apply()</c> зі СТАРИМ станом (попередня планета, попередній фільтр), а за мить
        /// <c>OnEnter</c> виставляв справжній стан і кликав <c>Apply()</c> ще раз — двічі збирався
        /// атлас і перев'язувалась стадія на кожному вході. Тепер вхід робить рівно один прохід.
        /// </summary>
        protected bool Entering { get; private set; }

        /// <summary>Підставляє композиційний корінь через <see cref="AppRouter"/>.</summary>
        public virtual void BindState(PlayerState state) => State = state;

        public virtual void OnEnter(ScreenArgs args)
        {
            Entering = true;
            try { gameObject.SetActive(true); }
            finally { Entering = false; }
        }

        public virtual void OnExit() => gameObject.SetActive(false);

        /// <summary>
        /// Перечитати стиль з <c>OnEnable</c>: у майстерні, при перекомпіляції й у Edit Mode — так,
        /// а під час входу через <see cref="OnEnter"/> — ні, бо він сам покличе <c>Apply()</c>
        /// з правильним станом (див. <see cref="Entering"/>).
        /// </summary>
        protected void ScheduleApply(System.Action apply)
        {
            if (!Entering)
                StyleRefresh.Schedule(this, apply);
        }

        /// <summary>
        /// Поки true, екран не запускає корутин входу (наближення, спалахи): стенд знімків і UI-тести
        /// в Edit Mode не тікають корутини, і кадр застиг би на першому їхньому кроці.
        /// </summary>
        public static bool SkipEnterAnimations { get; set; }

        /// <summary>Вхід без анімацій — для стендів у Edit Mode.</summary>
        public void PreviewEnter(ScreenArgs args)
        {
            var was = SkipEnterAnimations;
            SkipEnterAnimations = true;
            try { OnEnter(args); }
            finally { SkipEnterAnimations = was; }
        }
    }
}
