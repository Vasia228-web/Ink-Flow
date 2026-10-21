using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рядок таблиці «Ліга» (U1Glaze S08): місце (медаль для топ-3 або число), аватар-спрайт набору (інкогніто — маска),
    /// нік з «…», значення без переносу праворуч (автостиск до 70 %). Перше місце — теплий відтінок рядка, свій рядок —
    /// бірюзовий. Рядок перепризначається при скролі, тож Show кличеться лише коли рядок в'їхав у вікно.
    /// </summary>
    public sealed class RankingRow : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Button button;
        [SerializeField] private Image highlight;
        [SerializeField] private Image medal;
        [SerializeField] private TMP_Text place;
        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text nick;
        [SerializeField] private TMP_Text value;
        [SerializeField] private Image divider;

        [Header("Спрайти (ставить збирач)")]
        [SerializeField] private Sprite[] medals = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] avatars = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite? incognito;

        private RankPlayer? _player;
        private System.Action<RankPlayer>? _onTap;

        public RankPlayer? Player => _player;

        /// <summary>Нік, як його видно в рядку (тестам).</summary>
        public string NickText => nick != null ? nick.text : string.Empty;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(() => { if (_player != null) _onTap?.Invoke(_player); });
        }

        public void Bind(System.Action<RankPlayer> onTap) => _onTap = onTap;

        /// <summary>Спрайт аватара гравця: інкогніто — маска, інакше avatar_N за номером (поза набором — перший).</summary>
        public static Sprite? AvatarOf(RankPlayer player, Sprite[] set, Sprite? incognitoSprite)
        {
            if (player.Incognito && !player.IsYou)
                return incognitoSprite;
            if (set.Length == 0)
                return null;
            var id = player.AvatarId;
            return set[id >= 0 && id < set.Length ? id : 0];
        }

        public void Show(RankPlayer player, int position, bool last)
        {
            _player = player;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (design == null)
                return;

            var medalled = position >= 1 && position <= medals.Length;
            if (medal != null)
            {
                medal.gameObject.SetActive(medalled);
                if (medalled)
                    medal.sprite = medals[position - 1];
            }
            if (place != null)
            {
                place.text = position > 0 ? position.ToString() : "—";
                place.color = medalled ? design.MedalInk : design.InkMuted;
            }

            if (avatar != null)
            {
                avatar.sprite = AvatarOf(player, avatars, incognito);
                avatar.enabled = avatar.sprite != null;
            }
            if (nick != null)
                nick.text = player.Incognito && !player.IsYou ? "Гравець-інкогніто" : player.IsYou ? $"Ти · {player.Nick}" : player.Nick;
            if (value != null)
                value.text = ScoreFormat.Full(player.Value);

            if (highlight != null)
            {
                var lit = player.IsYou || position == 1;
                highlight.gameObject.SetActive(lit);
                if (lit)
                    highlight.color = player.IsYou
                        ? DesignSystem.WithAlpha(design.Teal, design.RankRowYouAlpha)
                        : DesignSystem.WithAlpha(design.Amber, design.RankRowFirstAlpha);
            }
            if (divider != null)
                divider.gameObject.SetActive(!last);
        }

        public void Release()
        {
            _player = null;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
