using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Одна планета каруселі (U1Glaze S03). Об'єкт із пулу: створюється раз, далі лише
    /// перепризначається на іншу планету через <see cref="Bind"/>.
    ///
    /// Поверхня, кільце й серпанок — квади з шейдером InkFlow/Planet у різних режимах. Обертання рахує
    /// шейдер за _Time, тож щокадру на C# тут не відбувається нічого, крім орбіти місяців. Навколо — кільце
    /// міток, по одній на комірку від 12-ї години за годинником (заповнена — головним кольором своєї картинки),
    /// бейдж-галочка ожилої планети й плитка із замком замкненої — спрайти U1Glaze, розставлені на подію.
    /// Мітки, бейдж і замок лежать під <see cref="FocusOnly"/>: карусель гасить їх, коли планета їде з центру.
    /// </summary>
    public sealed class PlanetView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;

        [Header("Шари")]
        [SerializeField] private RectTransform body;
        [SerializeField] private Image disc;
        [SerializeField] private Image atmosphere;
        [SerializeField] private Image ringBack;
        [SerializeField] private Image ringFront;
        [SerializeField] private RectTransform lockIcon;
        [SerializeField] private RectTransform[] moons = System.Array.Empty<RectTransform>();

        [Header("Мітки комірок, бейдж і замок (U1Glaze S03)")]
        [Tooltip("Те, що видно лише у фокусі: кільце міток і бейдж-галочка.")]
        [SerializeField] private RectTransform? focusOnly;
        [SerializeField] private Image[] pips = System.Array.Empty<Image>();
        [SerializeField] private Image[] pipShines = System.Array.Empty<Image>();
        [SerializeField] private Sprite? pipEmpty;
        [SerializeField] private Sprite? pipFilled;
        [SerializeField] private RectTransform? doneBadge;
        [SerializeField] private RectTransform? doneBadgeIcon;
        [SerializeField] private RectTransform? lockIconImage;

        [Tooltip("Гало серпанку — частка діаметра; 0 — токен planetAtmosphereScale (герой нової галактики — ширше).")]
        [SerializeField] private float hazeScale;

        private Material? _discMat;
        private Material? _atmoMat;
        private Material? _ringBackMat;
        private Material? _ringFrontMat;

        private float _moonPhase;
        private bool _hasMoons;
        private float _bodySize;

        /// <summary>Планета, яку зараз показує ця в'юха. null — слот вільний.</summary>
        public PlanetProgress? Planet { get; private set; }

        /// <summary>Гілка «лише у фокусі» (мітки, бейдж): карусель множить її прозорість на близькість до центру.</summary>
        public RectTransform? FocusOnly => focusOnly;

        /// <summary>Тести: скільки міток видно і які з них заповнені (за номером слота).</summary>
        public int PreviewPipCount
        {
            get
            {
                var n = 0;
                foreach (var pip in pips)
                    if (pip != null && pip.gameObject.activeSelf)
                        n++;
                return n;
            }
        }

        public bool PreviewPipFilled(int slot) => slot >= 0 && slot < pips.Length && pips[slot] != null && pips[slot].sprite == pipFilled;
        public Color PreviewPipColor(int slot) => slot >= 0 && slot < pips.Length && pips[slot] != null ? pips[slot].color : Color.clear;
        public Vector2 PreviewPipPosition(int slot) => slot >= 0 && slot < pips.Length && pips[slot] != null ? pips[slot].rectTransform.anchoredPosition : Vector2.zero;
        public bool PreviewDoneBadge => doneBadge != null && doneBadge.gameObject.activeSelf && focusOnly != null && focusOnly.gameObject.activeSelf;
        public bool PreviewLockShown => lockIcon != null && lockIcon.gameObject.activeSelf;

        private void Awake() =>
            // Фаза орбіти своя в кожної в'юхи — інакше місяці на сусідніх планетах
            // ходили б синхронно, як стрілки одного годинника.
            _moonPhase = Random.value * Mathf.PI * 2f;

        /// <summary>
        /// Прив'язує планету до слота. Викликається лише коли слот справді змінює
        /// планету — усі дотики до графіки зібрані тут, а не в LateUpdate.
        /// </summary>
        /// <param name="pictureColor">Колір мітки заповненої комірки за назвою картинки (головний колір; невідома — нейтральний).</param>
        public void Bind(PlanetProgress planet, bool focused, bool readOnly, System.Func<string, Color>? pictureColor = null)
        {
            Planet = planet;
            if (design == null)
                return;

            EnsureMaterials();

            var palette = design.Planet(planet.Type);
            var isLocked = planet.State == PlanetState.Locked;
            var locked = isLocked ? 1f : 0f;
            var painted = planet.State == PlanetState.Done ? 1f : planet.Fraction;

            _bodySize = planet.IsFinale ? design.PlanetSizeFinale : design.PlanetSize;
            if (body != null)
                body.sizeDelta = new Vector2(_bodySize, _bodySize);

            SetSphere(_discMat, palette, planet, painted, locked);

            // Серпанок-гало — у завершених завжди, у поточної лише коли вона у фокусі.
            // На сусідніх планетах серпанок тільки додав би шуму; у замкненої його немає (S03_03).
            var showAtmo = planet.State == PlanetState.Done ||
                           (planet.State == PlanetState.Current && focused);
            Toggle(atmosphere, showAtmo);
            if (showAtmo && _atmoMat != null)
            {
                _atmoMat.SetColor(AtmoId, palette.Atmosphere);
                var scale = hazeScale > 0f ? hazeScale : design.PlanetAtmosphereScale;
                Size(atmosphere, _bodySize * scale);
                // Пік серпанку — на ободі кулі за будь-якого масштабу квада (звичайний ×1.24 дає рівно 0.8).
                _atmoMat.SetFloat(RimId, HazeRimAtDefault * design.PlanetAtmosphereScale / scale);
            }

            Toggle(ringBack, planet.HasRing);
            Toggle(ringFront, planet.HasRing);
            if (planet.HasRing)
            {
                SetRing(_ringBackMat, palette, locked);
                SetRing(_ringFrontMat, palette, locked);
                var w = _bodySize * design.PlanetRingWidthScale;
                var h = _bodySize * design.PlanetRingHeightScale;
                Size(ringBack, w, h);
                Size(ringFront, w, h);
            }

            // Замок — скляна плитка icon_button з icon_lock по центру кулі (S03_03).
            Toggle(lockIcon, isLocked);
            if (isLocked)
            {
                SizeRect(lockIcon, _bodySize * design.GalaxyLockTile);
                SizeRect(lockIconImage, _bodySize * design.GalaxyLockIcon);
            }

            _hasMoons = planet.HasMoons && !isLocked;
            for (var i = 0; i < moons.Length; i++)
                Toggle(moons[i], _hasMoons);
            PlaceMoons();

            BindPips(planet, isLocked, pictureColor);
            _ = readOnly; // розкладка планети від режиму перегляду не залежить
        }

        /// <summary>
        /// Кільце міток (S03_01): по одній на комірку за номером слота, від 12-ї години за годинником; заповнена —
        /// pip_filled кольору своєї картинки з блиском, порожня — pip_empty. Бейдж-галочка — на ободі о пів на другу.
        /// У замкненої планети немає ні міток, ні бейджа.
        /// </summary>
        private void BindPips(PlanetProgress planet, bool isLocked, System.Func<string, Color>? pictureColor)
        {
            if (focusOnly == null)
                return;

            // Гілка FocusOnly тримає й замок, тож лишається ввімкненою; у замкненої гаснуть лише мітки й бейдж.
            var count = isLocked ? 0 : Mathf.Min(planet.TotalSlots, pips.Length);
            var radius = _bodySize * design.GalaxyPipRing;
            var side = _bodySize * design.GalaxyPipSize;
            var pictures = planet.SlotPictures;
            for (var i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                if (pip == null)
                    continue;
                var used = i < count;
                Toggle(pip, used);
                if (!used)
                    continue;

                var angle = (90f - i * 360f / Mathf.Max(count, 1)) * Mathf.Deg2Rad;
                var rect = pip.rectTransform;
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                rect.sizeDelta = new Vector2(side, side);

                // Без даних по слотах (мок) — перші FilledSlots заповнені нейтральним кольором.
                var id = i < pictures.Count ? pictures[i] : null;
                var filled = pictures.Count > 0 ? id != null : i < planet.FilledSlots;
                pip.sprite = filled ? pipFilled : pipEmpty;
                pip.color = !filled ? Color.white
                    : id != null && pictureColor != null ? pictureColor(id)
                    : design.InkDim;
                if (i < pipShines.Length && pipShines[i] != null)
                    Toggle(pipShines[i], filled);
            }

            var done = planet.State == PlanetState.Done;
            Toggle(doneBadge, done);
            if (done && doneBadge != null)
            {
                var offset = _bodySize * design.GalaxyBadgeOffset;
                doneBadge.anchoredPosition = new Vector2(offset, offset);
                SizeRect(doneBadge, _bodySize * design.GalaxyBadgeSize);
                SizeRect(doneBadgeIcon, _bodySize * design.GalaxyBadgeIcon);
            }
        }

        private static void SizeRect(RectTransform? rect, float side)
        {
            if (rect != null)
                rect.sizeDelta = new Vector2(side, side);
        }

        /// <summary>Слот поза вікном каруселі: гасимо цілком, щоб не платити за рендер.</summary>
        public void Release()
        {
            Planet = null;
            gameObject.SetActive(false);
        }

        private static readonly int BaseId = Shader.PropertyToID("_Base");
        private static readonly int LandId = Shader.PropertyToID("_Land");
        private static readonly int AtmoId = Shader.PropertyToID("_Atmo");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int TypeId = Shader.PropertyToID("_Type");
        private static readonly int SpinId = Shader.PropertyToID("_Spin");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int PaintedId = Shader.PropertyToID("_Painted");
        private static readonly int LockedId = Shader.PropertyToID("_Locked");
        private static readonly int RimId = Shader.PropertyToID("_Rim");

        /// <summary>Пік оболонки серпанку при звичайному масштабі атмосфери (шейдер, гілка 4).</summary>
        private const float HazeRimAtDefault = 0.8f;

        /// <summary>
        /// Свій матеріал на кожен шар: значення різні для кожної планети, а
        /// MaterialPropertyBlock на UGUI не працює — канвас склеює батч і губить його.
        /// Матеріалів мало (слотів у пулі одиниці) і живуть вони стільки ж, скільки слот.
        /// </summary>
        private void EnsureMaterials()
        {
            if (_discMat != null || planetShader == null)
                return;

            _discMat = NewMaterial(disc, 0f);
            _atmoMat = NewMaterial(atmosphere, 4f);
            _ringBackMat = NewMaterial(ringBack, 1f);
            _ringFrontMat = NewMaterial(ringFront, 2f);
        }

        private Material? NewMaterial(Image? target, float mode)
        {
            if (target == null)
                return null;
            var material = new Material(planetShader) { name = $"Planet_{mode}" };
            material.SetFloat(ModeId, mode);
            target.material = material;
            return material;
        }

        private void SetSphere(Material? material, PlanetPalette palette, PlanetProgress planet,
            float painted, float locked)
        {
            if (material == null)
                return;
            material.SetColor(BaseId, palette.Base);
            material.SetColor(LandId, palette.Land);
            material.SetColor(AtmoId, palette.Atmosphere);
            material.SetFloat(TypeId, (float)planet.Type);
            material.SetFloat(SpinId, palette.SecondsPerTurn);
            material.SetFloat(PaintedId, painted);
            material.SetFloat(LockedId, locked);
            // Сід від назви: та сама планета завжди виглядає однаково, різні —
            // по-різному, і для цього не треба зберігати жодного числа.
            material.SetFloat(SeedId, Mathf.Abs(planet.Name.GetHashCode() % 997) * 0.031f);
        }

        private void SetRing(Material? material, PlanetPalette palette, float locked)
        {
            if (material == null)
                return;
            material.SetColor(BaseId, palette.Base);
            material.SetColor(AtmoId, palette.Atmosphere);
            material.SetFloat(LockedId, locked);
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private static void Size(Image? target, float side) => Size(target, side, side);

        private static void Size(Image? target, float width, float height)
        {
            if (target != null)
                target.rectTransform.sizeDelta = new Vector2(width, height);
        }

        private void LateUpdate() => PlaceMoons();

        /// <summary>
        /// Орбіта — на localPosition: єдина щокадрова зміна в цій в'юсі, і вона не бруднить графіку.
        /// Кличеться й з <see cref="Bind"/>: до першого кадру (і в знімках Edit Mode) місяць інакше стояв би
        /// в центрі кулі білою крапкою.
        /// </summary>
        private void PlaceMoons()
        {
            if (!_hasMoons || design == null)
                return;

            // Усередині кільця міток (токен galaxyMoonOrbit): на старій 0.62 місяць ховався під мітки о 3-й і 9-й.
            var radius = _bodySize * design.GalaxyMoonOrbit;
            for (var i = 0; i < moons.Length; i++)
            {
                if (moons[i] == null)
                    continue;

                var period = design.PlanetMoonPeriod * (1f + i * 0.38f);
                var a = (Time.time / period + _moonPhase + i * 1.7f) * Mathf.PI * 2f;
                // Еліпс, а не коло: місяць має йти «за» планету, а не збоку.
                moons[i].localPosition = new Vector3(
                    Mathf.Cos(a) * radius,
                    Mathf.Sin(a) * radius * 0.34f,
                    0f);
            }
        }

        private void OnDestroy()
        {
            DestroyMaterial(_discMat);
            DestroyMaterial(_atmoMat);
            DestroyMaterial(_ringBackMat);
            DestroyMaterial(_ringFrontMat);
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
