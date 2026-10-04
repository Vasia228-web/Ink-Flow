using System.IO;
using InkFlow.Core;
using InkFlow.Gameplay;
using InkFlow.Meta;
using InkFlow.Style;
using InkFlow.UI;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Стенд екрана забігу в Edit Mode: спільна частина (камера, канвас, safe area) —
    /// <see cref="ScreenRigBase"/>, тут — живий префаб EndlessScreen і сесії для його станів.
    /// Стани ставляться через EndlessScreen.Preview* — без корутин, тому кадр можна знімати одразу.
    ///
    /// Один стенд на дві задачі: утиліта знімків (<see cref="RunScreenshots"/>) і UI-тести
    /// розкладки поля (Assets/Tests/EditMode/UI). Два окремі стенди розійшлися б на першій
    /// правці, і тест перевіряв би не те, що бачить автор на знімку.
    /// </summary>
    public sealed class RunScreenRig : ScreenRigBase
    {
        public const string PrefabPath = "Assets/_Prefabs/Screens/EndlessScreen.prefab";
        public const string LibraryPath = "Assets/_ScriptableObjects/Pictures/PictureLibrary.asset";
        public const string BalancePath = "Assets/_ScriptableObjects/Balance/BalanceConfig.asset";

        private readonly GameObject _screenGo;

        public EndlessScreen Screen { get; }
        public BoardView Board { get; }
        public PictureLibrary Library { get; }
        public BalanceData Balance { get; }
        public PlayerState Player { get; }

        /// <summary>Чи є з чого будувати стенд: префаб екрана й дизайн-система.</summary>
        public static bool Available =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath) != null;

        public static RunScreenRig Create(Device device) => new RunScreenRig(device);

        private RunScreenRig(Device device) : base(device, LoadDesign())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException($"Немає {PrefabPath} — спершу Build Endless Screen.");
            var libraryAsset = AssetDatabase.LoadAssetAtPath<PictureLibraryAsset>(LibraryPath);
            var balanceAsset = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            Library = libraryAsset != null ? libraryAsset.ToLibrary() : PictureLibrary.LoadFromDirectory(Path.GetFullPath("Assets/_Pictures"));
            Balance = balanceAsset != null ? balanceAsset.ToBalanceData() : BalanceData.Default;

            _screenGo = InstantiateScreen(prefab);
            // Без «??»: для об'єктів Unity він не бачить знищеного («fake null») об'єкта.
            var screen = _screenGo.GetComponent<EndlessScreen>();
            if (screen == null)
                throw new System.InvalidOperationException("У префабі немає EndlessScreen.");
            Screen = screen;
            var board = _screenGo.GetComponentInChildren<BoardView>(true);
            if (board == null)
                throw new System.InvalidOperationException("У префабі немає BoardView.");
            Board = board;

            Player = PlayerState.NewPlayer(EconomyData.Default, null, Library, Balance);
            Player.Wallet.Add(500, RewardSource.Debug);
            Canvas.ForceUpdateCanvases();
        }

        private static DesignSystem LoadDesign()
        {
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            if (design == null)
                throw new System.InvalidOperationException($"Немає {DesignSystemPath} — спершу Build UI Kit.");
            return design;
        }

        /// <summary>Перерозкладка після зміни розміру — те, що в грі робить LateUpdate за прапорцем.</summary>
        public override void Relayout()
        {
            Canvas.ForceUpdateCanvases();
            Screen.Layout();
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Сесія після стількох ходів бота; <paramref name="stopWhen"/> зупиняє раніше.</summary>
        public RunSession NewSession(int botMoves, uint seed = 4242u, System.Func<RunSession, bool>? stopWhen = null,
            PictureLibrary? library = null)
        {
            var session = new RunSession(Balance, PieceCatalogData.Default, new XorShiftRandom(seed), library ?? Library);
            var bot = new RunBot();
            for (var i = 0; i < botMoves && !session.IsOver && bot.TryChooseMove(session, out var index, out var anchor); i++)
            {
                if (stopWhen != null && stopWhen(session))
                    break;
                session.TryPlace(index, anchor);
            }
            return session;
        }

        /// <summary>Сесія, чия картинка — перша з бібліотеки потрібної рідкості (знімки й тести гало рідкості).</summary>
        public RunSession SessionWithRarity(Rarity rarity, int botMoves)
        {
            foreach (var picture in Library.Pictures)
                if (picture.Rarity == rarity)
                    return NewSession(botMoves, library: new PictureLibrary(new[] { picture }));
            throw new System.InvalidOperationException($"У бібліотеці немає картинки рідкості {rarity}.");
        }

        /// <summary>Та сама партія, відновлена зі зліпка (§9) — як після закриття застосунку.</summary>
        public RunSession Restored(RunSession source)
        {
            var snapshot = new RunSnapshot();
            source.Capture(snapshot);
            return new RunSession(Balance, PieceCatalogData.Default, Library, snapshot);
        }

        /// <summary>Показує сесію як є (без картки перед забігом) і розкладає під поточний пристрій.</summary>
        public void Show(RunSession session)
        {
            Canvas.ForceUpdateCanvases();
            Screen.PreviewSession(session, Player);
            Relayout();
        }

        /// <summary>Самоперевірка поля (див. <see cref="BoardView.FindLayoutFault"/>): null — усе на місці.</summary>
        public string? LayoutFault()
        {
            Canvas.ForceUpdateCanvases();
            return Board.FindLayoutFault(Safe);
        }

        public override void Dispose()
        {
            if (_screenGo != null) Object.DestroyImmediate(_screenGo);
            base.Dispose();
        }
    }
}
