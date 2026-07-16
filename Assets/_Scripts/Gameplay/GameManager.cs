using InkFlow.Core;
using InkFlow.Levels;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Точка входу сцени: вантажить рівень через LevelLoader і стартує сесію.
    /// Всі залежності — прямі serialized-референси (DI-фреймворк свідомо не використовуємо).
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelLoader levelLoader;
        [SerializeField] private GridController gridController;

        [Tooltip("Addressables-адреса рівня, що запускається (група Levels).")]
        [SerializeField] private string levelAddress = "Levels/Level_001";

        private void Start() =>
            levelLoader.Load(levelAddress, gridController.StartSession, Debug.LogError);

        private void OnDestroy() => GameEvents.Clear();
    }
}
