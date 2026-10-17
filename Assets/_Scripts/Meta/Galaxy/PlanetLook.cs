using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>Розкладка планети на екрані (U1Glaze S04 / S05).</summary>
    public enum PlanetLook
    {
        /// <summary>«Сфера»: куля, що крутиться, комірки на ній, кільце міток навколо (до порогу включно).</summary>
        Sphere,

        /// <summary>«Горизонт»: велика планета внизу, комірки рядами по дугах, мітки рядком під назвою.</summary>
        Horizon,
    }

    /// <summary>Яку розкладку брати: до порогу комірок — «Сфера», більше — «Горизонт» (поріг — повзунок дизайн-системи).</summary>
    public static class PlanetLooks
    {
        public static PlanetLook For(int slots, int sphereMaxSlots) =>
            slots <= sphereMaxSlots ? PlanetLook.Sphere : PlanetLook.Horizon;

        /// <summary>
        /// Seed розкладки «Сфери»: 1 + тип планети (рішення Сесії 1). Стабільний між запусками й версіями, бо у файлі
        /// планета — назва типу, а слот — номер; хеш рядка не годиться (у .NET він різний між процесами).
        /// </summary>
        public static int SeedOf(PlanetType type) => 1 + (int)type;
    }
}
