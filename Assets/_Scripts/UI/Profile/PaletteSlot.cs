using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Фарба в палітрі профілю: куля кольору, мензурка залишку, назва й літри.
    ///
    /// Мензурка — той самий <see cref="BeakerGauge"/>, що в картці магазину.
    /// </summary>
    public sealed class PaletteSlot : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private GradientImage drop;
        [SerializeField] private Image gloss;
        [SerializeField] private BeakerGauge beaker;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text litersLabel;

        public void Show(PaintKind kind, float liters)
        {
            if (design == null)
                return;

            gameObject.SetActive(true);
            var color = design.Paint(kind);

            if (drop != null)
                drop.SetGradient(
                    DesignSystem.Lighten(color, 0.5f),
                    DesignSystem.Darken(color, 0.28f));

            if (gloss != null)
                gloss.color = new Color(1f, 1f, 1f, 0.6f);

            beaker?.Show(liters, color);

            if (nameLabel != null)
            {
                nameLabel.text = design.PaintName(kind);
                nameLabel.fontSize = design.FontSizeCaption;
                nameLabel.color = design.TextMuted;
                nameLabel.fontStyle = FontStyles.Bold;
                if (design.Font != null) nameLabel.font = design.Font;
            }

            if (litersLabel == null)
                return;

            // Ціле показуємо без «.0»: «6 л» читається, «6.0 л» — ні.
            litersLabel.text = $"{liters:0.#} л";
            litersLabel.fontSize = design.FontSizeCaption;
            litersLabel.color = design.TextFaint;
            if (design.Font != null) litersLabel.font = design.Font;
        }

        public void Release() => gameObject.SetActive(false);
    }
}
