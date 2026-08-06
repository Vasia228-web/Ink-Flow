namespace InkFlow.Core
{
    /// <summary>
    /// Вісім фарб, якими фарбують зони планет. Як і <see cref="PlanetType"/>,
    /// живе в Core: на нього спираються і Meta (запас у літрах), і Style (кольори),
    /// а вони одне одного не бачать.
    ///
    /// Порядок фіксований — за ним індексується палітра в DesignSystem.
    /// </summary>
    public enum PaintKind
    {
        Ocean = 0,
        Teal = 1,
        Forest = 2,
        Ice = 3,
        Sand = 4,
        Lava = 5,
        Berry = 6,
        Violet = 7
    }

    public static class PaintKinds
    {
        public const int Count = 8;
    }
}
