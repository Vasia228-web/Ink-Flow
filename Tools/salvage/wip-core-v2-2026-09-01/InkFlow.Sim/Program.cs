// Прогонник ядра v2. Перший артефакт проєкту за §0: у гру можна грати без редактора,
// тож баланс перевіряється прогонами, а не оком.
//
//   dotnet run --project Tools/InkFlow.Sim -c Release -- --games 1000 --seed 42 --bot greedy --report csv
//
// Робочий цикл, і він єдиний правильний:
//   змінив формулу → прогнав 1000 партій → звірив із §13 → лишив або відкотив

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using InkFlow.Core;
using InkFlow.Sim;

var options = Options.Parse(args);
if (options is null)
    return 2;

var balance = options.Balance;
var catalog = PieceCatalogData.Default;
var canvases = CanvasCatalog.FromStarters();

if (options.Mode == SimMode.Levels)
    return RunLevels(options, balance, catalog, canvases);

return RunEndless(options, balance, catalog, canvases);

static int RunEndless(Options options, BalanceData balance, PieceCatalogData catalog, CanvasCatalog canvases)
{
    var canvas = canvases.Get(options.CanvasId);
    var stopwatch = Stopwatch.StartNew();
    var results = new RunStats[options.Games];

    Parallel.For(0, options.Games, i =>
    {
        var seed = unchecked((uint)(options.Seed + i * 2654435761u));
        results[i] = PlayOne(canvas, balance, catalog, seed, options.Noise, options.PlacementCap, options.Weights);
    });

    stopwatch.Stop();

    var percent = new Distribution(results.Select(r => r.CanvasPercent * 100f).ToArray());
    var placements = new Distribution(results.Select(r => (float)r.Placements).ToArray());
    var pureShare = new Distribution(results
        .Select(r => r.Lines == 0 ? 0f : 100f * r.PureLines / r.Lines).ToArray());
    var murkShare = new Distribution(results
        .Select(r => r.PaintTotal == 0 ? 0f : 100f * r.PaintToMurk / r.PaintTotal).ToArray());
    var fallbackShare = new Distribution(results
        .Select(r => r.Placements == 0 ? 0f : 100f * r.Fallbacks / r.Placements).ToArray());
    var perTenth = new Distribution(results
        .Select(r => r.CanvasPercent <= 0f ? 999f : r.Placements / (r.CanvasPercent * 10f)).ToArray());
    var linesPerRun = new Distribution(results.Select(r => (float)r.Lines).ToArray());

    // Коридор placements_per_10pct у §13 заданий як 8–12, і він арифметично несумісний із
    // двома сусідніми рядками тієї ж таблиці: 25–45% полотна за 60–120 розміщень дають
    // 60/4,5 … 120/2,5, тобто 13–48. Тут стоїть саме похідний коридор — рядки 1 і 2
    // сформульовані явно й з обґрунтуванням, тож правий той із них, а не наслідок.
    var rows = new (Distribution D, string Name, string Unit, Corridor C)[]
    {
        (percent, "canvas_percent", "%", new Corridor("canvas_percent", "%", 25f, 45f)),
        (placements, "placements_to_loss", "шт", new Corridor("placements", "шт", 60f, 120f)),
        (pureShare, "pure_line_share", "%", new Corridor("pure", "%", 15f, 35f)),
        (murkShare, "paint_to_murk", "%", new Corridor("murk", "%", 5f, 20f)),
        (fallbackShare, "tray_fallback", "% розміщень", new Corridor("fallback", "%", 0f, 1f)),
        (perTenth, "placements_per_10pct", "шт", new Corridor("per10", "шт", 13f, 48f))
    };

    if (options.Csv)
    {
        Console.WriteLine("metric,unit,mean,p10,median,p90,low,high,verdict");
        foreach (var row in rows)
            Console.WriteLine(row.D.Csv(row.Name, row.Unit, row.C));
        Console.WriteLine(new Distribution(results.Select(r => (float)r.Lines).ToArray())
            .Csv("lines_per_run", "шт", new Corridor("lines", "шт", 0f, 9999f)));
    }
    else
    {
        Console.WriteLine($"Полотно «{canvas.Id}» {canvas.Size}×{canvas.Size}, клітинок {canvas.TotalCells}, " +
                          $"фарби на 100%: {CanvasCatalog.PaintCost(canvas)}");
        Console.WriteLine($"Партій: {options.Games}, сід {options.Seed}, шум {options.Noise:0.##}, " +
                          $"{stopwatch.ElapsedMilliseconds} мс");
        Console.WriteLine();
        Console.WriteLine($"{"метрика",-22} {"серед.",8} {"p10",8} {"мед.",8} {"p90",8}   коридор");
        foreach (var row in rows)
            Console.WriteLine(
                $"{row.Name,-22} {row.D.Mean,8:0.##} {row.D.P10,8:0.##} {row.D.Median,8:0.##} {row.D.P90,8:0.##}   " +
                $"[{row.C.Low:0.#}..{row.C.High:0.#}] {(row.C.Contains(row.D.Mean) ? "ok" : "ЗА МЕЖАМИ")}");
        Console.WriteLine($"{"lines_per_run",-22} {linesPerRun.Mean,8:0.##} {linesPerRun.P10,8:0.##} " +
                          $"{linesPerRun.Median,8:0.##} {linesPerRun.P90,8:0.##}");
    }

    Console.Error.WriteLine($"# {stopwatch.ElapsedMilliseconds} мс на {options.Games} партій");
    return rows.All(r => r.C.Contains(r.D.Mean)) ? 0 : 1;
}

