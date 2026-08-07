using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Style
{
    /// <summary>
    /// Міст між кольором моделі (<see cref="Rgb"/> у Core, без рушія) і кольором
    /// рушія. Живе в Style, бо саме тут вперше сходяться обидві сторони.
    /// </summary>
    public static class RgbExtensions
    {
        public static Color ToColor(this Rgb rgb) => new Color(rgb.R, rgb.G, rgb.B, rgb.A);
    }
}
