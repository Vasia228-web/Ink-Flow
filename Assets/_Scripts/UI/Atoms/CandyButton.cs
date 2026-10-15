using System.Collections;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Кнопка-цукерка U1Glaze: 9-slice спрайт із запеченим блиском (коралова — головна дія,
    /// одна на екран; бірюзова — підтвердження; скляна — другорядна; вимкнена — плоска, без
    /// блиску й гало), гало під кнопкою, напис Nunito Black. Натиск: масштаб 0.96 і темніший
    /// відтінок на 80 мс — лише через localScale і CanvasRenderer (правило щокадрової анімації).
    ///
    /// Спрайти підставляє збирач (<c>U1.Button</c>): компонент знає лише свій тон і стан.
    /// </summary>
    public sealed class CandyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum Tone
        {
            Coral,
            Teal,
            Glass,
            Off
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Button button;
        [SerializeField] private Image body;
        [SerializeField] private Image? glow;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image? icon;

        [Header("Спрайти тону (ставить збирач)")]
        [SerializeField] private Sprite bodySprite;
        [SerializeField] private Sprite offSprite;

        [SerializeField] private Tone tone = Tone.Coral;
        [SerializeField] private bool small;

        [Tooltip("Скляна кнопка руйнівної дії («Викинути»): напис і іконка coral-300 замість ink.")]
        [SerializeField] private bool destructive;

        private Coroutine? _press;
        private float _scale = 1f;
        private bool _pressed;

        public Button Button => button;
        public Tone CurrentTone => tone;
        public bool IsSmall => small;

        /// <summary>Скляна кнопка руйнівної дії: напис coral-300 (S02Run_03/04 «Викинути»). Ставить збирач.</summary>
        public bool Destructive
        {
            get => destructive;
            set
            {
                destructive = value;
                Apply();
            }
        }

        public string Text
        {
            get => label != null ? label.text : string.Empty;
            set { if (label != null) label.text = value; }
        }

        /// <summary>Чи реагує на тап. Вимкнена — плоска сіра, як у макеті.</summary>
        public bool Interactable
        {
            get => button != null && button.interactable;
            set
            {
                if (button != null) button.interactable = value;
                Apply();
            }
        }

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

        /// <summary>
        /// Екран сховався посеред натиску (тап, що веде на інший екран): корутину вбито на масштабі 0.96 з
        /// темним тілом, і кнопка повернулась би на екран притиснутою. Повертаємо спокій одразу.
        /// </summary>
        private void OnDisable() => ResetPress();

        /// <summary>Спокій кнопки: масштаб 1 і світле тіло. Також коли кнопку вимкнули посеред натиску.</summary>
        private void ResetPress()
        {
            if (_press != null && isActiveAndEnabled)
                StopCoroutine(_press);
            _press = null;
            // Поза натиском нічого не чіпаємо: OnDisable й Apply приходять і з бутстрапа в Edit Mode.
            if (!_pressed && _scale == 1f)
                return;
            _pressed = false;
            _scale = 1f;
            transform.localScale = Vector3.one;
            if (body != null)
                body.canvasRenderer.SetColor(Color.white);
            if (glow != null)
                glow.canvasRenderer.SetColor(Color.white);
        }

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        /// <summary>Збирач: тон, розмір і спрайти.</summary>
        public void Configure(Tone value, bool isSmall, Sprite bodyOfTone, Sprite off)
        {
            tone = value;
            small = isSmall;
            bodySprite = bodyOfTone;
            offSprite = off;
            Apply();
        }

        public void SetTone(Tone value, Sprite bodyOfTone)
        {
            tone = value;
            bodySprite = bodyOfTone;
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            var active = Interactable && tone != Tone.Off;
            if (!Interactable)
                ResetPress();   // вимкнули посеред натиску — не лишати притиснутою
            if (body != null)
            {
                // Підміна спрайта — при зміні стану, не щокадру.
                body.sprite = active ? bodySprite : offSprite;
                body.color = Color.white;
                body.canvasRenderer.SetColor(Color.white);
            }

            if (glow != null)
            {
                var lit = active && (tone == Tone.Coral || tone == Tone.Teal);
                glow.gameObject.SetActive(lit);
                if (lit)
                    glow.color = new Color(1f, 1f, 1f, design.ButtonGlowAlpha);
            }

            if (label != null)
            {
                label.font = design.FontBlack;
                label.fontSize = small ? design.UiButtonSmall : design.UiButton;
                label.fontStyle = FontStyles.Normal;
                label.color = !active ? design.ButtonOffText : tone switch
                {
                    Tone.Teal => design.TealInk,
                    Tone.Glass => destructive ? design.Coral300 : design.InkText,
                    _ => Color.white
                };
                // Тінь тексту — лише на кораловій: окремий матеріал — окремий батч, коралова одна на екран.
                label.fontSharedMaterial = active && tone == Tone.Coral && design.FontBlackShadow != null
                    ? design.FontBlackShadow
                    : label.font != null ? label.font.material : label.fontSharedMaterial;
            }

            if (icon != null)
                icon.color = !active ? design.ButtonOffText
                    : tone == Tone.Teal ? design.TealInk
                    : tone == Tone.Glass && destructive ? design.Coral300
                    : Color.white;
        }

        public void OnPointerDown(PointerEventData eventData) => Press(true);
        public void OnPointerUp(PointerEventData eventData) => Press(false);
        public void OnPointerExit(PointerEventData eventData) => Press(false);

        private void Press(bool down)
        {
            if (design == null || !isActiveAndEnabled || !Interactable)
                return;
            if (_press != null)
                StopCoroutine(_press);
            _press = StartCoroutine(PressRoutine(down));
        }

        private IEnumerator PressRoutine(bool down)
        {
            var style = design;
            if (style == null)
                yield break;

            var from = _scale;
            var to = down ? style.PressScale : 1f;
            var duration = down ? style.PressDarkenDuration : style.PressReleaseDuration;
            var dark = new Color(1f - style.PressDarken, 1f - style.PressDarken, 1f - style.PressDarken, 1f);
            _pressed = down || _pressed;
            if (down)
            {
                body.canvasRenderer.SetColor(dark);
                if (glow != null && glow.gameObject.activeSelf) glow.canvasRenderer.SetColor(dark);
            }

            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                var k = style.CurveBackOut.Evaluate(Mathf.Clamp01(t / duration));
                _scale = Mathf.LerpUnclamped(from, to, k);
                transform.localScale = Vector3.one * _scale;
                yield return null;
            }

            _scale = to;
            transform.localScale = Vector3.one * _scale;
            if (!down)
            {
                _pressed = false;
                body.canvasRenderer.SetColor(Color.white);
                if (glow != null && glow.gameObject.activeSelf) glow.canvasRenderer.SetColor(Color.white);
            }
            _press = null;
        }
    }
}
