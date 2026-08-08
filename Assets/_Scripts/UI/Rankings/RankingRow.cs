using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рядок таблиці лідерів. Об'єкт із пулу: список віртуалізований, тож той самий
    /// рядок протягом скролу показує різних гравців.
    ///
    /// Матеріал планети створюється один раз на рядок і далі лише переналаштовується —
    /// створювати його на кожне перепризначення означало б алокацію під час скролу.
    /// </summary>
    public sealed class RankingRow : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;

        [SerializeField] private Image background;
        [SerializeField] private Image stroke;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text positionLabel;
        [SerializeField] private GradientImage avatar;
        [SerializeField] private Image avatarGloss;
        [SerializeField] private TMP_Text nickLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text unitLabel;
        [SerializeField] private Image planet;
        [SerializeField] private Image planetLock;

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

        public RankPlayer? Player => _player;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(() =>
                {
                    // Інкогніто не відкриваємо: гравець сам сховав профіль.
                    if (_player != null && !_player.Incognito && !_player.IsYou)
                        _onTap?.Invoke(_player);
                });
        }

        public void Bind(System.Action<RankPlayer> onTap) => _onTap = onTap;

        public void Show(RankPlayer player, int position, RankMetric metric, RankPeriod period)
        {
            _player = player;
            if (design == null)
                return;

            gameObject.SetActive(true);
            EnsureMaterial();

            var hidden = player.Incognito;

            if (positionLabel != null)
            {
                positionLabel.text = position.ToString();
                positionLabel.fontSize = design.FontSizeRankRow;
                positionLabel.color = design.TextFaint;
                if (design.Font != null) positionLabel.font = design.Font;
            }

            if (nickLabel != null)
            {
                nickLabel.text = hidden ? "Гравець-інкогніто" : player.Nick;
                nickLabel.fontSize = design.FontSizeRankRow;
                nickLabel.color = hidden ? design.TextMuted : design.TextPrimary;
                if (design.Font != null) nickLabel.font = design.Font;
            }

            if (valueLabel != null)
            {
                valueLabel.text = player.Value(metric, period).ToString("N0").Replace(",", " ");
                valueLabel.fontSize = design.FontSizeRankValue;
                valueLabel.color = design.TextPrimary;
                if (design.Font != null) valueLabel.font = design.Font;
            }

            if (unitLabel != null)
            {
                unitLabel.text = Leaderboard.Unit(metric);
                unitLabel.fontSize = design.FontSizeCaption;
                unitLabel.color = design.TextFaint;
                if (design.Font != null) unitLabel.font = design.Font;
            }

            if (avatar != null)
            {
                var color = hidden ? design.RankIncognito : player.DropColor.ToColor();
                avatar.SetGradient(DesignSystem.Lighten(color, 0.5f), DesignSystem.Darken(color, 0.28f));
            }

            if (avatarGloss != null)
                avatarGloss.color = new Color(1f, 1f, 1f, 0.5f);

            if (background != null)
                background.color = design.GlassFill;
            if (stroke != null)
                stroke.color = design.GlassStroke;

            // Інкогніто — замість планети матова заглушка.
            if (planetLock != null)
            {
                planetLock.gameObject.SetActive(hidden);
                planetLock.color = new Color(1f, 1f, 1f, 0.06f);
            }

            if (planet != null)
            {
                planet.gameObject.SetActive(!hidden);
                if (!hidden && _planetMaterial != null)
                    ApplyPlanet(player);
            }
        }

        private void ApplyPlanet(RankPlayer player)
        {
            var palette = design.Planet(player.Planet);
            _planetMaterial!.SetFloat(ModeId, 0f);
            _planetMaterial.SetFloat(TypeId, (float)player.Planet);
            // Мініатюра не обертається: у списку з десяти планет це був би шум.
            _planetMaterial.SetFloat(SpinId, 0f);
            _planetMaterial.SetFloat(PaintedId, 1f);
            _planetMaterial.SetFloat(LockedId, 0f);
            _planetMaterial.SetFloat(SeedId, Mathf.Abs(player.Id.GetHashCode() % 863) * 0.021f);
            _planetMaterial.SetColor(BaseId, palette.Base);
            _planetMaterial.SetColor(LandId, palette.Land);
            _planetMaterial.SetColor(AtmoId, palette.Atmosphere);
        }

        private void EnsureMaterial()
        {
            if (_planetMaterial != null || planetShader == null || planet == null)
                return;
            _planetMaterial = new Material(planetShader) { name = "RankPlanet" };
            planet.material = _planetMaterial;
        }

        public void Release()
        {
            _player = null;
            gameObject.SetActive(false);
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
