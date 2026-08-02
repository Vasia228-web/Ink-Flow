using System.Collections.Generic;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Стек навігації між екранами-префабами (push/pop, §9).
    ///
    /// Живе в окремому файлі, бо кожен MonoBehaviour мусить лежати у файлі зі своєю назвою —
    /// інакше Unity не створює MonoScript і компонент не підв'язується до сцени/префаба.
    /// </summary>
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
