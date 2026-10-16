using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Легка планета без шарів: лише диск шейдера InkFlow/Planet (режим 0), повністю «ожилий». Для дуги планет
    /// у діалозі «Галактику завершено» (S03_04): там їх дев'ять і більше, і повна <see cref="PlanetView"/> з чотирма
    /// матеріалами на кожну з'їла б бюджет викликів. Матеріал свій (MaterialPropertyBlock на UGUI не працює),
    /// створюється на першому показі й живе з об'єктом; щокадру — нічого, обертання рахує шейдер.
    /// </summary>
    public sealed class PlanetDisc : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;
        [SerializeField] private Image disc;

        private Material? _material;

        private static readonly int BaseId = Shader.PropertyToID("_Base");
        private static readonly int LandId = Shader.PropertyToID("_Land");
        private static readonly int AtmoId = Shader.PropertyToID("_Atmo");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int TypeId = Shader.PropertyToID("_Type");
        private static readonly int SpinId = Shader.PropertyToID("_Spin");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int PaintedId = Shader.PropertyToID("_Painted");
        private static readonly int LockedId = Shader.PropertyToID("_Locked");

        public PlanetType Type { get; private set; }

        /// <summary>Показати планету цього типу й назви (сід поверхні — від назви, як у каруселі).</summary>
        public void Show(PlanetType type, string name)
        {
            Type = type;
            if (design == null || disc == null || planetShader == null)
                return;
            if (_material == null)
            {
                _material = new Material(planetShader) { name = "PlanetDisc" };
                _material.SetFloat(ModeId, 0f);
                disc.material = _material;
            }
            var palette = design.Planet(type);
            _material.SetColor(BaseId, palette.Base);
            _material.SetColor(LandId, palette.Land);
            _material.SetColor(AtmoId, palette.Atmosphere);
            _material.SetFloat(TypeId, (float)type);
            _material.SetFloat(SpinId, palette.SecondsPerTurn);
            _material.SetFloat(PaintedId, 1f);
            _material.SetFloat(LockedId, 0f);
            _material.SetFloat(SeedId, Mathf.Abs((name ?? string.Empty).GetHashCode() % 997) * 0.031f);
        }

        private void OnDestroy()
        {
            if (_material == null)
                return;
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
        }
    }
}
