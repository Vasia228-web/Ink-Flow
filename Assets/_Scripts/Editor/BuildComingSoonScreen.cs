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
    /// Збирає заглушку «Скоро» для режиму «Рівні» (документ §10) у стилі решти екранів:
    /// шапка з «‹», скляна картка посередині. Меню: Ink Flow → Setup → Build Coming Soon Screen.
    /// </summary>
    public static class BuildComingSoonScreen
    {
        private const string ScenePath = "Assets/Scenes/ComingSoon.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        private static readonly float SideMargin = M(18f);

        [MenuItem("Ink Flow/Setup/Build Coming Soon Screen")]
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
            foreach (var (sprite, name) in new[]
            {
                (rounded, "rounded-rect"), (outline, "rounded-rect-outline"),
                (circle, "circle-soft"), (circleOutline, "circle-outline")
            })
                if (sprite == null) missing.Add($"{SpriteFolder}/{name}.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Заглушку НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "ComingSoonScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<ComingSoonScreen>();

            // ── Шапка ──
            var header = Child(screenGo, "Header");
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.offsetMin = new Vector2(SideMargin, -M(40f));
            headerRect.offsetMax = new Vector2(-SideMargin, 0f);

            var backGo = Child(header, "Back");
            var backFill = backGo.AddComponent<Image>();
            backFill.sprite = circle;
            backFill.color = design!.CircleButtonFill;
            Place(backFill, Vector2.zero, new Vector2(M(40f), M(40f)), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            var ringGo = Child(backGo, "Ring");
            Stretch(ringGo);
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = circleOutline;
            ring.color = design.GlassStroke;
            ring.raycastTarget = false;
            var chevron = Label(backGo, "Glyph", "‹", design, font, M(22f), design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(chevron.gameObject);
            var backButton = backGo.AddComponent<Button>();
            backButton.targetGraphic = backFill;

            var title = Label(header, "Title", "РІВНІ", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, Vector2.zero, new Vector2(M(240f), M(24f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            // ── Картка ──
            var plateGo = Child(screenGo, "Plate");
            var plate = plateGo.AddComponent<Image>();
            plate.sprite = rounded;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(26f));
            plate.color = design.GlassFill;
            plate.raycastTarget = false;
            Place(plate, new Vector2(0f, M(40f)), new Vector2(M(320f), M(220f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var strokeGo = Child(plateGo, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(26f));
            stroke.color = design.GlassStroke;
            stroke.raycastTarget = false;

            var caption = Label(plateGo, "Caption", "Скоро", design, font,
                design.FontSizeDisplay, design.TextPrimary, TextAlignmentOptions.Center);
            Place(caption, new Vector2(0f, -M(46f)), new Vector2(M(280f), M(60f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var note = Label(plateGo, "Note", "Режим «Рівні» повернеться на новому ядрі.", design, font,
                design.FontSizeBody, design.TextMuted, TextAlignmentOptions.Center);
            note.textWrappingMode = TextWrappingModes.Normal;
            Place(note, new Vector2(0f, M(34f)), new Vector2(M(272f), M(80f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            Wire(screen, ("design", design), ("backButton", backButton), ("title", title),
                ("caption", caption), ("note", note), ("plate", plate), ("plateStroke", stroke));

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Заглушку «Скоро» зібрано: {ScenePath}.");
        }
    }
}
