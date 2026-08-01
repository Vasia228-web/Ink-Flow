namespace InkFlow.Core
{
    /// <summary>
    /// Джерело випадковості для Core. UnityEngine.Random заборонений у логіці (§6) —
    /// він не гарантує однакову послідовність між платформами й версіями редактора,
    /// а від цього залежить обіцянка «те саме рішення = той самий результат».
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Ціле в діапазоні [0, maxExclusive).</summary>
        int Next(int maxExclusive);

        /// <summary>Поточний стан — щоб зберегти/відтворити партію (RunReplay).</summary>
        uint State { get; }
    }
}