static RunStats PlayOne(CanvasDefinition canvas, BalanceData balance, PieceCatalogData catalog,
    uint seed, float noise, int placementCap, BotWeights weights)
{
    var session = new EndlessSession(canvas, balance, catalog, seed);
    var bot = new GreedyBot(weights,
        noise > 0f ? new XorShiftRandom(seed ^ 0x5bf03635u) : null, noise);

    var lines = 0;
    var pure = 0;

    while (!session.IsOver && session.PlacementCount < placementCap)
    {
        if (!bot.TryChooseMove(session, out var trayIndex, out var anchor))
            break;
        var result = session.TryPlace(trayIndex, anchor);
        if (!result.Accepted)
            break; // бот запропонував невалідний хід — це баг, а не кінець партії
        lines += result.LinesCleared;
        pure += result.PureLinesCleared;
    }

    return new RunStats(
        session.CanvasProgress, session.PlacementCount, lines, pure,
        session.Tanks.TotalReceived, session.Tanks.TotalOverflowed,
        session.TrayFallbacksUsed, session.State == GameState.Won);
}

static int RunLevels(Options options, BalanceData balance, PieceCatalogData catalog, CanvasCatalog canvases)
{
    var levels = StarterLevels.All;
    var reports = new SolveReport[levels.Count];

    var stopwatch = Stopwatch.StartNew();
    Parallel.For(0, levels.Count, i =>
    {
        var level = StarterLevels.Data(levels[i]);
        reports[i] = LevelSolver.Solve(level, canvases.Get(level.CanvasId), balance, catalog,
            options.Games, options.Noise);
    });
    stopwatch.Stop();

    if (options.Csv)
    {
        Console.WriteLine("level,canvas,limit,success,median_left,p75_left,two_star,three_star,verdict");
        for (var i = 0; i < reports.Length; i++)
        {
            var r = reports[i];
            Console.WriteLine(string.Join(",", new[]
            {
                r.LevelId.ToString(CultureInfo.InvariantCulture),
                levels[i].CanvasId,
                r.PlacementLimit.ToString(CultureInfo.InvariantCulture),
                r.SuccessRate.ToString("0.###", CultureInfo.InvariantCulture),
                r.MedianLeft.ToString(CultureInfo.InvariantCulture),
                r.P75Left.ToString(CultureInfo.InvariantCulture),
                r.TwoStarFraction.ToString("0.##", CultureInfo.InvariantCulture),
                r.ThreeStarFraction.ToString("0.##", CultureInfo.InvariantCulture),
                r.IsValid() ? "ok" : "FAIL"
            }));
        }
    }
    else
    {
        Console.WriteLine($"{levels.Count} рівнів × {options.Games} прогонів, {stopwatch.ElapsedMilliseconds} мс");
        foreach (var r in reports)
            Console.WriteLine($"  {r}  {(r.IsValid() ? "ok" : "НЕ ПРОХОДИТЬСЯ")}");
    }

    return reports.All(r => r.IsValid()) ? 0 : 1;
}

namespace InkFlow.Sim
{
    internal enum SimMode
    {
        Endless,
        Levels
    }

