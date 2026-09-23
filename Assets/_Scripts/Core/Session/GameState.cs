namespace InkFlow.Core
{
    /// <summary>Стан партії. Тупика як окремого стану немає: «нікуди поставити» — це програш (§8).</summary>
    public enum GameState : byte
    {
        Playing = 0,
        Lost = 1
    }
}
