using System.Collections;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Профіль: візитка, драбина звань, статистика, палітра, вітрина й досягнення.
    ///
    /// Скролиться цілком разом із шапкою — так у макеті. Планета вітрини малюється
    /// тим самим шейдером, що в Галактиці й Рейтингах.
    /// </summary>
    public sealed class ProfileScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button settingsButton;

        [Header("Візитка")]
        [SerializeField] private DropView avatar;
        [SerializeField] private Button editAvatarButton;
        [SerializeField] private GradientImage editAvatarFill;
        [SerializeField] private TMP_Text nickLabel;
        [SerializeField] private GradientImage rankCapsule;
        [SerializeField] private Image rankCapsuleGlow;
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private TMP_Text oilValue;
        [SerializeField] private TMP_Text oilWord;

        [Header("Звання")]
        [SerializeField] private TMP_Text ladderCaption;
        [SerializeField] private RankStepView[] ladder = System.Array.Empty<RankStepView>();

        [Header("Статистика")]
        [SerializeField] private TMP_Text[] statValues = System.Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] statLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] private Image[] statStars = System.Array.Empty<Image>();

        [Header("Вітрина")]
        [SerializeField] private TMP_Text showcaseCaption;
        [SerializeField] private Image showcasePlanet;
        [SerializeField] private TMP_Text showcaseName;
        [SerializeField] private Button[] thumbButtons = System.Array.Empty<Button>();
        [SerializeField] private Image[] thumbPlanets = System.Array.Empty<Image>();
        [SerializeField] private Image[] thumbRings = System.Array.Empty<Image>();

        [Header("Досягнення")]
        [SerializeField] private TMP_Text achievementsCaption;
        [SerializeField] private AchievementBadge[] badges = System.Array.Empty<AchievementBadge>();
        [SerializeField] private RectTransform toast;
        [SerializeField] private TMP_Text toastLabel;

        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int TypeId = Shader.PropertyToID("_Type");
        private static readonly int SpinId = Shader.PropertyToID("_Spin");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int PaintedId = Shader.PropertyToID("_Painted");
        private static readonly int LockedId = Shader.PropertyToID("_Locked");
        private static readonly int BaseId = Shader.PropertyToID("_Base");
        private static readonly int LandId = Shader.PropertyToID("_Land");
        private static readonly int AtmoId = Shader.PropertyToID("_Atmo");

        private PlayerProfile? _profile;
        private Material? _showcaseMaterial;
        private Material?[] _thumbMaterials = System.Array.Empty<Material?>();
        private Coroutine? _toast;

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;


        /// <summary>Гравець хоче змінити нік — олівець біля аватара.</summary>
        public System.Action? NickEditRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (settingsButton != null)
                WireSettingsButton(settingsButton);
            if (editAvatarButton != null)
                editAvatarButton.onClick.AddListener(() => NickEditRequested?.Invoke());

            for (var i = 0; i < thumbButtons.Length; i++)
            {
                var index = i;
                thumbButtons[i]?.onClick.AddListener(() => SelectShowcase(index));
            }
        }

        private void Start()
        {
            for (var i = 0; i < badges.Length; i++)
                badges[i]?.Bind(ShowRequirement);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _profile = State != null ? PlayerProfile.FromState(State) : PlayerProfile.CreateMock();
            Apply();
        }

        /// <summary>Перечитати стан без повторного входу на екран.</summary>
        public void Refresh()
        {
            _profile = null;
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            _profile ??= State != null ? PlayerProfile.FromState(State) : PlayerProfile.CreateMock();

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "ПРОФІЛЬ";

            Caption(ladderCaption, "ЗВАННЯ");
            Caption(showcaseCaption, "ВІТРИНА");
            Caption(achievementsCaption, "ДОСЯГНЕННЯ");

            ApplyIdentity();
            ApplyLadder();
            ApplyStats();
            ApplyShowcase();
            ApplyAchievements();

            if (toast != null)
                toast.gameObject.SetActive(false);
        }

        private void Caption(TMP_Text? label, string text)
        {
            if (label == null)
                return;
            label.text = text;
            label.fontSize = design.FontSizeSmall;
            label.color = design.TextFaint;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = design.LetterSpacingWide;
            if (design.Font != null) label.font = design.Font;
        }

        private void ApplyIdentity()
        {
            var profile = _profile!;

            avatar?.Apply();

            if (editAvatarFill != null)
                editAvatarFill.SetGradient(design.AccentTeal, design.AccentBlue);

            ApplyFont(nickLabel, design.FontSizeProfileNick, design.TextPrimary, FontStyles.Bold, 0f);
            if (nickLabel != null) nickLabel.text = profile.Nick;

            if (rankCapsule != null)
                rankCapsule.SetGradient(design.ShopTabActiveFrom, design.ShopTabActiveTo);
            if (rankCapsuleGlow != null)
                rankCapsuleGlow.color = DesignSystem.WithAlpha(design.AccentPrimary, design.RankCapsuleGlowAlpha);

            ApplyFont(rankLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            if (rankLabel != null) rankLabel.text = profile.RankTitle;

            ApplyFont(oilValue, design.FontSizeShopPrice, design.TextPrimary, FontStyles.Bold, 0f);
            if (oilValue != null) oilValue.text = profile.Oil.ToString("N0").Replace(",", " ");

            ApplyFont(oilWord, design.FontSizeCardSubtitle, design.TextFaint, FontStyles.Normal, 0f);
            if (oilWord != null) oilWord.text = "нафти";
        }

        private void ApplyLadder()
        {
            var steps = _profile!.Ladder;
            for (var i = 0; i < ladder.Length; i++)
            {
                if (ladder[i] == null)
                    continue;
                if (i < steps.Count)
                    ladder[i].Show(steps[i]);
                else
                    ladder[i].Release();
            }
        }

        private void ApplyStats()
        {
            var stats = _profile!.Stats;
            for (var i = 0; i < statValues.Length; i++)
            {
                var used = i < stats.Count;
                if (i < statLabels.Length)
                    Toggle(statLabels[i], used);
                Toggle(statValues[i], used);
                if (i < statStars.Length)
                    Toggle(statStars[i], used && stats[i].Star);
                if (!used)
                    continue;

                var stat = stats[i];
                ApplyFont(statValues[i], design.FontSizeProfileStat, stat.Color.ToColor(),
                    FontStyles.Bold, 0f);
                statValues[i].text = stat.Value;

                if (i >= statLabels.Length)
                    continue;
                ApplyFont(statLabels[i], design.FontSizeLabel, design.TextMuted, FontStyles.Normal, 0f);
                statLabels[i].text = stat.Label;

                // Зірка перед числом — не символ, а спрайт: ★ у Nunito немає.
                if (i < statStars.Length && statStars[i] != null && stat.Star)
                    statStars[i].color = stat.Color.ToColor();
            }
        }

        private void SelectShowcase(int index)
        {
            if (_profile == null || index < 0 || index >= _profile.Showcase.Count)
                return;
            _profile.FavouriteIndex = index;
            ApplyShowcase();
        }

        private void ApplyShowcase()
        {
            var profile = _profile!;
            if (showcasePlanet != null && planetShader != null)
            {
                if (_showcaseMaterial == null)
                {
                    _showcaseMaterial = new Material(planetShader) { name = "Showcase" };
                    showcasePlanet.material = _showcaseMaterial;
                }

                var favourite = profile.Favourite;
                if (favourite != null)
                    ApplyPlanet(_showcaseMaterial, favourite, design.ShowcaseSpin);
            }

            ApplyFont(showcaseName, design.FontSizeShopPrice, design.TextPrimary, FontStyles.Bold, 0f);
            if (showcaseName != null && profile.Favourite != null)
                showcaseName.text = $"Моя гордість · {profile.Favourite.Name}";

            EnsureThumbMaterials();
            for (var i = 0; i < thumbPlanets.Length; i++)
            {
                var used = i < profile.Showcase.Count;
                Toggle(thumbPlanets[i], used);
                if (i < thumbButtons.Length)
                    Toggle(thumbButtons[i], used);
                if (i < thumbRings.Length && thumbRings[i] != null)
                {
                    // Обраний бачок обведено — інакше незрозуміло, який саме
                    // із трьох зараз на вітрині.
                    thumbRings[i].gameObject.SetActive(used && i == profile.FavouriteIndex);
                    thumbRings[i].color = design.TextPrimary;
                }

                if (!used || i >= _thumbMaterials.Length || _thumbMaterials[i] == null)
                    continue;
                // Мініатюри не крутяться: три планети, що обертаються поруч, — шум.
                ApplyPlanet(_thumbMaterials[i]!, profile.Showcase[i], 0f);
            }
        }

        private void EnsureThumbMaterials()
        {
            if (_thumbMaterials.Length == thumbPlanets.Length || planetShader == null)
                return;

            _thumbMaterials = new Material?[thumbPlanets.Length];
            for (var i = 0; i < thumbPlanets.Length; i++)
            {
                if (thumbPlanets[i] == null)
                    continue;
                _thumbMaterials[i] = new Material(planetShader) { name = $"Thumb{i}" };
                thumbPlanets[i].material = _thumbMaterials[i];
            }
        }

        private void ApplyPlanet(Material material, ShowcasePlanet planet, float spin)
        {
            var palette = design.Planet(planet.Type);
            material.SetFloat(ModeId, 0f);
            material.SetFloat(TypeId, (float)planet.Type);
            material.SetFloat(SpinId, spin);
            material.SetFloat(PaintedId, 1f);
            material.SetFloat(LockedId, 0f);
            material.SetFloat(SeedId, Mathf.Abs(planet.Id.GetHashCode() % 743) * 0.019f);
            material.SetColor(BaseId, palette.Base);
            material.SetColor(LandId, palette.Land);
            material.SetColor(AtmoId, palette.Atmosphere);
        }

        private void ApplyAchievements()
        {
            var list = _profile!.Achievements;
            for (var i = 0; i < badges.Length; i++)
            {
                if (badges[i] == null)
                    continue;
                if (i < list.Count)
                    badges[i].Show(list[i]);
                else
                    badges[i].Release();
            }
        }

        private void ShowRequirement(Achievement achievement)
        {
            if (toast == null || toastLabel == null || design == null)
                return;

            ApplyFont(toastLabel, design.FontSizeSmall, design.TextPrimary, FontStyles.Normal, 0f);
            toastLabel.text = achievement.Unlocked
                ? $"<b>{achievement.Name}</b> · отримано"
                : $"<b>{achievement.Name}</b> · {achievement.Requirement}";

            toast.gameObject.SetActive(true);
            if (!isActiveAndEnabled)
                return;

            if (_toast != null)
                StopCoroutine(_toast);
            _toast = StartCoroutine(HideToast());
        }

        private IEnumerator HideToast()
        {
            yield return new WaitForSeconds(design.ToastDuration);
            if (toast != null)
                toast.gameObject.SetActive(false);
            _toast = null;
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private void ApplyFont(TMP_Text? label, float size, Color color, FontStyles style, float spacing)
        {
            if (label == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.characterSpacing = spacing;
            if (design.Font != null)
                label.font = design.Font;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_showcaseMaterial);
            for (var i = 0; i < _thumbMaterials.Length; i++)
                DestroyMaterial(_thumbMaterials[i]);
        }

        private static void DestroyMaterial(Material? material)
        {
            if (material == null)
                return;
            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }
}
