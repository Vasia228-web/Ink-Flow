using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Велика розмита туманність у центрі фону: дає глибину, якої не дає сам градієнт.
    ///
    /// Це один Image зі спрайтом м'якого кола, а не post-process Bloom: повноекранний
    /// bloom на iPhone SE-класі коштує кадру, а тут — один прозорий квад.
    /// Дихає дуже повільно (17 с) і з дуже малою альфою — атмосфера, а не анімація.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class NebulaGlow : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image image;

        [Tooltip("Другий шар туманності — інший колір і зсунута фаза, щоб пляма не була круглою.")]
        [SerializeField] private Image? secondary;

        private float _phase;
        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _rect = (RectTransform)transform;
            StyleRefresh.Schedule(this, Apply);
        }
#endif

        public void Apply()
        {
            if (design == null || image == null)
                return;

            var size = DesignSystem.ReferenceWidth * design.NebulaScale;
            _rect.sizeDelta = new Vector2(size, size);

            image.color = DesignSystem.WithAlpha(design.AccentSecondary, design.NebulaAlpha);
            image.raycastTarget = false;

            if (secondary != null)
            {
                secondary.color = DesignSystem.WithAlpha(design.AccentBlue, design.NebulaAlpha * 0.7f);
                secondary.raycastTarget = false;
            }
        }

        private void Update()
        {
            if (design == null || design.NebulaBreathPeriod <= 0f)
                return;

            var t = Mathf.Sin((Time.time / design.NebulaBreathPeriod + _phase) * Mathf.PI * 2f);
            var scale = 1f + design.NebulaBreathAmount * t;
            _rect.localScale = new Vector3(scale, scale * 0.82f, 1f);

            if (secondary != null)
            {
                // Другий шар дихає в протифазі — разом вони «переливаються».
                var inverse = 1f - design.NebulaBreathAmount * t * 0.6f;
                secondary.rectTransform.localScale = new Vector3(inverse * 0.8f, inverse, 1f);
            }
        }
    }
}
