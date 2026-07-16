using System.Collections;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Простий transform-offset shake (±0.05 world units за ТЗ) — без Cinemachine,
    /// бо для одного ефекту пакет не потрібен. Вмикається з 3-ї ланки ланцюга.
    /// </summary>
    public sealed class CameraShaker : MonoBehaviour
    {
        [Tooltip("Максимальний зсув камери, world units.")]
        [SerializeField, Range(0.01f, 0.3f)] private float amplitude = 0.05f;

        [SerializeField, Range(0.05f, 0.5f)] private float duration = 0.15f;

        private Vector3 _basePosition;
        private Coroutine _active;

        private void Awake() => _basePosition = transform.localPosition;

        /// <summary>strength01 — сила поштовху 0..1 (росте з довжиною ланцюга).</summary>
        public void Shake(float strength01)
        {
            if (_active != null)
                StopCoroutine(_active);
            _active = StartCoroutine(ShakeRoutine(Mathf.Clamp01(strength01)));
        }

        private IEnumerator ShakeRoutine(float strength)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var falloff = 1f - elapsed / duration;
                var offset = Random.insideUnitCircle * (amplitude * strength * falloff);
                transform.localPosition = _basePosition + (Vector3)offset;
                yield return null;
            }

            transform.localPosition = _basePosition;
            _active = null;
        }
    }
}
