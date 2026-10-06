using System.Collections.Generic;
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
    /// Збирає екран налаштувань (майстер-док §15): картка перемикачів звуку, музики й вібрації,
    /// три дії («Додому», «Заново», «Колекція»), картка «Інші налаштування» (приховати профіль,
    /// мова — лише з ≥ 2 локалями, політика, підтримка, версія). Меню: Ink Flow → Setup → Build Settings Screen.
    ///
    /// Кожне число — px макета × K, K = 1080/390. Усе нижче шапки — у вертикальному скролі: на iPhone SE
    /// (694 px макета заввишки) усе влазить і так, але зайвий рядок мови чи довший напис не мають
    /// обрізатись мовчки.
    /// </summary>
    public static class BuildSettingsScreen
    {
        private const string ScenePath = "Assets/Scenes/Settings.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        private static readonly float SideMargin = M(18f);
        private static readonly float RowHeight = M(48f);
        private static readonly float CardPadding = M(6f);
        private static readonly float CardRadius = M(22f);

        [MenuItem("Ink Flow/Setup/Build Settings Screen")]
        public static void Build()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            StyleRefresh.Suspended = true;
            try { BuildScene(); }
            finally { StyleRefresh.Suspended = false; }
        }

        private static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath) == null)
            {
                Debug.LogError($"[InkFlow] Немає {DesignSystemPath}. Спершу: Ink Flow → Setup → Build UI Kit.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);

            var rounded = LoadSprite("rounded-rect");
            var outline = LoadSprite("rounded-rect-outline");
            var circle = LoadSprite("circle-soft");
            var circleOutline = LoadSprite("circle-outline");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (circleOutline == null) missing.Add($"{SpriteFolder}/circle-outline.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Налаштування НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
                return;
            }

            CreateCamera(design!);

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
            Wire(uiRoot, ("canvas", canvas), ("scaler", scaler),
                ("navigation", navigation), ("safeArea", safeBinder));

            var screenGo = Child(safe, "SettingsScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<SettingsScreen>();

            var headerHeight = BuildHeader(screenGo, design!, font, circle!, circleOutline!, out var backButton, out var title);
            VerticalScroll(screenGo, "Body", headerHeight + M(12f), out var content);

            var y = 0f;
            var soundCaption = Caption(content, "SoundCaption", "ЗВУК І ВІБРАЦІЯ", design!, font, ref y);
            var soundCard = Card(content, "SoundCard", rounded!, outline!, design!, RowHeight * 3f + CardPadding * 2f, ref y);
            var soundToggle = ToggleRow(soundCard, "Sound", "Звук", 0, design!, font, rounded!, circle!);
            var musicToggle = ToggleRow(soundCard, "Music", "Музика", 1, design!, font, rounded!, circle!);
            var vibrationToggle = ToggleRow(soundCard, "Vibration", "Вібрація", 2, design!, font, rounded!, circle!);
            y -= M(14f);

            var homeButton = ActionButton(content, "Home", "Додому", design!, font, rounded!, outline!, true, ref y, out var homeLabel);
            var restartButton = ActionButton(content, "Restart", "Заново", design!, font, rounded!, outline!, false, ref y, out var restartLabel);
            var collectionButton = ActionButton(content, "Collection", "Колекція", design!, font, rounded!, outline!, false, ref y, out var collectionLabel);
            y -= M(4f);

            var othersCaption = Caption(content, "OthersCaption", "ІНШІ НАЛАШТУВАННЯ", design!, font, ref y);
            var othersCard = Card(content, "OthersCard", rounded!, outline!, design!, RowHeight * 4f + M(36f) + CardPadding * 2f, ref y);
            var hideProfileToggle = ToggleRow(othersCard, "HideProfile", "Приховати профіль у рейтингах", 0, design!, font, rounded!, circle!);
            var languageButton = LinkRow(othersCard, "Language", "Мова", "Українська", 1, design!, font, out var languageLabel, out var languageValue);
            var privacyButton = LinkRow(othersCard, "Privacy", "Політика приватності", "скоро", 2, design!, font, out var privacyLabel, out var privacyHint);
            var supportButton = LinkRow(othersCard, "Support", "Написати в підтримку", "скоро", 3, design!, font, out var supportLabel, out var supportHint);
            var version = Label(othersCard, "Version", "Ink Flow · версія 1.0", design!, font,
                design!.FontSizeCaption, design.TextDim, TextAlignmentOptions.Center);
            var versionRect = version.GetComponent<RectTransform>();
            versionRect.anchorMin = new Vector2(0f, 1f);
            versionRect.anchorMax = new Vector2(1f, 1f);
            versionRect.pivot = new Vector2(0.5f, 1f);
            versionRect.offsetMin = new Vector2(0f, -(CardPadding + RowHeight * 4f + M(36f)));
            versionRect.offsetMax = new Vector2(0f, -(CardPadding + RowHeight * 4f));
            // Рядок мови в префабі схований: екран вмикає його лише з двома й більше локалями.
            languageButton.gameObject.SetActive(false);

            content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, -y + M(24f));

            Wire(screen,
                ("design", design!), ("backButton", backButton), ("title", title),
                ("soundCaption", soundCaption), ("soundToggle", soundToggle), ("musicToggle", musicToggle),
                ("vibrationToggle", vibrationToggle),
                ("homeButton", homeButton), ("homeLabel", homeLabel),
                ("restartButton", restartButton), ("restartLabel", restartLabel),
                ("collectionButton", collectionButton), ("collectionLabel", collectionLabel),
                ("content", content.GetComponent<RectTransform>()), ("othersCard", othersCard.GetComponent<RectTransform>()),
                ("othersCaption", othersCaption), ("hideProfileToggle", hideProfileToggle),
                ("languageButton", languageButton), ("languageLabel", languageLabel), ("languageValue", languageValue),
                ("privacyButton", privacyButton), ("privacyLabel", privacyLabel), ("privacyHint", privacyHint),
                ("supportButton", supportButton), ("supportLabel", supportLabel), ("supportHint", supportHint),
                ("versionLabel", version));

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Налаштування зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        private static float BuildHeader(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, out Button backButton, out TMP_Text title)
        {
            var height = M(46f);
            var go = Child(parent, "Header");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -height);
            rect.offsetMax = new Vector2(-SideMargin, 0f);

            backButton = RoundIconButton(go, "Back", design, font, circle, circleOutline, null, "‹",
                new Vector2(0f, 0.5f), Vector2.zero);

            // Праворуч нічого немає (це і є налаштування), тож заголовок центрується по шапці.
            title = Label(go, "Title", "НАЛАШТУВАННЯ", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, Vector2.zero, new Vector2(M(260f), M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            return height;
        }

        private static TMP_Text Caption(GameObject content, string name, string text, DesignSystem design,
            TMP_FontAsset? font, ref float y)
        {
            var label = Label(content, name, text, design, font, design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Left);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin + M(6f), y - M(18f));
            rect.offsetMax = new Vector2(-SideMargin, y);
            y -= M(24f);
            return label;
        }

        /// <summary>Скляна картка на всю ширину з відступами; рядки кладуться всередину зверху.</summary>
        private static GameObject Card(GameObject content, string name, Sprite rounded, Sprite outline,
            DesignSystem design, float height, ref float y)
        {
            var go = Child(content, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, y - height);
            rect.offsetMax = new Vector2(-SideMargin, y);

            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(CardRadius);
            fill.color = design.GlassFill;
            fill.raycastTarget = false;

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(CardRadius);
            stroke.color = design.GlassStroke;
            stroke.raycastTarget = false;

            y -= height;
            return go;
        }

        private static RectTransform Row(GameObject card, string name, int index)
        {
            var go = Child(card, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            var top = -(CardPadding + RowHeight * index);
            rect.offsetMin = new Vector2(0f, top - RowHeight);
            rect.offsetMax = new Vector2(0f, top);
            return rect;
        }

        /// <summary>Рядок із перемикачем: тап по всьому рядку, пігулка 52×30 праворуч, кружок 22.</summary>
        private static SettingsToggle ToggleRow(GameObject card, string name, string text, int index,
            DesignSystem design, TMP_FontAsset? font, Sprite rounded, Sprite circle)
        {
            var row = Row(card, name, index);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var label = Label(row.gameObject, "Label", text, design, font, design.FontSizeBody, design.TextPrimary, TextAlignmentOptions.Left);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(M(16f), 0f);
            labelRect.offsetMax = new Vector2(-M(84f), 0f);

            var trackGo = Child(row.gameObject, "Track");
            var track = trackGo.AddComponent<Image>();
            track.sprite = rounded;
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(15f));
            track.color = design.ToggleOffFill;
            track.raycastTarget = false;
            Place(track, new Vector2(-M(16f), 0f), new Vector2(M(52f), M(30f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var knobGo = Child(trackGo, "Knob");
            var knob = knobGo.AddComponent<Image>();
            knob.sprite = circle;
            knob.color = design.ToggleKnob;
            knob.raycastTarget = false;
            Place(knob, Vector2.zero, new Vector2(M(22f), M(22f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;

            var toggle = row.gameObject.AddComponent<SettingsToggle>();
            Wire(toggle, ("design", design), ("label", label), ("button", button),
                ("track", track), ("knob", knobGo.GetComponent<RectTransform>()), ("knobImage", knob));
            return toggle;
        }

        /// <summary>Рядок-посилання: напис ліворуч, значення або «›» праворуч; тап по всьому рядку.</summary>
        private static Button LinkRow(GameObject card, string name, string text, string valueText, int index,
            DesignSystem design, TMP_FontAsset? font, out TMP_Text label, out TMP_Text value)
        {
            var row = Row(card, name, index);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            label = Label(row.gameObject, "Label", text, design, font, design.FontSizeBody, design.TextPrimary, TextAlignmentOptions.Left);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(M(16f), 0f);
            labelRect.offsetMax = new Vector2(-M(110f), 0f);

            value = Label(row.gameObject, "Value", valueText, design, font, design.FontSizeBody, design.TextMuted, TextAlignmentOptions.Right);
            var valueRect = value.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0f, 0f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = new Vector2(M(120f), 0f);
            valueRect.offsetMax = new Vector2(-M(16f), 0f);

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            return button;
        }

        /// <summary>Кнопка-дія на всю ширину: «Додому» — акцентний градієнт, решта — скло.</summary>
        private static Button ActionButton(GameObject content, string name, string text, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, bool accent, ref float y, out TMP_Text label)
        {
            var height = M(48f);
            var go = Child(content, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, y - height);
            rect.offsetMax = new Vector2(-SideMargin, y);

            var fill = go.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(24f));
            if (accent)
                fill.SetGradient(design.AccentTeal, design.AccentBlue);
            else
                fill.SetGradient(design.GlassFill, design.GlassFill);

            if (!accent)
            {
                var strokeGo = Child(go, "Stroke");
                Stretch(strokeGo);
                var stroke = strokeGo.AddComponent<Image>();
                stroke.sprite = outline;
                stroke.type = Image.Type.Sliced;
                stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(24f));
                stroke.color = design.GlassStroke;
                stroke.raycastTarget = false;
            }

            label = Label(go, "Label", text, design, font, design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            y -= height + M(10f);
            return button;
        }
    }
}
