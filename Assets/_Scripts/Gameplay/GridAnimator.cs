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

        [Header("Burst")]
        [SerializeField] private EffectPool effects;

        [Tooltip("Стискання спрайта до нуля перед частками, сек (за ТЗ 100ms).")]
        [SerializeField, Range(0.05f, 0.3f)] private float burstShrinkDuration = 0.1f;

        [Tooltip("Пауза між ланками ланцюга — щоб chain читався оком.")]
        [SerializeField, Range(0f, 0.4f)] private float interBurstDelay = 0.1f;

        [Tooltip("Поп-ін пофарбованої вибухом клітинки, сек.")]
        [SerializeField, Range(0.02f, 0.2f)] private float paintPopDuration = 0.08f;

        [Tooltip("Скільки перших ланок ланцюга анімувати повністю; далі — миттєво " +
                 "(захист від патологічних ланцюгів до MaxChainBursts, які б анімувались хвилинами).")]
        [SerializeField, Min(1)] private int maxAnimatedLinks = 24;

        [Header("Chain feedback")]
        [SerializeField] private ChainAudio chainAudio;
        [SerializeField] private CameraShaker cameraShaker;

        [Tooltip("З якої ланки ланцюга вмикається camera shake (за ТЗ — з 3-ї).")]
        [SerializeField, Min(1)] private int shakeFromChainLink = 3;

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

        private IEnumerator PlayBursts(MoveResult result)
        {
            for (var i = 0; i < result.Bursts.Count; i++)
            {
                var burst = result.Bursts[i];
                // За межею maxAnimatedLinks решта ланцюга програється миттєво і
                // без часток/звуку — інакше патологічний ланцюг (див. MaxChainBursts)
                // дав би сотні PlayOneShot і спавнів ефектів за один кадр.
                var animated = i < maxAnimatedLinks;

                if (animated)
                    OnChainLink(i);

                if (view.TryGetView(burst.Position, out var burstView))
                {
                    burstView.SetNearMiss(false);
                    if (animated)
                        yield return Tween.Scale(burstView.transform, Vector3.one, Vector3.zero, burstShrinkDuration);
                    view.ReleaseAt(burst.Position);
                }

                if (animated && effects != null)
                    effects.PlayBurst(view.CellToWorld(burst.Position), view.ColorForIndex(burst.Color));

                // Сусіди цього вибуху зі знімка Core: пофарбовані з'являються поп-іном,
                // однокольорові просто оновлюють density (+1 від «мікро-merge»).
                foreach (var change in burst.NeighborChanges)
                {
                    var isNew = !view.TryGetView(change.Position, out _);
                    var cellView = view.ShowCell(change.Position, new Cell(change.Color, change.DensityAfter));
                    if (animated && isNew && change.WasPainted)
                        StartCoroutine(Tween.Scale(cellView.transform, Vector3.zero, Vector3.one, paintPopDuration));
                }

                if (animated && i < result.Bursts.Count - 1)
                    yield return new WaitForSeconds(interBurstDelay);
            }
        }

        /// <summary>Фідбек ланки ланцюга: pitch звуку росте з кожним вибухом, shake — з 3-ї ланки і сильнішає далі.</summary>
        private void OnChainLink(int chainIndex)
        {
            if (chainAudio != null)
                chainAudio.PlayChainPop(chainIndex);

            var linkNumber = chainIndex + 1; // 1-базований номер ланки
            if (cameraShaker != null && linkNumber >= shakeFromChainLink)
                cameraShaker.Shake(0.5f + 0.25f * (linkNumber - shakeFromChainLink));
        }
    }
}
