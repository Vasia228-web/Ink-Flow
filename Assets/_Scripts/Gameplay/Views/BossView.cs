using System.Collections.Generic;
using InkFlow.Core;
using TMPro;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Клякс над полем: сегменти тіла = прогрес-бар рівня (майстер-док §6).
    /// Сегменти малюються тими самими спрайтами через звичайний spawn на старті рівня
    /// (їх 5-6 і вони живуть усю партію — пул тут не потрібен, це не частий спавн).
    /// </summary>
    public sealed class BossView : MonoBehaviour
    {
        [SerializeField] private GridView gridView;
        [SerializeField] private SpriteRenderer segmentPrefab;
        [SerializeField] private TMP_Text telegraphLabel;

        [Tooltip("На скільки world units тіло боса підняте над верхнім рядом сітки.")]
        [SerializeField] private float heightAboveGrid = 1.2f;

        private readonly List<SpriteRenderer> _segments = new List<SpriteRenderer>(8);
        private BossSession _session;

        public void Bind(BossSession session)
        {
            _session = session;
            gameObject.SetActive(session != null);
            if (session == null)
                return;

            BuildSegments(session);
            Refresh();
        }

        private void BuildSegments(BossSession session)
        {
            foreach (var segment in _segments)
                if (segment != null)
                    Destroy(segment.gameObject);
            _segments.Clear();

            var count = session.Boss.SegmentCount;
            var width = session.Grid.Width;

            for (var i = 0; i < count; i++)
            {
                var segment = Instantiate(segmentPrefab, transform);
                // Рівномірно розподіляємо сегменти по ширині сітки.
                var column = (i + 0.5f) * width / count - 0.5f;
                var basePos = gridView.CellToWorld(new GridPos(0, session.Grid.Height - 1));
                segment.transform.position = basePos + new Vector3(
                    column * (gridView.CellSize + 0.08f), heightAboveGrid, 0f);
                _segments.Add(segment);
            }
        }

        /// <summary>Оновлює кольори сегментів і телеграф наміру.</summary>
        public void Refresh()
        {
            if (_session == null)
                return;

            for (var i = 0; i < _segments.Count && i < _session.Boss.SegmentCount; i++)
            {
                var color = _session.Boss[i];
                _segments[i].color = color == InkColor.None
                    ? new Color(0.12f, 0.12f, 0.16f, 1f) // незафарбоване тіло
                    : gridView.ColorOf(color);
            }

            if (telegraphLabel != null)
                telegraphLabel.text = TelegraphText(_session.Boss.PendingAction);
        }

        /// <summary>Намір оголошується завжди на хід раніше — гравець встигає зреагувати (§18.8).</summary>
        private static string TelegraphText(BossActionType action) => action switch
        {
            BossActionType.Sponge => "Клякс: злиже фарбу",
            BossActionType.SpitRepaint => "Клякс: плюне у краплю",
            BossActionType.SpitBlot => "Клякс: лишить кляксу",
            _ => string.Empty
        };
    }
}
