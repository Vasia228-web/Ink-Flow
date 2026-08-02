using System.Collections;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Тряска камери ±0.05 world units (§8/Feel). Без Cinemachine: заради одного ефекту
    /// пакет не потрібен, а transform-offset дає той самий результат і не тягне залежність.
    /// </summary>
    public sealed class ShakeController : MonoBehaviour
    {
        [SerializeField, Range(0.01f, 0.3f)] private float amplitude = 0.05f;
        [SerializeField, Range(0.05f, 0.5f)] private float duration = 0.15f;

        private Vector3 _basePosition;
        private Coroutine? _active;

        private void Awake() => _basePosition = transform.localPosition;

        /// <summary>strength01 — сила поштовху 0..1 (росте з довжиною ланцюга).</summary>
        public void Shake(float strength01)
        {
            if (!isActiveAndEnabled)
                return;
            if (_active != null)
                StopCoroutine(_active);
            _active = StartCoroutine(ShakeRoutine(Mathf.Clamp01(strength01)));
        }

        private IEnumerator ShakeRoutine(float strength)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var falloff = 1f - elapsed / duration;
                // UnityEngine.Random тут доречний: це чиста косметика, не ігрова логіка (§6).
                var offset = Random.insideUnitCircle * (amplitude * strength * falloff);
                transform.localPosition = _basePosition + (Vector3)offset;
                yield return null;
            }

            transform.localPosition = _basePosition;
            _active = null;
        }
    }
}
