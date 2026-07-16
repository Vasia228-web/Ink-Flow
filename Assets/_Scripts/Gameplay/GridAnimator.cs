using System;
using System.Collections;
using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Відтворює MoveResult як послідовність анімацій (Фаза 2):
    ///  - merge: спрайт A «перетікає» в B (~220ms, OutQuad), B пульсує 1.0→1.15→1.0 (150ms);
    ///  - відхилений свайп: короткий «відскок» A у бік цілі й назад (120ms), хід не витрачено.
    /// Модель уже змінена синхронно (GameSession.TryMove) — аніматор лише візуалізує
    /// перехід, а наприкінці контролер синхронізує в'ю повним Repaint.
    /// </summary>
    public sealed class GridAnimator : MonoBehaviour
    {
        [SerializeField] private GridView view;

        [Header("Merge")]
        [Tooltip("Тривалість переливання A→B, сек (за ТЗ 200-250ms).")]
        [SerializeField, Range(0.1f, 0.4f)] private float mergeFlowDuration = 0.22f;

        [Tooltip("Пульс B після злиття, сек.")]
        [SerializeField, Range(0.05f, 0.3f)] private float mergePulseDuration = 0.15f;

        [SerializeField, Range(1f, 1.5f)] private float mergePulseScale = 1.15f;

        [Header("Відскок")]
        [Tooltip("Тривалість відскоку відхиленого свайпу, сек.")]
        [SerializeField, Range(0.05f, 0.3f)] private float rejectBounceDuration = 0.12f;

        [Tooltip("Відстань відскоку як частка розміру клітинки (у ТЗ «8 units» — трактуємо як ~8-12% клітинки, інакше спрайт летів би через усю дошку).")]
        [SerializeField, Range(0.05f, 0.5f)] private float rejectBounceFraction = 0.12f;

        public bool IsAnimating { get; private set; }

        public void Play(MoveResult result, GridModel finalGrid, Action onComplete)
        {
            Interrupt();
            StartCoroutine(Sequence(result, finalGrid, onComplete));
        }

        /// <summary>Обірвати анімацію (напр. Retry посеред ланцюга) — в'ю досинхронізує Repaint.</summary>
        public void Interrupt()
        {
            StopAllCoroutines();
            IsAnimating = false;
        }

        private IEnumerator Sequence(MoveResult result, GridModel finalGrid, Action onComplete)
        {
            IsAnimating = true;

            if (result.IsValidMerge)
            {
                yield return MergeFlow(result, finalGrid);
                yield return PlayBursts(result);
            }
            else
            {
                yield return RejectBounce(result);
            }

            IsAnimating = false;
            onComplete?.Invoke();
        }

        private IEnumerator MergeFlow(MoveResult result, GridModel finalGrid)
        {
            if (!view.TryGetView(result.From, out var fromView))
                yield break;

            fromView.SetNearMiss(false); // під час польоту не пульсує
            yield return Tween.Move(
                fromView.transform,
                view.CellToWorld(result.From),
                view.CellToWorld(result.To),
                mergeFlowDuration);

            view.ReleaseAt(result.From);

            // Density B одразу після злиття: якщо стався burst — вона зафіксована
            // в першому записі ланцюга (він завжди у клітинці To), інакше — у фінальній моделі.
            var mergedCell = result.ChainLength > 0
                ? new Cell(result.Bursts[0].Color, result.Bursts[0].Density)
                : finalGrid[result.To];

            var toView = view.ShowCell(result.To, mergedCell);
            yield return Tween.Pulse(toView.transform, 1f, mergePulseScale, mergePulseDuration);
        }

        private IEnumerator RejectBounce(MoveResult result)
        {
            if (!view.TryGetView(result.From, out var fromView))
                yield break;

            var origin = view.CellToWorld(result.From);
            // CellToWorld — чиста формула, працює і для цілі поза сіткою.
            var toward = view.CellToWorld(result.To);
            var peak = Vector3.LerpUnclamped(origin, toward, rejectBounceFraction);
            yield return Tween.Bounce(fromView.transform, origin, peak, rejectBounceDuration);
        }

        // Фаза «вибухи»: у цьому коміті — миттєво (без візуалу), послідовність
        // shrink+частки+фідбек додається наступними кроками Фази 2.
        private IEnumerator PlayBursts(MoveResult result)
        {
            yield break;
        }
    }
}
