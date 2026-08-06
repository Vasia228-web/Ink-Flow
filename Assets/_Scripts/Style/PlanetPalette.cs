using System;
using UnityEngine;

namespace InkFlow.Style
{
    /// <summary>
    /// Палітра поверхні планети: три кольори й швидкість обертання.
    ///
    /// Шейдер InkFlow/Planet будує з них усе інше — материки, смуги, кратери,
    /// відблиск і ободок атмосфери. Тому змінити вигляд планети можна тут,
    /// не чіпаючи ані шейдера, ані сцени.
    /// </summary>
    [Serializable]
    public struct PlanetPalette
    {
        [Tooltip("Основа: океан, порода, газ.")]
        [SerializeField] private Color baseColor;

        [Tooltip("Деталь: материки, кратери, лавові жили, смуги.")]
        [SerializeField] private Color land;

        [Tooltip("Атмосфера: ободок на освітленому краю, серпанок і дуга прогресу.")]
        [SerializeField] private Color atmosphere;

        [Tooltip("Скільки секунд на повний оберт. Менше — швидше.")]
        [SerializeField] private float secondsPerTurn;

        public PlanetPalette(Color baseColor, Color land, Color atmosphere, float secondsPerTurn)
        {
            this.baseColor = baseColor;
            this.land = land;
            this.atmosphere = atmosphere;
            this.secondsPerTurn = secondsPerTurn;
        }

        public Color Base => baseColor;
        public Color Land => land;
        public Color Atmosphere => atmosphere;

        /// <summary>Нуль означав би зупинену планету — підстраховуємось.</summary>
        public float SecondsPerTurn => secondsPerTurn > 0.01f ? secondsPerTurn : 30f;
    }
}
