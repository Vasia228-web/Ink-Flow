using System.Collections;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Блок поля або клітинка фігури в лотку: заокруглений квадрат кольору пігменту
    /// з глянцем зверху. Навмисно дешевий — на екрані їх під сотню (64 на полі,
    /// до 15 у лотку), і жодного LateUpdate: дихання крапель тут недоречне,
    /// блоки в сітці мусять стояти рівно.
    ///
    /// Колір задається на подію ходу, а не щокадру; анімації чіпають лише localScale.
    /// </summary>
    public sealed class BlockView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image body;
        [SerializeField] private Image gloss;

        private Coroutine? _anim;

        public bool IsVisible => gameObject.activeSelf;

        /// <summary>Показує блок цим кольором. Виклик — лише на подію, не щокадру.</summary>
        public void Show(Color color)
        {
            if (body != null)
                body.color = color;
            if (gloss != null && design != null)
                gloss.color = new Color(1f, 1f, 1f, design.BlockGlossAlpha);

            transform.localScale = Vector3.one;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            StopAnim();
            transform.localScale = Vector3.one;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        /// <summary>Пружне приземлення фігури на поле.</summary>
        public void PlayLand()
        {
            if (!isActiveAndEnabled || design == null)
                return;
            StopAnim();
            _anim = StartCoroutine(LandRoutine());
        }

        /// <summary>Зрив: блок стискається в нуль і гасне. Дошка чекає на цю корутину.</summary>
        public IEnumerator ClearRoutine(float duration)
        {
            StopAnim();
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                // Перша чверть — коротке розширення, решта — схлопування.
                var scale = k < 0.25f ? 1f + k * 0.8f : Mathf.Lerp(1.2f, 0f, (k - 0.25f) / 0.75f);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }

            Hide();
        }

        private IEnumerator LandRoutine()
        {
            var duration = design.BoardPlaceDuration;
            var curve = design.CurveLand;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = curve.Evaluate(Mathf.Clamp01(t / duration));
                transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, k);
                yield return null;
            }

            transform.localScale = Vector3.one;
            _anim = null;
        }

        private void StopAnim()
        {
            if (_anim == null)
                return;
            StopCoroutine(_anim);
            _anim = null;
        }
    }
}
