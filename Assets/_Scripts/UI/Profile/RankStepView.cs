using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Одна сходинка драбини звань: кружок на лінії, назва й підпис.
    ///
    /// Чотири стани відрізняються не лише кольором, а й змістом кружка — галочка,
    /// зірка чи порожнеча. Тільки кольором їх не розрізнити на маленькому екрані.
    /// </summary>
    public sealed class RankStepView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private GradientImage node;
        [SerializeField] private Image nodeStroke;
        [SerializeField] private Image nodeGlow;
        [SerializeField] private Image checkIcon;
        [SerializeField] private Image starIcon;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;

        public void Show(RankStep step)
        {
            if (design == null)
                return;

            gameObject.SetActive(true);

            var current = step.State == RankStepState.Current;
            var achieved = step.State == RankStepState.Achieved;

            if (node != null)
            {
                if (current)
                    node.SetGradient(design.AccentPrimary, design.AccentSecondary);
                else if (achieved)
                    node.SetGradient(design.RankNodeAchieved, design.RankNodeAchieved);
                else
                    node.SetGradient(design.RankNodeLocked, design.RankNodeLocked);
            }

            if (nodeStroke != null)
                nodeStroke.color = achieved ? design.GlassStroke : design.RankNodeStrokeDim;

            // Світиться лише поточне звання — це головний акцент блоку.
            if (nodeGlow != null)
            {
                nodeGlow.gameObject.SetActive(current);
                if (current)
                    nodeGlow.color = DesignSystem.WithAlpha(design.AccentPrimary, design.RankGlowAlpha);
            }

            if (checkIcon != null)
                checkIcon.gameObject.SetActive(achieved);
            if (starIcon != null)
                starIcon.gameObject.SetActive(current);

            if (titleLabel != null)
            {
                titleLabel.text = step.Title;
                titleLabel.fontSize = current ? design.FontSizeSubtitle : design.FontSizeRankRow;
                titleLabel.color = step.State switch
                {
                    RankStepState.Current => design.TextPrimary,
                    RankStepState.Achieved => design.TextMuted,
                    RankStepState.Next => design.TextDim,
                    _ => design.TextFaintest
                };
                titleLabel.fontStyle = FontStyles.Bold;
                if (design.Font != null) titleLabel.font = design.Font;
            }

            if (subtitleLabel == null)
                return;

            var hasSub = !string.IsNullOrEmpty(step.Subtitle);
            subtitleLabel.gameObject.SetActive(hasSub);
            if (!hasSub)
                return;

            subtitleLabel.text = step.Subtitle;
            subtitleLabel.fontSize = design.FontSizeSmall;
            // «Твоє звання зараз» — бірюзовим, «ще N планети» — жовтим: перше
            // констатує, друге підказує, куди йти.
            subtitleLabel.color = current ? design.AccentTeal : design.AccentGold;
            subtitleLabel.fontStyle = FontStyles.Bold;
            if (design.Font != null) subtitleLabel.font = design.Font;
        }

        public void Release() => gameObject.SetActive(false);
    }
}
