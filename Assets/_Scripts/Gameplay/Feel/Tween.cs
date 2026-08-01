using System;
using System.Collections;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Мінімальні coroutine-твіни. Свідомо замість DOTween: твінів у грі небагато
    /// (merge/burst/pulse/bounce), а власний хелпер на ~50 рядків не тягне зовнішню залежність.
    /// Швидкість анімацій керується з конфігу — при AnimationSpeed = 0 усе стає миттєвим (§8).
    /// </summary>
    public static class Tween
    {
        /// <summary>Ease.OutQuad — швидкий старт, м'яке гальмування.</summary>
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float Linear(float t) => t;

        public static IEnumerator Animate(float duration, Func<float, float> ease, Action<float> apply)
        {
            if (duration <= 0f)
            {
                apply(1f);
                yield break;
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                apply(ease(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            apply(1f);
        }

        public static IEnumerator Move(Transform target, Vector3 from, Vector3 to, float duration) =>
            Animate(duration, OutQuad, t => target.position = Vector3.LerpUnclamped(from, to, t));

        public static IEnumerator Scale(Transform target, Vector3 from, Vector3 to, float duration) =>
            Animate(duration, OutQuad, t => target.localScale = Vector3.LerpUnclamped(from, to, t));

        public static IEnumerator Pulse(Transform target, float baseScale, float peakScale, float duration) =>
            Animate(duration, Linear, t =>
            {
                var wave = t < 0.5f ? OutQuad(t * 2f) : 1f - OutQuad((t - 0.5f) * 2f);
                target.localScale = Vector3.one * Mathf.LerpUnclamped(baseScale, peakScale, wave);
            });

        public static IEnumerator Bounce(Transform target, Vector3 origin, Vector3 peak, float duration) =>
            Animate(duration, Linear, t =>
            {
                var wave = t < 0.5f ? OutQuad(t * 2f) : 1f - OutQuad((t - 0.5f) * 2f);
                target.position = Vector3.LerpUnclamped(origin, peak, wave);
            });
    }
}
