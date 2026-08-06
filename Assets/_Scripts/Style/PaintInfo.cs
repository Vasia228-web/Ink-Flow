using System;
using UnityEngine;

namespace InkFlow.Style
{
    /// <summary>
    /// Фарба в палітрі планет: колір і назва.
    ///
    /// Назва лежить поруч із кольором навмисно — інакше при додаванні фарби
    /// довелось би правити два списки в різних місцях, і рано чи пізно вони
    /// розійшлися б на один елемент.
    /// </summary>
    [Serializable]
    public struct PaintInfo
    {
        [SerializeField] private Color color;
        [SerializeField] private string name;

        public PaintInfo(Color color, string name)
        {
            this.color = color;
            this.name = name;
        }

        public Color Color => color;
        public string Name => name ?? string.Empty;
    }
}