    internal sealed class Options
    {
        public int Games { get; private set; } = 1000;
        public uint Seed { get; private set; } = 42;
        public bool Csv { get; private set; }
        public SimMode Mode { get; private set; } = SimMode.Endless;
        public string CanvasId { get; private set; } = "aurora";
        public float Noise { get; private set; } = 4f;

        /// <summary>Ваги бота. Свій прапорець, щоб зважування підбиралось прогонами, а не перекомпіляцією.</summary>
        public BotWeights Weights { get; private set; } = BotWeights.Default;

        /// <summary>Баланс прогону. Перекривається --balance, щоб свіп не вимагав перекомпіляції.</summary>
        public BalanceData Balance { get; private set; } = BalanceData.Default;

        /// <summary>Запобіжник від нескінченної партії: ідеальний бот теоретично не програє ніколи.</summary>
        public int PlacementCap { get; private set; } = 2000;

        public static Options? Parse(string[] args)
        {
            var options = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{key} без значення");

                switch (key)
                {
                    case "--games": options.Games = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--seed": options.Seed = uint.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--bot":
                        var bot = Next();
                        if (bot != "greedy")
                        {
                            Console.Error.WriteLine($"Невідомий бот «{bot}». Є лише greedy.");
                            return null;
                        }

                        break;
                    case "--report": options.Csv = Next() == "csv"; break;
                    case "--mode":
                        var mode = Next();
                        options.Mode = mode == "levels" ? SimMode.Levels : SimMode.Endless;
                        break;
                    case "--canvas": options.CanvasId = Next(); break;
                    case "--noise": options.Noise = float.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--cap": options.PlacementCap = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--balance":
                        options.Balance = ParseBalance(Next());
                        break;
                    case "--weights":
                        var parts = Next().Split(',');
                        if (parts.Length != 6)
                        {
                            Console.Error.WriteLine("--weights чекає шість чисел: canvas,leftover,empty,frag,pure,potential");
                            return null;
                        }

                        options.Weights = new BotWeights(
                            float.Parse(parts[0], CultureInfo.InvariantCulture),
                            float.Parse(parts[1], CultureInfo.InvariantCulture),
                            float.Parse(parts[2], CultureInfo.InvariantCulture),
                            float.Parse(parts[3], CultureInfo.InvariantCulture),
                            float.Parse(parts[4], CultureInfo.InvariantCulture),
                            float.Parse(parts[5], CultureInfo.InvariantCulture));
                        break;
                    case "--help":
                    case "-h":
                        Console.WriteLine(
                            "--games N --seed S --bot greedy --report csv|text --mode endless|levels " +
                            "--canvas ID --noise F --cap N --weights c,l,e,f,p,pp --balance k=v,k=v");
                        return null;
                    default:
                        Console.Error.WriteLine($"Невідомий аргумент «{key}».");
                        return null;
                }
            }

            return options;
        }

        /// <summary>
        /// «tankCap=24,purePaintPerCell=2,…» — рівно ті самі поля, що в BalanceConfig.asset.
        /// Свіп по балансу мусить іти прапорцем, інакше кожна гіпотеза коштує перекомпіляції.
        /// </summary>
        private static BalanceData ParseBalance(string spec)
        {
            var d = BalanceData.Default;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2)
                    throw new ArgumentException($"Не розібрати «{pair}»");
                values[kv[0].Trim()] = kv[1].Trim();
            }

            int I(string key, int fallback) =>
                values.TryGetValue(key, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;
            float F(string key, float fallback) =>
                values.TryGetValue(key, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : fallback;

            var mode = d.TrayColorMode;
            if (values.TryGetValue("trayColorMode", out var m))
                mode = Enum.Parse<TrayColorMode>(m, ignoreCase: true);

            return new BalanceData(
                I("gridWidth", d.GridWidth), I("gridHeight", d.GridHeight), I("traySize", d.TraySize),
                I("purePaintPerCell", d.PurePaintPerCell), I("mixedDivisor", d.MixedDivisor),
                d.ComboMultipliers,
                I("tankCap", d.TankCap), I("murkPerCell", d.MurkPerCell),
                F("underpaintWeight", d.UnderpaintWeight), I("maxBrushes", d.MaxBrushes),
                I("escalationStep", d.EscalationStep), I("startPieceSize", d.StartPieceSize),
                I("maxPieceSize", d.MaxPieceSize), I("maxTrayAttempts", d.MaxTrayAttempts),
                I("haloWarningFreeCells", d.HaloWarningFreeCells), I("hintIdleSeconds", d.HintIdleSeconds),
                mode);
        }
    }
}
