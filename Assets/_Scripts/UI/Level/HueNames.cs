using InkFlow.Core;

namespace InkFlow.UI
{
    /// <summary>Назви відтінків для написів (прототип v3, MIXES). Core назв не знає.</summary>
    public static class HueNames
    {
        public static string Of(Hue hue) => hue switch
        {
            Hue.Blue => "СИНІЙ",
            Hue.Red => "ЧЕРВОНИЙ",
            Hue.Yellow => "ЖОВТИЙ",
            Hue.Green => "ЗЕЛЕНИЙ",
            Hue.Orange => "ПОМАРАНЧЕВИЙ",
            Hue.Purple => "ФІОЛЕТОВИЙ",
            Hue.Brown => "КОРИЧНЕВИЙ",
            _ => ""
        };
    }
}
