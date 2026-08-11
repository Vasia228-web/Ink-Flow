namespace InkFlow.Meta
{
    /// <summary>
    /// Куди пишеться збереження. Інтерфейс живе ОКРЕМО від реалізації, бо сама
    /// реалізація тягне UnityEngine (`persistentDataPath`, `JsonUtility`), а
    /// headless-раннер компілює Meta без нього — і все, що приймає сховище,
    /// інакше стало б неперевірюваним.
    /// </summary>
    public interface ISaveStorage
    {
        bool Exists { get; }
        SaveFile Load();
        void Save(SaveFile save);
        void Delete();
    }
}
