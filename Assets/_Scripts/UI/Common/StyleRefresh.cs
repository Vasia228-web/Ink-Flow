using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Відкладає перечитування стилю на кінець кадру редактора.
    ///
    /// Навіщо: наші <c>Apply()</c> чіпають десятки Graphic-ів одразу, а редактор
    /// може покликати їх просто посеред перебудови канвасу. Тоді кожен дотик до
    /// кольору чи розміру пробує зареєструвати графіку на перебудову зсередини
    /// самої перебудови, і Unity кидає «Trying to add … for graphic rebuild while
    /// we are already inside a graphic rebuild loop».
    ///
    /// Через це проходять ОБИДВА входи, і другий менш очевидний:
    /// <list type="bullet">
    /// <item><c>OnValidate</c> — коли editor-скрипт застосовує SerializedObject;</item>
    /// <item><c>OnEnable</c> — він спрацьовує і в Edit Mode, на кожному
    /// <c>AddComponent</c> та <c>InstantiatePrefab</c>, тобто рівно там, де бутстрап
    /// будує ієрархію.</item>
    /// </list>
    ///
    /// <c>delayCall</c> виконує ту саму роботу наступним кадром, коли цикл уже
    /// закритий. Для <c>OnEnable</c> у Play Mode відкладати не треба — там колбек
    /// приходить у нормальному порядку, а екран мусить бути правильним з першого
    /// кадру. Для <c>OnValidate</c> — треба ЗАВЖДИ, див.
    /// <see cref="ScheduleFromValidate"/>.
    /// </summary>
    public static class StyleRefresh
    {
#if UNITY_EDITOR
        /// <summary>
        /// Поки бутстрап збирає сцену або префаби, відкладати нічого не треба: він
        /// сам кличе <c>Apply()</c> синхронно й у правильному порядку, перед
        /// збереженням. Інакше кожен <c>Wire()</c> ставив би в чергу ще одне
        /// перечитування, і сцена ставала б брудною одразу після збереження.
        /// </summary>
        public static bool Suspended { get; set; }
#endif

        /// <summary>
        /// Вхід САМЕ з <c>OnValidate</c>. Відкладає завжди — і в Edit Mode, і в Play.
        ///
        /// Причина: під час входу в Play Mode <c>Application.isPlaying</c> уже true,
        /// але Unity ще десеріалізує сцену, і <c>SendMessage</c>-колбеки заборонені.
        /// Синхронний <c>Apply()</c> звідти пише <c>sizeDelta</c> дочірнім об'єктам,
        /// а це розсилає <c>OnRectTransformDimensionsChange</c> — і на кожен об'єкт
        /// прилітає «SendMessage cannot be called during Awake, CheckConsistency,
        /// or OnValidate». На карті рівнів це давало по попередженню на кожен
        /// вузол і відрізок сліду.
        ///
        /// Нічого не втрачається: правку в інспекторі видно наступним тіком
        /// редактора, а стан екрана однаково перечитується в <c>OnEnable</c>.
        /// </summary>
        public static void ScheduleFromValidate(Object owner, System.Action apply)
        {
#if UNITY_EDITOR
            if (Suspended)
                return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (owner != null)
                    apply();
            };
#else
            apply();
#endif
        }

        public static void Schedule(Object owner, System.Action apply)
        {
#if UNITY_EDITOR
            if (Suspended)
                return;

            if (Application.isPlaying)
            {
                apply();
                return;
            }

            UnityEditor.EditorApplication.delayCall += () =>
            {
                // Об'єкт міг зникнути між викликом і наступним кадром — звичайна річ,
                // коли бутстрап перезбирає сцену з нуля.
                if (owner != null)
                    apply();
            };
#else
            apply();
#endif
        }
    }
}
