using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Одна зона картинки в забігу: Image з маскою зони й матеріалом InkFlow/PictureZone.
    /// Заливка — властивість матеріалу (<c>_Fill</c>), тож анімація її не бруднить графіку.
    /// </summary>
    public sealed class PictureZoneView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader zoneShader;
        [SerializeField] private Image image;

        private static readonly int PaintId = Shader.PropertyToID("_Paint");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int OriginId = Shader.PropertyToID("_Origin");
        private static readonly int ExtentId = Shader.PropertyToID("_Extent");
        private static readonly int SelectedId = Shader.PropertyToID("_Selected");
        private static readonly int IdleAlphaId = Shader.PropertyToID("_IdleAlpha");
        private static readonly int ActiveAlphaId = Shader.PropertyToID("_ActiveAlpha");

        private Material? _material;
        private Coroutine? _fill;
        private float _shown;

        public bool IsBound { get; private set; }

        public void Bind(Sprite mask, Vector2 originUv, float extent, Hue hue, float fill, bool selected)
        {
            EnsureMaterial();
            if (image != null)
            {
                image.sprite = mask;
                image.enabled = true;
            }
            gameObject.SetActive(true);
            IsBound = true;

            if (_fill != null)
            {
                StopCoroutine(_fill);
                _fill = null;
            }

            if (_material == null || design == null)
                return;
            _material.SetColor(PaintId, design.HueColor(hue));
            _material.SetVector(OriginId, new Vector4(originUv.x, originUv.y, 0f, 0f));
            _material.SetFloat(ExtentId, extent);
            _material.SetFloat(SelectedId, selected ? 1f : 0f);
            _material.SetFloat(IdleAlphaId, design.ZoneIdleAlpha);
            _material.SetFloat(ActiveAlphaId, design.ZoneActiveAlpha);
            _shown = fill;
            _material.SetFloat(FillId, fill);
        }

        public void SetSelected(bool selected)
        {
            if (_material != null)
                _material.SetFloat(SelectedId, selected ? 1f : 0f);
        }

        /// <summary>Мазок: фронт іде від точки, куди впав виплеск, до нової частки.</summary>
        public void PlayFill(float fraction, Vector2 originUv, float duration)
        {
            if (_material == null)
                return;
            _material.SetVector(OriginId, new Vector4(originUv.x, originUv.y, 0f, 0f));

            if (_fill != null)
                StopCoroutine(_fill);

            if (!isActiveAndEnabled || duration <= 0f)
            {
                _shown = fraction;
                _material.SetFloat(FillId, fraction);
                return;
            }

            _fill = StartCoroutine(FillRoutine(fraction, duration));
        }

        private IEnumerator FillRoutine(float target, float duration)
        {
            var from = _shown;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                // Швидко на початку, м'яко наприкінці — рідина «наздоганяє» край.
                var k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 2f);
                _shown = Mathf.Lerp(from, target, k);
                _material!.SetFloat(FillId, _shown);
                yield return null;
            }

            _shown = target;
            _material!.SetFloat(FillId, target);
            _fill = null;
        }

        public void Release()
        {
            if (_fill != null)
                StopCoroutine(_fill);
            _fill = null;
            IsBound = false;
            gameObject.SetActive(false);
        }

        private void EnsureMaterial()
        {
            if (_material != null || zoneShader == null || image == null)
                return;
            _material = new Material(zoneShader) { name = "PictureZone" };
            image.material = _material;
        }

        private void OnDestroy()
        {
            if (_material == null)
                return;
            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);
        }
    }
}
