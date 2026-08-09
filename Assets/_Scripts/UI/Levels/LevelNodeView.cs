using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Вузол карти рівнів. Об'єкт із пулу: шлях може бути на сто рівнів, тож
    /// той самий вузол протягом скролу показує різні рівні.
    ///
    /// Три стани відрізняються не відтінком, а вмістом: пройдений — номер і зірки,
    /// замкнений — замок, бос — очі й підпис. Самим лише кольором їх не розрізнити.
    /// </summary>
    public sealed class LevelNodeView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;

        [SerializeField] private RectTransform body;
        [SerializeField] private Button button;
        [SerializeField] private GradientImage drop;
        [SerializeField] private Image gloss;
        [SerializeField] private Image stroke;
        [SerializeField] private Image glow;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private RectTransform lockIcon;
        [SerializeField] private RectTransform bossEyes;
        [SerializeField] private TMP_Text captionLabel;
        [SerializeField] private RectTransform starsRow;
        [SerializeField] private Image[] starPips = System.Array.Empty<Image>();
        [SerializeField] private RectTransform requirementChip;
        [SerializeField] private TMP_Text requirementLabel;

        private LevelNode? _node;
        private System.Action<LevelNode>? _onTap;

        public LevelNode? Node => _node;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(() =>
                {
                    if (_node != null)
                        _onTap?.Invoke(_node);
                });
        }

        public void Bind(System.Action<LevelNode> onTap) => _onTap = onTap;

        /// <summary>
        /// Показує вузол. Викликається лише коли вузол справді змінюється —
        /// не щокадру, тож тут можна вільно чіпати кольори й розміри.
        /// </summary>
        public void Show(LevelNode node, float mockupToReference)
        {
            _node = node;
            if (design == null)
                return;

            gameObject.SetActive(true);

            var boss = node.Kind == LevelNodeKind.Boss;
            var bonus = node.Kind == LevelNodeKind.Bonus;

            // Розміри з макета: бос 84, поточний 66, бонус 46, звичайний 52.
            var size = (boss ? 84f : node.IsCurrent ? 66f : bonus ? 46f : 52f) * mockupToReference;
            if (body != null)
                body.sizeDelta = new Vector2(size, size);

            ApplyDrop(node, boss, bonus);
            ApplyContents(node, boss, bonus, size);
            ApplyStars(node);
        }

        private void ApplyDrop(LevelNode node, bool boss, bool bonus)
        {
            if (drop == null)
                return;

            if (boss)
            {
                // Клякс — не колір палітри, а майже чорна куля з фіолетовим нутром.
                drop.SetGradient(design.BossNodeFrom, design.BossNodeTo);
            }
            else if (node.Locked)
            {
                drop.SetGradient(design.LockedNodeFrom, design.LockedNodeTo);
            }
            else
            {
                var hue = design.Ink(LevelMap.Hue(node.Number));
                drop.SetGradient(DesignSystem.Lighten(hue, 0.5f), DesignSystem.Darken(hue, 0.28f));
            }

            if (gloss != null)
                gloss.gameObject.SetActive(!node.Locked || boss);

            if (stroke != null)
                stroke.color = node.Locked ? design.LockedNodeStroke : Color.clear;

            // Світиться лише поточний вузол і бос — решта не має сперечатися
            // з ними за увагу.
            if (glow == null)
                return;
            var glowing = node.IsCurrent || (boss && !node.Locked);
            glow.gameObject.SetActive(glowing);
            if (!glowing)
                return;
            var glowColor = boss ? design.AccentSecondary : design.Ink(LevelMap.Hue(node.Number));
            glow.color = DesignSystem.WithAlpha(glowColor, design.LevelNodeGlowAlpha);
            _ = bonus;
        }

        private void ApplyContents(LevelNode node, bool boss, bool bonus, float size)
        {
            var showNumber = !boss && !bonus && !node.Locked;
            Toggle(numberLabel, showNumber);
            if (showNumber && numberLabel != null)
            {
                numberLabel.text = node.Number.ToString();
                numberLabel.fontSize = node.IsCurrent
                    ? design.FontSizeLevelNodeCurrent
                    : design.FontSizeLevelNode;
                // Темна цифра на світлій кулі — білу на лаймі чи бурштині не видно.
                numberLabel.color = design.ShopOnLightText;
                numberLabel.fontStyle = FontStyles.Bold;
                if (design.Font != null) numberLabel.font = design.Font;
            }

            Toggle(lockIcon, node.Locked);
            Toggle(bossEyes, boss);

            var showCaption = boss || bonus;
            Toggle(captionLabel, showCaption);
            if (showCaption && captionLabel != null)
            {
                captionLabel.text = boss ? "КЛЯКС" : "БОНУС";
                captionLabel.fontSize = boss ? design.FontSizeLabel : design.FontSizeCaption;
                captionLabel.color = boss ? design.BossCaption : design.TextMuted;
                captionLabel.fontStyle = FontStyles.Bold;
                captionLabel.characterSpacing = design.LetterSpacingWide;
                if (design.Font != null) captionLabel.font = design.Font;
                ((RectTransform)captionLabel.transform).anchoredPosition =
                    new Vector2(0f, -size * 0.5f - design.LevelCaptionOffset);
            }

            // Бонус несе ціну входу — скільки зірок сумарно треба.
            Toggle(requirementChip, bonus);
            if (!bonus || requirementLabel == null)
                return;

            requirementLabel.text = $"<sprite name=\"star\"> {node.StarsRequired}";
            requirementLabel.fontSize = design.FontSizeCaption;
            requirementLabel.color = node.Locked ? design.TextFaint : design.AccentGold;
            requirementLabel.fontStyle = FontStyles.Bold;
            if (design.Font != null) requirementLabel.font = design.Font;
            if (requirementChip != null)
                requirementChip.anchoredPosition =
                    new Vector2(0f, -size * 0.5f - design.LevelCaptionOffset);
        }

        private void ApplyStars(LevelNode node)
        {
            Toggle(starsRow, node.Cleared);
            if (!node.Cleared)
                return;

            for (var i = 0; i < starPips.Length; i++)
            {
                if (starPips[i] == null)
                    continue;
                var earned = i < node.Stars;
                starPips[i].color = earned ? design.AccentGold : design.StarPipEmpty;
            }
        }

        public void Release()
        {
            _node = null;
            gameObject.SetActive(false);
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }
    }
}
