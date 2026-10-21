using InkFlow.Style;
using InkFlow.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.Editor
{
    /// <summary>
    /// Створює спрайти, шрифти, DesignSystem.asset і спільні префаби (капсула нафти, космічний фон).
    /// Меню: Ink Flow → Setup → Build UI Kit. Компоненти U1Glaze — фабрики <see cref="U1"/>, вітрина — Build Components Showcase.
    ///
    /// Порядок як у InkFlowBootstrap: спершу залежності (спрайти, шрифт, конфіг),
    /// потім префаби, і лише в самому кінці сцена — і всі асети перечитуються
    /// з AssetDatabase безпосередньо перед підв'язкою (див. коментар у InkFlowBootstrap).
    /// </summary>
    public static class BuildUIKit
    {
        private const string StyleFolder = "Assets/_ScriptableObjects/Style";
        private const string DesignSystemPath = StyleFolder + "/DesignSystem.asset";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string BlackFontPath = GenerateFontAsset.BlackPath;
        private const string BodyFontPath = GenerateFontAsset.BodyPath;

        [MenuItem("Ink Flow/Setup/Build UI Kit")]
        public static void Build()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            // Поки збираємо — жодних відкладених перечитувань стилю: усі потрібні
            // Apply() викликаються нижче синхронно, до SavePrefab.
            StyleRefresh.Suspended = true;
            try { BuildAll(); }
            finally { StyleRefresh.Suspended = false; }
        }

        private static void BuildAll()
        {
            // Кожен крок в окремому try: виняток в одному не має обривати збірку мовчки —
            // саме так недоступна для читання текстура вбила генерацію шрифтів і префабів.
            Step("UI-спрайти", GenerateUISprites.Generate);
            Step("Спрайти U1Glaze", U1GlazeSprites.ImportAll);
            Step("TMP-шрифти", GenerateFontAsset.Generate);
            Step("TMP Sprite Asset", GenerateSpriteAsset.Generate);
            EnsureDesignSystem();
            // Матеріал тіні тексту читає токени DesignSystem — тому після нього, і підв'язка окремо.
            Step("Матеріал тіні тексту", GenerateFontAsset.EnsureShadowMaterial);
            WireShadowMaterial();

            InkFlowBootstrap.EnsureFolder(PrefabFolder);
            BuildCurrencyWidget();
            BuildCosmicBackground();

            AssetDatabase.SaveAssets();
            Debug.Log("[InkFlow] UI Kit готовий: спрайти, шрифти, DesignSystem.asset, префаби CurrencyWidget і CosmicBackground.");
        }

        /// <summary>Виконує крок збірки, не даючи його падінню обірвати решту.</summary>
        private static void Step(string label, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[InkFlow] Крок «{label}» впав: {e.Message}\n{e.StackTrace}");
            }
        }

        // ───────────────────────── Дизайн-система ─────────────────────────

        private static void EnsureDesignSystem()
        {
            InkFlowBootstrap.EnsureFolder(StyleFolder);

            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);

            if (design != null && design.TokenVersion < DesignSystem.CurrentTokenVersion)
            {
                // Токени виправили в коді — переносимо їх у вже створений асет.
                // Без цього гра й далі читала б старі значення, а правки жили б лише в дефолтах.
                Debug.Log($"[InkFlow] DesignSystem: токени v{design.TokenVersion} → " +
                          $"v{DesignSystem.CurrentTokenVersion}, асет перестворюю.");
                AssetDatabase.DeleteAsset(DesignSystemPath);
                design = null;
            }

            if (design == null)
            {
                design = ScriptableObject.CreateInstance<DesignSystem>();
                AssetDatabase.CreateAsset(design, DesignSystemPath);
            }

            // Шрифти підв'язуємо тут: значення кольорів і метрик уже стоять
            // за замовчуванням у самому класі — вони і є витягом із еталона.
            // U1Glaze: Black 900 (заголовки, числа, кнопки) і Bold 700 (текст); ExtraBold — підписи над полем і картинкою забігу.
            var so = new SerializedObject(design);
            foreach (var (field, path) in new[] { ("font", FontPath), ("fontBlack", BlackFontPath), ("fontBody", BodyFontPath) })
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                    so.FindProperty(field).objectReferenceValue = font;
                else
                    Debug.LogWarning($"[InkFlow] {path} не знайдено — DesignSystem.{field} лишається порожнім (bash Tools/fetch-fonts.sh → Generate Font Assets).");
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(design);
        }

        private static void WireShadowMaterial()
        {
            var design = LoadDesign();
            var material = AssetDatabase.LoadAssetAtPath<Material>(GenerateFontAsset.BlackShadowMaterialPath);
            if (design == null || material == null)
                return;
            var so = new SerializedObject(design);
            so.FindProperty("fontBlackShadow").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(design);
        }

        private static DesignSystem LoadDesign() =>
            AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);

        // ───────────────────────── Атоми ─────────────────────────

        /// <summary>
        /// Капсула нафти U1Glaze: 9-slice capsule, крапля oil_drop 24 і число Nunito Black. Ширина росте
        /// за вмістом (HorizontalLayoutGroup + fitter): «999 999 999» вміщається, не переносячись. Висота —
        /// 46 px макета. Бірюзової плитки «+» (вхід у магазин) немає — покупка нафти в запасі
        /// (Reserve/OilShop); у макеті «+» стоїть впритул до правого краю, тож без нього праворуч
        /// відступ 14 замість 1 — число не впирається в заокруглення.
        /// </summary>
        private static void BuildCurrencyWidget()
        {
            var design = LoadDesign();
            var height = U1.Px(46f);
            var root = NewUIObject("CurrencyWidget", new Vector2(U1.Px(118f), height));
            try
            {
                var capsule = root.AddComponent<Image>();
                capsule.sprite = U1.S("capsule");
                capsule.type = Image.Type.Sliced;
                capsule.raycastTarget = false;

                var layout = root.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset((int)U1.Px(10f), (int)U1.Px(14f), 0, 0);
                layout.spacing = U1.Px(6f);
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                root.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                var element = root.AddComponent<LayoutElement>();
                element.preferredHeight = height;
                element.minHeight = height;

                var dropGo = NewChild(root, "Drop", Vector2.zero);
                var drop = dropGo.AddComponent<Image>();
                drop.sprite = U1.S("oil_drop");
                drop.raycastTarget = false;
                var dropLayout = dropGo.AddComponent<LayoutElement>();
                dropLayout.preferredWidth = dropLayout.preferredHeight = U1.Px(24f);

                var label = U1.Text(root, "Amount", "1\u00A0240", design, U1.Role.H3, design.InkText, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;

                var widget = root.AddComponent<CurrencyWidget>();
                Wire(widget,
                    ("design", design), ("capsule", capsule), ("drop", drop),
                    ("amountLabel", label));
                widget.Apply();

                SavePrefab(root, "CurrencyWidget");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildCosmicBackground()
        {
            var design = LoadDesign();

            var root = NewUIObject("CosmicBackground", Vector2.zero, stretch: true);
            try
            {
                var gradient = root.AddComponent<CosmicBackground>();
                gradient.raycastTarget = false;
                Wire(gradient, ("design", design));

                var starsGo = NewChild(root, "Stars", Vector2.zero, stretch: true);
                var stars = starsGo.AddComponent<StarField>();
                stars.raycastTarget = false;
                Wire(stars, ("design", design));

                SavePrefab(root, "CosmicBackground");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject NewUIObject(string name, Vector2 size, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.sizeDelta = size;
            }

            return go;
        }

        private static GameObject NewChild(GameObject parent, string name, Vector2 size, bool stretch = false)
        {
            var go = NewUIObject(name, size, stretch);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void SavePrefab(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        }

        /// <summary>Проставляє приватні [SerializeField]-поля з валідацією, як у InkFlowBootstrap.</summary>
        private static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields)
            {
                if (value == null)
                {
                    Debug.LogError($"[InkFlow] null у поле '{field}' на {target.GetType().Name}");
                    continue;
                }

                var property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[InkFlow] Поле '{field}' не знайдено на {target.GetType().Name}");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            var check = new SerializedObject(target);
            foreach (var (field, value) in fields)
                if (value != null && check.FindProperty(field)?.objectReferenceValue == null)
                    Debug.LogError($"[InkFlow] Поле '{field}' на {target.GetType().Name} записалось як null.");
        }
    }
}
