using System;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Meta;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// GalaxyConfig.asset — розкладка галактики (майстер-док §12): планети, кількість слотів
    /// у кожній, прикраси й імена циклів. Числа живуть тут, не в коді: автор крутить слоти
    /// повзунками. Галактик нескінченно — та сама розкладка повторюється як II, III…
    /// Дефолти — <see cref="GalaxyLayout.Default"/>, тож порожній асет дає ту саму гру.
    /// </summary>
    [CreateAssetMenu(fileName = "GalaxyConfig", menuName = "Ink Flow/Galaxy Config")]
    public sealed class GalaxyConfig : ScriptableObject
    {
        [Serializable]
        public sealed class PlanetEntry
        {
            public PlanetType type;
            public string name = string.Empty;
            [Tooltip("Скільки картинок треба поставити, щоб планета ожила.")]
            [Range(1, 24)] public int slots = 4;
            public bool hasMoons;
            public bool hasRing;
            public bool isFinale;
        }

        [Tooltip("Планети по черзі відкриття; тип кожної — унікальний (він же ідентифікатор у збереженні).")]
        [SerializeField] private List<PlanetEntry> planets = DefaultPlanets();

        [Tooltip("Імена циклів галактик по колу: I — перше, II — друге, …")]
        [SerializeField] private string[] galaxyNames = DefaultNames();

        public GalaxyLayout ToLayout()
        {
            var list = new List<PlanetLayout>(planets.Count);
            foreach (var entry in planets)
            {
                if (entry is null)
                    continue;
                list.Add(new PlanetLayout(entry.type,
                    entry.name is null || entry.name.Length == 0 ? entry.type.ToString() : entry.name,
                    Mathf.Max(1, entry.slots), entry.hasMoons, entry.hasRing, entry.isFinale));
            }

            try
            {
                return new GalaxyLayout(list, galaxyNames is null || galaxyNames.Length == 0 ? DefaultNames() : galaxyNames);
            }
            catch (ArgumentException e)
            {
                // Зіпсований асет (два записи одного типу, порожній список) не має ронити гру —
                // голосно в консоль і дефолтна розкладка.
                Debug.LogError($"[InkFlow] GalaxyConfig зіпсований: {e.Message}. Беру розкладку за замовчуванням.");
                return GalaxyLayout.Default;
            }
        }

        private static List<PlanetEntry> DefaultPlanets()
        {
            var list = new List<PlanetEntry>();
            foreach (var planet in GalaxyLayout.Default.Planets)
                list.Add(new PlanetEntry
                {
                    type = planet.Type,
                    name = planet.Name,
                    slots = planet.Slots,
                    hasMoons = planet.HasMoons,
                    hasRing = planet.HasRing,
                    isFinale = planet.IsFinale
                });
            return list;
        }

        private static string[] DefaultNames()
        {
            var names = new string[GalaxyLayout.Default.GalaxyNames.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = GalaxyLayout.Default.GalaxyNames[i];
            return names;
        }
    }
}
