using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Ледь помітне потовщення світла у фоні: прибирає пласкість градієнта — і все.
    ///
    /// Гравець не має помічати туманність свідомо. Тому вона більша за екран
    /// (форма не читається), зміщена вниз-убік від лого, а колір будується від
    /// фонового — вона не може стати світлішою за фон, як би не крутили токени.
    ///
    /// Це два прозорі квади зі спрайтом м'якого купола, а не post-process Bloom:
    /// повноекранний bloom на iPhone SE-класі коштує кадру.
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

            // Розмір і позиція живуть тут, а не в бутстрапі: інакше nebulaScale
            // і nebulaOffset не крутились би в інспекторі без перезбирання сцени.
            var size = DesignSystem.ReferenceWidth * design.NebulaScale;
            _rect.sizeDelta = new Vector2(size, size);
            _rect.anchoredPosition = design.NebulaOffset * DesignSystem.ReferenceWidth;

            image.color = DesignSystem.WithAlpha(design.NebulaTint, design.NebulaAlpha);
            image.raycastTarget = false;

            if (secondary != null)
            {
                // Другий шар менший і зсунутий у протилежний бік від першого:
                // разом вони дають несиметричне світло, у якому не вгадується коло.
                var secondRect = secondary.rectTransform;
                secondRect.sizeDelta = new Vector2(size * 0.68f, size * 0.68f);
                secondRect.anchoredPosition = new Vector2(size * 0.24f, size * 0.18f);

                secondary.color = DesignSystem.WithAlpha(
                    Color.Lerp(design.NebulaTint, design.AccentBlue, 0.3f),
                    design.NebulaAlpha * 0.6f);
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
