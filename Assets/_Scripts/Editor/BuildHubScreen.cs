using InkFlow.Core;
using InkFlow.Style;
using InkFlow.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static InkFlow.Editor.UiBuilder;

namespace InkFlow.Editor
{
    /// <summary>
    /// Збирає головний екран U1Glaze (S01Home_01/02, S12SmallScreen_01) фабриками <see cref="U1"/>.
    /// Меню: Ink Flow → Setup → Build Hub Screen.
    ///
    /// Розкладка — групи, а не абсолютні позиції: SafeArea → стовпчик (бічні поля 16, низ 8):
    /// шапка 56 → лого 70 → підпис → заставка (гнучка: стискається першою на SE і росте на 20:9) →
    /// картка «Нескінченний» → навігація 76. Числа — px макета 402×874 через <see cref="U1.Px"/>.
    /// Картки «Рівні · Скоро» немає (у запасі, Reserve/LevelsMode), «+» на капсулі — теж (Reserve/OilShop).
    /// </summary>
    public static class BuildHubScreen
    {
        private const string ScenePath = "Assets/Scenes/Hub.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        /// <summary>Сторона мініатюри на картці «Нескінченний» (мала рамка рідкості), px макета.</summary>
        private const float ThumbnailSidePx = 62f;

        [MenuItem("Ink Flow/Setup/Build Hub Screen")]
        public static void Build()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            // Поки збираємо — жодних відкладених перечитувань стилю: ApplyScaler() і Apply() кличемо самі.
            StyleRefresh.Suspended = true;
            try { BuildScene(); }
            finally { StyleRefresh.Suspended = false; }
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Асети — строго після NewScene (див. коментар у InkFlowBootstrap).
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            if (design == null || cosmic == null || U1GlazeSprites.Load("logo_spacepaint") == null)
            {
                Debug.LogError("[InkFlow] Хаб НЕ зібрано: потрібні DesignSystem.asset, CosmicBackground.prefab і спрайти U1Glaze (Build UI Kit).");
                return;
            }

            CreateCamera(design);
            var canvasGo = new GameObject("UI Root");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            PrefabUtility.InstantiatePrefab(cosmic, canvasGo.transform);

            var safe = Child(canvasGo, "SafeArea");
            Stretch(safe);
            var safeBinder = safe.AddComponent<SafeAreaBinder>();
            var navigation = safe.AddComponent<NavigationStack>();
            var uiRoot = canvasGo.AddComponent<UIRoot>();
            Wire(uiRoot, ("canvas", canvas), ("scaler", scaler), ("navigation", navigation), ("safeArea", safeBinder));

            var screenGo = Child(safe, "HubScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<HubScreen>();

            var column = Child(screenGo, "Column");
            Stretch(column);
            var layout = column.AddComponent<VerticalLayoutGroup>();
            var gutter = (int)design.SideGutter;
            layout.padding = new RectOffset(gutter, gutter, 0, (int)U1.Px(8f));
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            U1.MainHeader(column, design, out var avatar, out var nick, out var profile, out var currency, out var gear);
            Gap(column, "GapLogo", 16f);
            BuildLogo(column, design);
            BuildHero(column);
            var card = BuildEndlessCard(column, design, out var picturesBadge, out var recordBadge, out var play,
                out var thumbnail, out var thumbnailFrame, out var thumbnailPicture);
            _ = card;
            Gap(column, "GapNav", 12f);
            var navBar = U1.NavBar(column, "NavBar", design, NavBar.Play);

            Wire(screen,
                ("design", design), ("avatar", avatar), ("playerName", nick), ("profileButton", profile),
                ("currency", currency), ("settingsButton", gear),
                ("picturesBadge", picturesBadge), ("recordBadge", recordBadge), ("playButton", play),
                ("thumbnail", thumbnail), ("thumbnailFrame", thumbnailFrame), ("thumbnailPicture", thumbnailPicture),
                ("navBar", navBar));
            var avatars = new Object[8];
            for (var i = 0; i < avatars.Length; i++)
                avatars[i] = U1.S($"avatar_{i + 1}");
            WireArray(screen, "avatarSprites", avatars);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            // Стилі — тут, синхронно й перед збереженням: відкладений OnValidate у файл уже не потрапив би.
            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);
            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Хаб зібрано: {ScenePath} (U1Glaze, коефіцієнт {DesignSystem.MockupToReference:0.000}).");
        }

        /// <summary>Порожній проміжок фіксованої висоти в стовпчику (px макета).</summary>
        private static void Gap(GameObject column, string name, float heightPx) => U1.Size(Child(column, name), 0f, heightPx);

        // Лого SpacePaint 296×70 (спрайт, тінь запечена) і підпис ui-label ink-muted під ним.
        private static void BuildLogo(GameObject column, DesignSystem design)
        {
            var logoGo = Child(column, "Logo");
            var logo = logoGo.AddComponent<Image>();
            logo.sprite = U1.S("logo_spacepaint");
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            U1.Size(logoGo, 0f, 70f);
            Gap(column, "GapTagline", 2f);
            var tagline = U1.Text(column, "Tagline", "Фарбуй галактику", design, U1.Role.Label, design.InkMuted);
            tagline.textWrappingMode = TextWrappingModes.NoWrap;
            tagline.raycastTarget = false;
        }

