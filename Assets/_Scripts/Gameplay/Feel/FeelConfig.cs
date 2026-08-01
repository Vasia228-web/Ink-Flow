using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Таймінги feel-шару (§8). Винесені в конфіг, щоб AnimationSpeed = 0 давав миттєвий
    /// режим — і для тестів, і для «швидкого рестарту &lt; 300 мс».
    /// </summary>
    [CreateAssetMenu(fileName = "FeelConfig", menuName = "Ink Flow/Feel Config")]
    public sealed class FeelConfig : ScriptableObject
    {
        [Tooltip("Множник швидкості анімацій. 1 = нормально, більше = швидше, 0 = миттєво.")]
        [SerializeField, Range(0f, 8f)] private float animationSpeed = 1f;

        [Header("Merge")]
        [SerializeField, Range(0f, 0.5f)] private float mergeFlowDuration = 0.22f;
        [SerializeField, Range(0f, 0.4f)] private float mergePulseDuration = 0.15f;
        [SerializeField, Range(1f, 1.6f)] private float mergePulseScale = 1.15f;

        [Header("Відхилений свайп")]
        [SerializeField, Range(0f, 0.4f)] private float rejectBounceDuration = 0.12f;
        [SerializeField, Range(0.02f, 0.5f)] private float rejectBounceFraction = 0.12f;

        [Header("Burst")]
        [SerializeField, Range(0f, 0.4f)] private float burstShrinkDuration = 0.1f;
        [SerializeField, Range(0f, 0.3f)] private float paintPopDuration = 0.08f;
        [SerializeField, Range(0f, 0.4f)] private float interBurstDelay = 0.08f;

        [Header("Ланцюг")]
        [Tooltip("З якої ланки ланцюга вмикається тряска камери.")]
        [SerializeField, Min(1)] private int shakeFromChainLink = 3;

        [Tooltip("Скільки перших ланок анімувати повністю; далі — миттєво (захист від довгих ланцюгів).")]
        [SerializeField, Min(1)] private int maxAnimatedLinks = 24;

        [Header("Near-miss")]
        [Tooltip("Частка порогу, з якої крапля починає пульсувати підсвіткою.")]
        [SerializeField, Range(0.5f, 1f)] private float nearMissFraction = 0.8f;

        public float AnimationSpeed => animationSpeed;
        public bool Instant => animationSpeed <= 0f;

        public float MergeFlowDuration => Scaled(mergeFlowDuration);
        public float MergePulseDuration => Scaled(mergePulseDuration);
        public float MergePulseScale => mergePulseScale;
        public float RejectBounceDuration => Scaled(rejectBounceDuration);
        public float RejectBounceFraction => rejectBounceFraction;
        public float BurstShrinkDuration => Scaled(burstShrinkDuration);
        public float PaintPopDuration => Scaled(paintPopDuration);
        public float InterBurstDelay => Scaled(interBurstDelay);
        public int ShakeFromChainLink => shakeFromChainLink;
        public int MaxAnimatedLinks => maxAnimatedLinks;
        public float NearMissFraction => nearMissFraction;

        private float Scaled(float seconds) => Instant ? 0f : seconds / animationSpeed;
    }
}
