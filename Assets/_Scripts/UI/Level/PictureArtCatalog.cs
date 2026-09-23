using System;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Маски зон картинок — те, що редактор згенерував із креслень
    /// <see cref="InkFlow.Core.PictureCatalogData"/>. Один спрайт на зону, усі спрайти
    /// картинки одного розміру: в'юха кладе їх стосом в один прямокутник.
    /// Меню: Ink Flow → Setup → Generate Picture Art.
    /// </summary>
    [CreateAssetMenu(menuName = "Ink Flow/Picture Art", fileName = "PictureArt")]
    public sealed class PictureArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class PictureArt
        {
            public string id = "";
            public Sprite[] zones = Array.Empty<Sprite>();

            /// <summary>Центр зони в UV спрайта — звідси розтікається заливка.</summary>
            public Vector2[] origins = Array.Empty<Vector2>();

            /// <summary>Радіус зони від центру в UV — нормує фронт заливки.</summary>
            public float[] extents = Array.Empty<float>();
        }

        [SerializeField] private PictureArt[] pictures = Array.Empty<PictureArt>();

        public PictureArt[] Pictures
        {
            get => pictures;
            set => pictures = value;
        }

        public PictureArt? Find(string id)
        {
            for (var i = 0; i < pictures.Length; i++)
                if (pictures[i].id == id)
                    return pictures[i];
            return null;
        }
    }
}
