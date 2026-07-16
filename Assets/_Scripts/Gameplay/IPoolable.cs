namespace InkFlow.Gameplay
{
    /// <summary>Об'єкт, що живе в пулі: скидає стан на Get/Release замість Instantiate/Destroy.</summary>
    public interface IPoolable
    {
        void OnGetFromPool();
        void OnReleaseToPool();
    }
}
