namespace InkFlow.Gameplay
{
    /// <summary>Об'єкт із пулу: скидає стан на Get/Release замість Instantiate/Destroy.</summary>
    public interface IPoolable
    {
        void OnGetFromPool();
        void OnReleaseToPool();
    }
}
