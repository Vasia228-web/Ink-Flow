using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Одна планета каруселі. Об'єкт із пулу: створюється раз, далі лише
    /// перепризначається на іншу планету через <see cref="Bind"/>.
    ///
    /// Уся поверхня, кільце, дуга прогресу й серпанок — це квади з шейдером
    /// InkFlow/Planet у різних режимах. Обертання рахує шейдер за _Time, тож
    /// щокадру на C# тут не відбувається нічого, крім орбіти місяців.
    /// </summary>
    public sealed class PlanetView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;

        [Header("Шари")]
        [SerializeField] private RectTransform body;
        [SerializeField] private Image disc;
        [SerializeField] private Image atmosphere;
        [SerializeField] private Image progressRing;
        [SerializeField] private Image ringBack;
        [SerializeField] private Image ringFront;
        [SerializeField] private RectTransform lockIcon;
        [SerializeField] private RectTransform[] moons = System.Array.Empty<RectTransform>();

        private Material? _discMat;
        private Material? _atmoMat;
        private Material? _progressMat;
        private Material? _ringBackMat;
        private Material? _ringFrontMat;

        private float _moonPhase;
        private bool _hasMoons;
        private float _bodySize;

        /// <summary>Планета, яку зараз показує ця в'юха. null — слот вільний.</summary>
        public PlanetProgress? Planet { get; private set; }

        private void Awake() =>
            // Фаза орбіти своя в кожної в'юхи — інакше місяці на сусідніх планетах
            // ходили б синхронно, як стрілки одного годинника.
            _moonPhase = Random.value * Mathf.PI * 2f;

        /// <summary>
        /// Прив'язує планету до слота. Викликається лише коли слот справді змінює
        /// планету — усі дотики до графіки зібрані тут, а не в LateUpdate.
        /// </summary>
        public void Bind(PlanetProgress planet, bool focused, bool readOnly)
        {
            Planet = planet;
            if (design == null)
                return;

            EnsureMaterials();

            var palette = design.Planet(planet.Type);
            var locked = planet.State == PlanetState.Locked ? 1f : 0f;
            var painted = planet.State == PlanetState.Done ? 1f : planet.Fraction;

            _bodySize = planet.IsFinale ? design.PlanetSizeFinale : design.PlanetSize;
            if (body != null)
                body.sizeDelta = new Vector2(_bodySize, _bodySize);

            SetSphere(_discMat, palette, planet, painted, locked);

            // Атмосфера — у завершених завжди, у поточної лише коли вона у фокусі.
            // На сусідніх планетах серпанок тільки додав би шуму.
            var showAtmo = planet.State == PlanetState.Done ||
                           (planet.State == PlanetState.Current && focused);
            Toggle(atmosphere, showAtmo);
            if (showAtmo && _atmoMat != null)
            {
                _atmoMat.SetColor(AtmoId, palette.Atmosphere);
                Size(atmosphere, _bodySize * design.PlanetAtmosphereScale);
            }

            // Кільце прогресу — тільки у фокусі й тільки коли планета не замкнена:
            // на замкненій показувати нічого, а на сусідніх воно нечитабельне.
            var showProgress = focused && planet.State != PlanetState.Locked;
            Toggle(progressRing, showProgress);
            if (showProgress && _progressMat != null)
            {
                _progressMat.SetColor(AtmoId, palette.Atmosphere);
                _progressMat.SetFloat(PaintedId, planet.State == PlanetState.Done ? 1f : planet.Fraction);
                Size(progressRing, _bodySize * design.PlanetProgressRingScale);
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

            Toggle(lockIcon, planet.State == PlanetState.Locked);

            _hasMoons = planet.HasMoons && planet.State != PlanetState.Locked;
            for (var i = 0; i < moons.Length; i++)
                Toggle(moons[i], _hasMoons);

            _ = readOnly; // розкладка планети від режиму перегляду не залежить
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
            _progressMat = NewMaterial(progressRing, 3f);
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

        private void LateUpdate()
        {
            if (!_hasMoons || design == null)
                return;

            // Орбіта — на localPosition: єдина щокадрова зміна в цій в'юсі,
            // і вона не бруднить графіку.
            var radius = _bodySize * design.PlanetMoonOrbitScale;
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
            DestroyMaterial(_progressMat);
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
