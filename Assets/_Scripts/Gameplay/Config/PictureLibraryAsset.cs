using System.Collections.Generic;
using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// PictureLibrary.asset — усі картинки гри як текстові асети з Assets/_Pictures
    /// (документ §7, формат — docs/pictures-format.md). Заповнює редактор:
    /// Ink Flow → Setup → Refresh Picture Library; сам файл картинки правиться руками
    /// або скриптом Tools/pictures/author.py.
    /// </summary>
    [CreateAssetMenu(fileName = "PictureLibrary", menuName = "Ink Flow/Picture Library")]
    public sealed class PictureLibraryAsset : ScriptableObject
    {
        [Tooltip("Тексти картинок. Порядок не має значення: бібліотека сортує за id.")]
        [SerializeField] private TextAsset[] pictures = System.Array.Empty<TextAsset>();

        private PictureLibrary? _cached;

        public int Count => pictures.Length;

        /// <summary>
        /// Бібліотека для Core. Кешується: асет не змінюється під час гри. Зламаний файл
        /// не валить гру — пропускається з помилкою в консоль, бо гравцеві потрібна
        /// бібліотека, а не стектрейс.
        /// </summary>
        public PictureLibrary ToLibrary()
        {
            if (_cached != null)
                return _cached;

            var texts = new List<string>(pictures.Length);
            var ids = new List<string>(pictures.Length);
            foreach (var asset in pictures)
            {
                if (asset == null)
                    continue;
                try
                {
                    PixelPicture.Parse(asset.text, asset.name);
                    texts.Add(asset.text);
                    ids.Add(asset.name);
                }
                catch (System.ArgumentException e)
                {
                    Debug.LogError($"[InkFlow] Картинку «{asset.name}» пропущено: {e.Message}", asset);
                }
            }

            if (texts.Count == 0)
            {
                Debug.LogError("[InkFlow] PictureLibrary.asset порожній — гра йде із запасним серцем. " +
                               "Ink Flow → Setup → Refresh Picture Library.", this);
                return _cached = PictureLibrary.Fallback;
            }

            return _cached = PictureLibrary.Parse(texts, ids);
        }

        private void OnValidate() => _cached = null;
    }
}
