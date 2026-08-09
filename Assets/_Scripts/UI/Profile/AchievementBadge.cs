using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Бейдж досягнення: кольоровий кружок із іконкою або темний силует із замком.
    ///
    /// Замкнений бейдж лишається на місці й клікабельним — тап показує умову.
    /// Ховати недосягнуте означало б приховати від гравця, куди рухатись.
    /// </summary>
    public sealed class AchievementBadge : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Button button;
        [SerializeField] private GradientImage circle;
        [SerializeField] private Image glow;
        [SerializeField] private Image icon;
        [SerializeField] private RectTransform lockBadge;
        [SerializeField] private TMP_Text nameLabel;

        private Achievement? _achievement;
        private System.Action<Achievement>? _onTap;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(() =>
                {
                    if (_achievement != null)
                        _onTap?.Invoke(_achievement);
                });
        }

        public void Bind(System.Action<Achievement> onTap) => _onTap = onTap;

        public void Show(Achievement achievement)
        {
            _achievement = achievement;
            if (design == null)
                return;

            gameObject.SetActive(true);
            var color = achievement.Color.ToColor();

            if (circle != null)
            {
                if (achievement.Unlocked)
                    circle.SetGradient(DesignSystem.Lighten(color, 0.18f), color);
                else
                    circle.SetGradient(design.BadgeLockedFill, design.BadgeLockedFill);
            }

            // Власне світіння — тільки в отриманих: саме воно відрізняє їх здалеку.
            if (glow != null)
            {
                glow.gameObject.SetActive(achievement.Unlocked);
                if (achievement.Unlocked)
                    glow.color = DesignSystem.WithAlpha(color, design.BadgeGlowAlpha);
            }

            if (icon != null)
                icon.color = achievement.Unlocked ? design.TextPrimary : design.TextDim;

            if (lockBadge != null)
                lockBadge.gameObject.SetActive(!achievement.Unlocked);

            if (nameLabel == null)
                return;

            nameLabel.text = achievement.Name;
            nameLabel.fontSize = design.FontSizeCaption;
            nameLabel.color = achievement.Unlocked ? design.TextMuted : design.TextFaintest;
            nameLabel.fontStyle = FontStyles.Bold;
            if (design.Font != null) nameLabel.font = design.Font;
        }

        public void Release()
        {
            _achievement = null;
            gameObject.SetActive(false);
        }
    }
}
