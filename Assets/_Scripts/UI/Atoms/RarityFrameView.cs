using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рамка рідкості U1Glaze навколо картинки: 6 спрайтів 9-slice (великі ≥ 70 px і малі),
    /// капсула міток 1–6 під нижнім краєм, гало під рамкою для Епічної+, іскри в кутах
    /// (Легендарна — верхні, Космічна — лівий-верх і правий-низ) і перелив Космічної —
    /// шейдер <c>InkFlow/UI Sheen</c> через <c>_Time</c>, без C# щокадру.
    ///
    /// Сама картинка (атлас чи полотно W4) — дочірній об'єкт, який кладе збирач у виріз;
    /// рамка лише перемикає спрайти при зміні рідкості.
    /// </summary>
    public sealed class RarityFrameView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image frame;
        [SerializeField] private Image? pips;
        [SerializeField] private Image? glow;
        [SerializeField] private Image[] sparkles = System.Array.Empty<Image>();
        [SerializeField] private Shader? sheenShader;

        [Header("Спрайти (ставить збирач): 6 рамок, 6 малих, 6 міток, 3 гало (епічна, легендарна, космічна)")]
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] framesSmall = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] pipSprites = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] glows = System.Array.Empty<Sprite>();

        [SerializeField] private bool big = true;
        [SerializeField] private Rarity rarity = Rarity.Common;

        // Один матеріал переливу на всі космічні рамки: окремий екземпляр на рамку — окремий виклик малювання на кожну
        // (сітка колекції з фільтром «Космічна» — до 18). Живе весь сеанс; період — з дизайн-системи при застосуванні.
        private static Material? _sheen;

        public Rarity Rarity => rarity;
        public bool IsBig => big;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        public void Configure(bool isBig, Sprite[] frameSprites, Sprite[] smallSprites, Sprite[] pipSet, Sprite[] glowSet, Shader? sheen)
        {
            big = isBig;
            frames = frameSprites;
            framesSmall = smallSprites;
            pipSprites = pipSet;
            glows = glowSet;
            sheenShader = sheen;
            Apply();
        }

        public void SetRarity(Rarity value)
        {
            rarity = value;
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;
            var index = Mathf.Clamp((int)rarity, 0, 5);
            var set = big ? frames : framesSmall;

            if (frame != null)
            {
                if (index < set.Length) frame.sprite = set[index];
                frame.color = Color.white;
                frame.material = rarity == Rarity.Cosmic ? Sheen() : null;
            }

            if (pips != null)
            {
                if (index < pipSprites.Length) pips.sprite = pipSprites[index];
                pips.SetNativeSize();
                // Під малою рамкою мітки менші (токен): розмір похідний — тож localScale, не sizeDelta.
                pips.rectTransform.localScale = Vector3.one * (big ? 1f : design.RarityPipsSmallScale);
                pips.color = Color.white;
            }

            if (glow != null)
            {
                var lit = rarity >= Rarity.Epic && glows.Length >= 3;
                glow.gameObject.SetActive(lit);
                if (lit)
                {
                    glow.sprite = glows[index - (int)Rarity.Epic];
                    glow.color = new Color(1f, 1f, 1f, design.FrameGlowAlpha);
                }
            }

            // Іскри: Легендарна — два верхні кути (бурштин), Космічна — лівий-верх і правий-низ (рожева).
            for (var i = 0; i < sparkles.Length; i++)
            {
                var sparkle = sparkles[i];
                if (sparkle == null) continue;
                var show = rarity == Rarity.Legendary ? i < 2 : rarity == Rarity.Cosmic ? i >= 2 : false;
                sparkle.gameObject.SetActive(show);
                if (show)
                    sparkle.color = rarity == Rarity.Legendary ? design.Amber300 : design.Pink300;
            }
        }

        private Material? Sheen()
        {
            if (sheenShader == null)
                return null;
            if (_sheen == null || _sheen.shader != sheenShader)
                _sheen = new Material(sheenShader) { name = "CosmicSheen", hideFlags = HideFlags.DontSave };
            // Період — при застосуванні стилю, не щокадру: далі крутить сам шейдер.
            _sheen.SetFloat("_Period", design != null ? design.CosmicSheenPeriod : 6f);
            return _sheen;
        }
    }
}
