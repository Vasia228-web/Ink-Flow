using InkFlow.Style;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Чорнило, що стікає з логотипу: під «Ink» зривається маджентова краплина,
    /// під «Flow» — бірюзова. Одна за раз, з випадковими паузами.
    ///
    /// Свідомо ОДНА краплина за раз: дві одночасні читаються як ефект, а одна
    /// рідкісна — як жива деталь. Пауза з розкидом прибирає ефект метронома.
    /// </summary>
    public sealed class LogoDrip : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private DripPool? pool;

        [Tooltip("Точки зриву в координатах пулу: під «Ink» і під «Flow».")]
        [SerializeField] private RectTransform[] sources = System.Array.Empty<RectTransform>();

        [Tooltip("Розмір краплини логотипу в reference-одиницях.")]
        [SerializeField] private float dropSize = 46f;

        [Tooltip("Множник паузи відносно DripInterval: логотип капає рідше за краплі.")]
        [SerializeField, Range(0.5f, 4f)] private float intervalScale = 1.8f;

        private float _next;
        private int _lastSource = -1;

        private void OnEnable() => _next = Delay();

        private float Delay()
        {
            if (design == null)
                return float.MaxValue;
            var baseDelay = design.DripInterval * intervalScale;
            var jitter = baseDelay * design.DripJitter;
            return baseDelay + Random.Range(-jitter, jitter);
        }

        private void LateUpdate()
        {
            if (design == null || pool == null || sources.Length == 0)
                return;

            _next -= Time.deltaTime;
            if (_next > 0f)
                return;

            _next = Delay();

            // Не з тієї самої літери двічі поспіль — інакше друга літера
            // виглядає «мертвою».
            var index = sources.Length == 1
                ? 0
                : (_lastSource + 1 + Random.Range(0, sources.Length - 1)) % sources.Length;
            _lastSource = index;

            var source = sources[index];
            if (source == null)
                return;

            var local = (Vector2)pool.transform.InverseTransformPoint(source.position);
            var color = index == 0 ? design.AccentPrimary : design.AccentTeal;
            pool.Emit(local, color, dropSize / design.DripSize);
        }
    }
}
