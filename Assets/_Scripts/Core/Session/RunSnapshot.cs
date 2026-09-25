using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Зліпок забігу (документ §9): усе, що треба, щоб після згортання чи закриття
    /// застосунку продовжити з того самого місця — поле, лоток, картинка з пікселями,
    /// лічильники й стан генератора випадковості. Програш і переривання — різне: картинку
    /// можна втратити лише програвши.
    ///
    /// Публічні поля й [Serializable] — щоб Meta клав його у файл збереження як є.
    /// Ідентифікатори — назви (картинка, форми фігур), не індекси: бібліотека й каталог ростуть.
    /// </summary>
    [Serializable]
    public sealed class RunSnapshot
    {
        public int Width;
        public int Height;
        public int[] Cells = Array.Empty<int>();
        public string[] TrayShapes = Array.Empty<string>();
        public int[] TrayColors = Array.Empty<int>();
        public string PictureId = string.Empty;
        public int[] FilledPixels = Array.Empty<int>();
        public int Score;
        public int BestChain;
        public int PlacementCount;
        public int LinesCleared;
        public int PureLinesCleared;
        public int PixelsFilled;
        public int PixelsWasted;
        public int PicturesCompleted;
        public int ContinuesUsed;
        public int TraysIssued;
        public int RescuesUsed;
        public string[] Collected = Array.Empty<string>();
        public uint RandomState;

        /// <summary>Порожній зліпок — забігу немає. JsonUtility не вміє null, тому файл тримає саме порожній.</summary>
        public bool IsEmpty => PictureId is null || PictureId.Length == 0 || Cells is null || Cells.Length == 0;

        public void Clear()
        {
            Width = 0;
            Height = 0;
            Cells = Array.Empty<int>();
            TrayShapes = Array.Empty<string>();
            TrayColors = Array.Empty<int>();
            PictureId = string.Empty;
            FilledPixels = Array.Empty<int>();
            Score = BestChain = PlacementCount = LinesCleared = PureLinesCleared = 0;
            PixelsFilled = PixelsWasted = PicturesCompleted = ContinuesUsed = TraysIssued = RescuesUsed = 0;
            Collected = Array.Empty<string>();
            RandomState = 0;
        }
    }
}
