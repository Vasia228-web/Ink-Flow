using System.Collections.Generic;
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

    /// <summary>Стек навігації між екранами-префабами (push/pop).</summary>
    public sealed class NavigationStack : MonoBehaviour
    {
        private readonly List<ScreenBase> _stack = new List<ScreenBase>(8);

        public ScreenBase? Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        public int Depth => _stack.Count;

        public void Push(ScreenBase screen, ScreenArgs? args = null)
        {
            Current?.OnExit();
            _stack.Add(screen);
            screen.OnEnter(args ?? ScreenArgs.Empty);
        }

        public void Pop()
        {
            if (_stack.Count == 0)
                return;

            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.OnExit();
            Current?.OnEnter(ScreenArgs.Empty);
        }

        /// <summary>Замінює весь стек одним екраном (перемикання вкладок).</summary>
        public void SetRoot(ScreenBase screen, ScreenArgs? args = null)
        {
            foreach (var entry in _stack)
                entry.OnExit();
            _stack.Clear();
            Push(screen, args);
        }
    }
}
