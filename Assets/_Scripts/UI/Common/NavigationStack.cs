using System.Collections.Generic;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Стек навігації між екранами (push/pop, §9).
    ///
    /// Живе в окремому файлі, бо кожен MonoBehaviour мусить лежати у файлі зі своєю назвою —
    /// інакше Unity не створює MonoScript і компонент не підв'язується до сцени/префаба.
    ///
    /// Стек тримає не лише екран, а й АРГУМЕНТИ, з якими його відкрили. Без цього
    /// повернення назад втрачало б контекст: галактика, відкрита з Рейтингів у режимі
    /// перегляду, після pop із фарбування раптом ставала б власною й редагованою.
    /// </summary>
    public sealed class NavigationStack : MonoBehaviour
    {
        private readonly struct Entry
        {
            public Entry(ScreenBase screen, ScreenArgs args)
            {
                Screen = screen;
                Args = args;
            }

            public ScreenBase Screen { get; }
            public ScreenArgs Args { get; }
        }

        private readonly List<Entry> _stack = new List<Entry>(8);

        public ScreenBase? Current => _stack.Count > 0 ? _stack[_stack.Count - 1].Screen : null;

        /// <summary>Глибина стека. Нею ж перевіряють, що рестарт партії його не роздуває.</summary>
        public int Depth => _stack.Count;

        public void Push(ScreenBase screen, ScreenArgs? args = null)
        {
            if (screen == null)
            {
                Debug.LogError("[InkFlow] Push порожнього екрана — десь не підв'язане посилання.");
                return;
            }

            // Один екземпляр екрана не може лежати у стеку двічі: Pop зняв би
            // верхній і «показав» той самий об'єкт, який нікуди не подівся.
            // Мовчки це виглядало б як застрягання, тому кричимо.
            if (IndexOf(screen) >= 0)
            {
                Debug.LogError($"[InkFlow] {screen.name} уже в стеку — повторний Push заборонений.");
                return;
            }

            Current?.OnExit();
            var entry = new Entry(screen, args ?? ScreenArgs.Empty);
            _stack.Add(entry);
            screen.OnEnter(entry.Args);
        }

        public void Pop()
        {
            if (_stack.Count == 0)
                return;

            // Останній екран не знімаємо: корінь мусить лишатись, інакше «‹» з хаба
            // лишив би порожній екран.
            if (_stack.Count == 1)
                return;

            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.Screen.OnExit();

            var below = _stack[_stack.Count - 1];
            below.Screen.OnEnter(below.Args);
        }

        /// <summary>
        /// Замінює ВЕРХНІЙ екран, не чіпаючи те, що під ним.
        ///
        /// Саме цим переходять «Далі» після пройденого рівня: пуш на кожному рівні
        /// нарощував би стек, і після десяти рівнів «‹» вело б через усі десять.
        /// </summary>
        public void Replace(ScreenBase screen, ScreenArgs? args = null)
        {
            if (screen == null)
                return;

            if (_stack.Count == 0)
            {
                Push(screen, args);
                return;
            }

            var top = _stack[_stack.Count - 1];
            var entry = new Entry(screen, args ?? ScreenArgs.Empty);

            // Той самий екран із новими аргументами — не смикаємо OnExit/OnEnter
            // парою, просто заходимо заново: інакше блимав би кадр порожнечі.
            if (ReferenceEquals(top.Screen, screen))
            {
                _stack[_stack.Count - 1] = entry;
                screen.OnEnter(entry.Args);
                return;
            }

            _stack.RemoveAt(_stack.Count - 1);
            top.Screen.OnExit();
            _stack.Add(entry);
            screen.OnEnter(entry.Args);
        }

        /// <summary>Замінює весь стек одним екраном (корінь застосунку).</summary>
        public void SetRoot(ScreenBase screen, ScreenArgs? args = null)
        {
            foreach (var entry in _stack)
                entry.Screen.OnExit();
            _stack.Clear();
            Push(screen, args);
        }

        private int IndexOf(ScreenBase screen)
        {
            for (var i = 0; i < _stack.Count; i++)
                if (ReferenceEquals(_stack[i].Screen, screen))
                    return i;
            return -1;
        }
    }
}
