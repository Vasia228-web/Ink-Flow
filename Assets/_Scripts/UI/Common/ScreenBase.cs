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
        public virtual void OnEnter(ScreenArgs args) => gameObject.SetActive(true);

        public virtual void OnExit() => gameObject.SetActive(false);
    }
}
