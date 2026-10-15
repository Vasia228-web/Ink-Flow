using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Бейдж рідкості U1Glaze (S02Run_02/03, S00): капсула <c>badge_rarity_N</c>, мітки <c>pips_N</c> ліворуч
    /// і назва великими. Рідкість міняється на подію (нова картинка) — підміною спрайтів і ширини міток
    /// (у кожної кількості міток своя ширина з маніфесту), щокадрово не робить нічого.
    /// </summary>
    public sealed class RarityBadgeView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image body;
        [SerializeField] private Image marks;
        [SerializeField] private LayoutElement marksSize;
        [SerializeField] private TMP_Text label;

        [Header("Спрайти й розміри міток (ставить збирач): 6 бейджів, 6 міток, ширина міток у reference-одиницях")]
        [SerializeField] private Sprite[] bodies = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] pips = System.Array.Empty<Sprite>();
        [SerializeField] private Vector2[] pipSizes = System.Array.Empty<Vector2>();

        [SerializeField] private Rarity rarity = Rarity.Common;

        public Rarity Rarity => rarity;
        public string Text => label != null ? label.text : string.Empty;

        /// <summary>Збирач: спрайти бейджів і міток та розміри міток з маніфесту.</summary>
        public void Configure(Sprite[] bodySprites, Sprite[] pipSprites, Vector2[] sizes)
        {
            bodies = bodySprites;
            pips = pipSprites;
            pipSizes = sizes;
            Apply();
        }

        public void SetRarity(Rarity value)
        {
            rarity = value;
            Apply();
        }

        public void Apply()
        {
            var index = Mathf.Clamp((int)rarity, 0, 5);
            if (body != null && index < bodies.Length)
                body.sprite = bodies[index];
            if (marks != null && index < pips.Length)
                marks.sprite = pips[index];
            if (marksSize != null && index < pipSizes.Length)
            {
                marksSize.preferredWidth = marksSize.minWidth = pipSizes[index].x;
                marksSize.preferredHeight = marksSize.minHeight = pipSizes[index].y;
            }
            if (label != null)
            {
                label.text = RarityNames.Of(rarity);
                if (design != null)
                    label.color = design.PanelBottom;
            }
        }
    }
}
