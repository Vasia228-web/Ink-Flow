namespace InkFlow.Core
{
    /// <summary>
    /// Дев'ять типів планет галактики. Живуть у Core з тієї ж причини, що й
    /// <see cref="InkColor"/>: тип — це ігрове поняття, на яке спираються і Meta
    /// (прогрес гравця), і Style (палітра поверхні), а вони одне одного не бачать.
    ///
    /// Порядок фіксований — за ним індексуються палітри в DesignSystem.
    /// </summary>
    public enum PlanetType
    {
        Ocean = 0,
        Rocky = 1,
        Ice = 2,
        Earth = 3,
        Rings = 4,
        Gas = 5,
        Volcano = 6,
        Desert = 7,
        Pearl = 8
    }

    public static class PlanetTypes
    {
        /// <summary>Скільки типів. Палітра в DesignSystem мусить мати рівно стільки записів.</summary>
        public const int Count = 9;
    }

    /// <summary>Стан планети для гравця, що дивиться на галактику.</summary>
    public enum PlanetState
    {
        /// <summary>Темний силует із замком: попередня планета ще не завершена.</summary>
        Locked = 0,

        /// <summary>Частина зон пофарбована, решта — матова.</summary>
        Current = 1,

        /// <summary>Усі зони пофарбовані: атмосфера, світіння, галочка.</summary>
        Done = 2
    }
}