        // Заставка 400×240: забирає всю висоту, що лишилась між підписом і карткою; картинка — за пропорціями
        // по центру блоку (preserveAspect). На SE блок найменший, на 20:9 — найбільший.
        private static void BuildHero(GameObject column)
        {
            var heroGo = Child(column, "Hero");
            var hero = heroGo.AddComponent<Image>();
            hero.sprite = U1.S("home_hero");
            hero.preserveAspect = true;
            hero.raycastTarget = false;
            // preferred = min = 64 і flexible 1: група дає заставці ВЕСЬ залишок висоти, і жоден інший блок не
            // стискається. (З preferred 240 на SE стовпчик не вміщав preferred-висоти, і група стискала всіх
            // дітей, у кого min < preferred, — зокрема написи з min 0: підпис і текст картки переповнювались.)
            var size = heroGo.AddComponent<LayoutElement>();
            size.preferredHeight = U1.Px(64f);
            size.minHeight = U1.Px(64f);
            size.flexibleHeight = 1f;
        }

        // Картка «Нескінченний» (S01Home_01): panel_card з тінню, поля 18/20, проміжок 16. Верхній ряд — бірюзова
        // плитка 60 з ∞, назва ui-h2 і підпис ui-body на два рядки, мініатюра в малій рамці рідкості 62;
        // далі бейджі «N КАРТИНОК» (бурштин) і «РЕКОРД N» (рожевий); унизу коралова «Грати» на всю ширину —
        // єдина коралова на екрані.
        private static GameObject BuildEndlessCard(GameObject column, DesignSystem design,
            out TMP_Text picturesBadge, out TMP_Text recordBadge, out CandyButton play,
            out RectTransform thumbnail, out RarityFrameView thumbnailFrame, out PictureView thumbnailPicture)
        {
            var card = U1.Panel(column, "EndlessCard", "panel_card");
            var col = card.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)U1.Px(18f), (int)U1.Px(18f), (int)U1.Px(20f), (int)U1.Px(20f));
            col.spacing = U1.Px(16f);
            col.childAlignment = TextAnchor.UpperLeft;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            var top = Child(card, "Top");
            var row = top.AddComponent<HorizontalLayoutGroup>();
            row.spacing = U1.Px(16f);
            row.childAlignment = TextAnchor.UpperLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            BuildModeTile(top, design);

            var texts = Child(top, "Texts");
            var textCol = texts.AddComponent<VerticalLayoutGroup>();
            textCol.spacing = U1.Px(2f);
            textCol.childAlignment = TextAnchor.UpperLeft;
            textCol.childControlWidth = textCol.childControlHeight = true;
            textCol.childForceExpandWidth = true;
            textCol.childForceExpandHeight = false;
            var textsSize = texts.AddComponent<LayoutElement>();
            textsSize.preferredWidth = 0f;
            textsSize.flexibleWidth = 1f;
            var title = U1.Text(texts, "Title", "Нескінченний", design, U1.Role.H2, design.InkText, TextAlignmentOptions.Left);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            // На SE колонка тексту ≈ 150 px — назва на межі; трохи стиснути краще, ніж обрізати.
            title.enableAutoSizing = true;
            title.fontSizeMax = design.UiH2;
            title.fontSizeMin = design.UiH2 * 0.85f;
            title.raycastTarget = false;
            var subtitle = U1.Text(texts, "Subtitle", "Малюй картинки без кінця", design, U1.Role.Body, design.InkMuted,
                TextAlignmentOptions.Left);
            subtitle.raycastTarget = false;

            var thumbGo = Child(top, "Thumbnail");
            U1.Size(thumbGo, ThumbnailSidePx, ThumbnailSidePx);
            thumbnail = thumbGo.GetComponent<RectTransform>();
            thumbnailFrame = U1.RarityFrame(thumbGo, "Frame", design, big: false, sidePx: ThumbnailSidePx, Rarity.Rare);
            var pictureGo = thumbnailFrame.transform.Find("Picture").gameObject;
            thumbnailPicture = PictureViewBuilder.MakeFramedPicture(pictureGo, design, U1.Px(ThumbnailSidePx - 6f));

            var badges = Child(card, "Badges");
            var badgeRow = badges.AddComponent<HorizontalLayoutGroup>();
            badgeRow.spacing = U1.Px(8f);
            badgeRow.childAlignment = TextAnchor.MiddleLeft;
            badgeRow.childControlWidth = badgeRow.childControlHeight = true;
            badgeRow.childForceExpandWidth = badgeRow.childForceExpandHeight = false;
            U1.Size(badges, 0f, 26f);
            picturesBadge = U1.Badge(badges, "Pictures", "14 картинок", design, "badge_amber", design.AmberInk);
            recordBadge = U1.Badge(badges, "Record", "Рекорд 7 980", design, "badge_pink", design.PinkInk);

            play = U1.Button(card, "Play", "Грати", design, CandyButton.Tone.Coral, small: false, icon: U1.S("icon_play"));
            U1.Size(play.gameObject, 0f, 56f);
            return card;
        }

        // Плитка режиму: бірюзова цукерка 60 (button_teal + гало), ∞ кольору teal-ink. Не кнопка — дія в «Грати».
        private static void BuildModeTile(GameObject parent, DesignSystem design)
        {
            var tile = Child(parent, "ModeTile");
            U1.Size(tile, 60f, 60f);
            var glowGo = Child(tile, "Glow");
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = U1.S("button_glow_teal");
            glow.type = Image.Type.Sliced;
            glow.color = new Color(1f, 1f, 1f, design.ButtonGlowAlpha);
            glow.raycastTarget = false;
            Stretch(glowGo, -U1.Px(16f));
            var bodyGo = Child(tile, "Body");
            var body = bodyGo.AddComponent<Image>();
            body.sprite = U1.S("button_teal");
            body.type = Image.Type.Sliced;
            body.raycastTarget = false;
            Stretch(bodyGo);
            var iconGo = Child(tile, "Icon");
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = U1.S("icon_infinity");
            icon.color = design.TealInk;
            icon.raycastTarget = false;
            Place(icon, Vector2.zero, new Vector2(U1.Px(34f), U1.Px(34f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }
    }
}
