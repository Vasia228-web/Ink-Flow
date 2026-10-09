using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Одна колонка подіуму топ-3: бейдж місця, планета з гало в колір медалі,
    /// нік і число.
    ///
    /// Планета — той самий шейдер, що в Галактиці, лише розмір і швидкість інші.
    /// Гало кольору медалі — це єдине, що відрізняє перше місце від третього,
    /// окрім розміру: у макеті воно й несе всю ієрархію.
    /// </summary>
    public sealed class PodiumSlot : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;

        [SerializeField] private Button button;
        [SerializeField] private GradientImage crown;
        [SerializeField] private TMP_Text crownLabel;
        [SerializeField] private Image glow;
        [SerializeField] private Image planet;
        [SerializeField] private TMP_Text nickLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text unitLabel;

        [Tooltip("Місце: 1, 2 або 3. Від нього залежать розмір, колір медалі й підйом.")]
        [SerializeField, Range(1, 3)] private int place = 1;

        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int TypeId = Shader.PropertyToID("_Type");
        private static readonly int SpinId = Shader.PropertyToID("_Spin");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int PaintedId = Shader.PropertyToID("_Painted");
        private static readonly int LockedId = Shader.PropertyToID("_Locked");
        private static readonly int BaseId = Shader.PropertyToID("_Base");
        private static readonly int LandId = Shader.PropertyToID("_Land");
        private static readonly int AtmoId = Shader.PropertyToID("_Atmo");

        private Material? _planetMaterial;
        private RankPlayer? _player;
        private System.Action<RankPlayer>? _onTap;

        public int Place => place;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(() =>
                {
                    if (_player != null && !_player.Incognito)
                        _onTap?.Invoke(_player);
                });
        }

        public void Bind(System.Action<RankPlayer> onTap) => _onTap = onTap;

        public void Show(RankPlayer player, RankMetric metric)
        {
            _player = player;
            if (design == null)
                return;

            gameObject.SetActive(true);
            EnsureMaterial();

            var medal = design.Medal(place);
            var hidden = player.Incognito;

            if (crown != null)
                crown.SetGradient(DesignSystem.Lighten(medal, 0.24f), medal);

            if (crownLabel != null)
            {
                crownLabel.text = place.ToString();
                crownLabel.fontSize = design.FontSizeGalaxyTitle;
                crownLabel.color = design.MedalText;
                if (design.Font != null) crownLabel.font = design.Font;
            }

            if (glow != null)
                glow.color = DesignSystem.WithAlpha(medal, design.PodiumGlowAlpha);

            if (nickLabel != null)
            {
                nickLabel.text = hidden ? "Інкогніто" : player.Nick;
                nickLabel.fontSize = design.FontSizeShopCard;
                nickLabel.color = design.TextPrimary;
                if (design.Font != null) nickLabel.font = design.Font;
            }

            if (valueLabel != null)
            {
                valueLabel.text = ScoreFormat.Full(player.Value);
                // Число фарбуємо в колір медалі — саме воно, а не нік: у макеті
                // ієрархію тримають цифри.
                valueLabel.fontSize = place == 1 ? design.FontSizePodiumFirst : design.FontSizePodiumOther;
                valueLabel.color = medal;
                if (design.Font != null) valueLabel.font = design.Font;
            }

            if (unitLabel != null)
            {
                unitLabel.text = Leaderboard.Unit(metric);
                unitLabel.fontSize = design.FontSizeCaption;
                unitLabel.color = design.TextFaint;
                if (design.Font != null) unitLabel.font = design.Font;
            }

            if (planet == null || _planetMaterial == null)
                return;

            var type = hidden ? PlanetTypeForIncognito : player.Planet;
            var palette = design.Planet(type);
            _planetMaterial.SetFloat(ModeId, 0f);
            _planetMaterial.SetFloat(TypeId, (float)type);
            // Перше місце крутиться помітно швидше — воно живіше за решту.
            _planetMaterial.SetFloat(SpinId, place == 1 ? design.PodiumSpinFirst : design.PodiumSpinOther);
            _planetMaterial.SetFloat(PaintedId, hidden ? 0f : 1f);
            _planetMaterial.SetFloat(LockedId, hidden ? 1f : 0f);
            _planetMaterial.SetFloat(SeedId, Mathf.Abs(player.Id.GetHashCode() % 863) * 0.021f);
            _planetMaterial.SetColor(BaseId, palette.Base);
            _planetMaterial.SetColor(LandId, palette.Land);
            _planetMaterial.SetColor(AtmoId, palette.Atmosphere);
        }

        /// <summary>Схований профіль показуємо кам'янистою сірою планетою.</summary>
        private const InkFlow.Core.PlanetType PlanetTypeForIncognito = InkFlow.Core.PlanetType.Rocky;

        public void Release()
        {
            _player = null;
            gameObject.SetActive(false);
        }

        private void EnsureMaterial()
        {
            if (_planetMaterial != null || planetShader == null || planet == null)
                return;
            _planetMaterial = new Material(planetShader) { name = $"Podium{place}" };
            planet.material = _planetMaterial;
        }

        private void OnDestroy()
        {
            if (_planetMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_planetMaterial);
            else
                DestroyImmediate(_planetMaterial);
        }
    }
}
