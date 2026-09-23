// Прогонник нового ядра: тисяча забігів бота замість десяти ручних партій.
//
//   dotnet run --project Tools/InkFlow.Sim -c Release -- --games 1000 --seed 42 --noise 3
//   dotnet run --project Tools/InkFlow.Sim -c Release -- --games 1000 --csv
//
// Робочий цикл: змінив число в BalanceData → прогнав → подивився на цифри → лишив або відкотив.
// Бот — жадібний із шумом (RunBot): моделює звичайного гравця, а не оптимальну гру.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using InkFlow.Core;
using InkFlow.Sim;

var games = 1000;
var seed = 42u;
var noise = 3f;
var csv = false;
var botName = "default";
var weights = BotWeights.Default;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--games": games = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--seed": seed = uint.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--noise": noise = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--csv": csv = true; break;
        case "--bot":
            botName = args[++i];
            // «Акуратний» цілиться в чисті лінії, «неакуратний» не бачить кольору взагалі —
            // різниця між ними в фарбі і є критерієм кроку 2 («видно різницю між акуратною
            // й неакуратною грою»).
            weights = botName switch
            {
                // Ваги підібрано перебором (--weights): «акуратний» готує чисті лінії, але не
                // ціною партії — при purityPotential ≥ 0.3 бот беріг кольори й гинув удвічі раніше.
                "careful" => new BotWeights(lineCleared: 12f, pureLine: 30f, emptyCell: 1f,
                    fragmentation: 0.5f, purityPotential: 0.25f),
                "sloppy" => new BotWeights(lineCleared: 12f, pureLine: 0f, emptyCell: 1f,
                    fragmentation: 0.35f, purityPotential: 0f),
                "default" => BotWeights.Default,
                _ => throw new ArgumentException($"Невідомий бот: {botName} (default | careful | sloppy)")
            };
            break;
        case "--weights":
        {
            // Довільні ваги для перебору: lineCleared,pureLine,emptyCell,fragmentation,purityPotential.
            var parts = args[++i].Split(',');
            if (parts.Length != 5)
                throw new ArgumentException("--weights чекає п'ять чисел через кому");
            float W(int k) => float.Parse(parts[k], CultureInfo.InvariantCulture);
            weights = new BotWeights(W(0), W(1), W(2), W(3), W(4));
            botName = "custom(" + args[i] + ")";
            break;
        }
        case "--help":
            Console.WriteLine("--games N  --seed S  --noise F  --bot default|careful|sloppy  --weights a,b,c,d,e  --csv");
            return 0;
        default:
            Console.Error.WriteLine($"Невідомий аргумент: {args[i]}");
            return 2;
    }
}

var balance = BalanceData.Default;
var catalog = PieceCatalogData.Default;
var stopwatch = Stopwatch.StartNew();
var results = new RunStats[games];

Parallel.For(0, games, i =>
{
    var runSeed = unchecked(seed + (uint)i * 2654435761u);
    results[i] = PlayOne(balance, catalog, runSeed, noise, weights);
});

stopwatch.Stop();

var placements = new Distribution(results.Select(r => (float)r.Placements).ToArray());
var rounds = new Distribution(results.Select(r => (float)r.Rounds).ToArray());
var score = new Distribution(results.Select(r => (float)r.Score).ToArray());
var lines = new Distribution(results.Select(r => (float)r.Lines).ToArray());
var pureShare = new Distribution(results.Select(r => r.Lines == 0 ? 0f : 100f * r.PureLines / r.Lines).ToArray());
var bestChain = new Distribution(results.Select(r => (float)r.BestChain).ToArray());
var paint = new Distribution(results.Select(r => (float)r.PaintYielded).ToArray());
var paintPerPlacement = new Distribution(results.Select(r => r.Placements == 0 ? 0f : (float)r.PaintYielded / r.Placements).ToArray());
var splashes = new Distribution(results.Select(r => (float)r.Splashes).ToArray());
var placementsPerSplash = new Distribution(results.Select(r => r.Splashes == 0 ? (float)r.Placements : (float)r.Placements / r.Splashes).ToArray());
var splashTotal = results.Sum(r => r.Splashes);
var baseShare = splashTotal == 0 ? 0f : 100f * results.Sum(r => r.BaseSplashes) / splashTotal;
var secondaryShare = splashTotal == 0 ? 0f : 100f * results.Sum(r => r.SecondarySplashes) / splashTotal;
var brownShare = splashTotal == 0 ? 0f : 100f * results.Sum(r => r.BrownSplashes) / splashTotal;
var noSplash = results.Count(r => r.Splashes == 0);
var paintPerLine = new Distribution(results.Select(r => r.Lines == 0 ? 0f : (float)r.PaintYielded / r.Lines).ToArray());
var pressure = new Distribution(results.Where(r => r.PressureAt >= 0).Select(r => (float)r.PressureAt).ToArray());
var pressureShare = new Distribution(results.Where(r => r.PressureAt >= 0)
    .Select(r => 100f * r.PressureAt / Math.Max(1, r.Placements)).ToArray());
var emptyAtDeath = new Distribution(results.Select(r => (float)r.EmptyAtDeath).ToArray());
var rescues = new Distribution(results.Select(r => (float)r.Rescues).ToArray());

var lostAtRefill = results.Count(r => r.LostAtRefill);
var unfair = results.Count(r => r.Unfair);
var neverPressured = results.Count(r => r.PressureAt < 0);

if (csv)
{
    Console.WriteLine("metric,unit,mean,p10,median,p90,min,max");
    Console.WriteLine(placements.Csv("placements_per_run", "шт"));
    Console.WriteLine(rounds.Csv("rounds_per_run", "шт"));
    Console.WriteLine(score.Csv("score", "очок"));
    Console.WriteLine(lines.Csv("lines_per_run", "шт"));
    Console.WriteLine(pureShare.Csv("pure_line_share", "%"));
    Console.WriteLine(bestChain.Csv("best_chain", "ліній"));
    Console.WriteLine(paint.Csv("paint_yielded", "од"));
    Console.WriteLine(paintPerPlacement.Csv("paint_per_placement", "од"));
    Console.WriteLine(splashes.Csv("splashes", "шт"));
    Console.WriteLine(placementsPerSplash.Csv("placements_per_splash", "шт"));
    Console.WriteLine($"splash_hue_base,%,{baseShare.ToString("0.##", CultureInfo.InvariantCulture)},,,,,");
    Console.WriteLine($"splash_hue_secondary,%,{secondaryShare.ToString("0.##", CultureInfo.InvariantCulture)},,,,,");
    Console.WriteLine($"splash_hue_brown,%,{brownShare.ToString("0.##", CultureInfo.InvariantCulture)},,,,,");
    Console.WriteLine($"runs_without_splash,%,{(100f * noSplash / games).ToString("0.##", CultureInfo.InvariantCulture)},,,,,");
    Console.WriteLine(paintPerLine.Csv("paint_per_line", "од"));
    Console.WriteLine(pressure.Csv("pressure_onset_placement", "шт"));
    Console.WriteLine(pressureShare.Csv("pressure_onset_share", "% партії"));
    Console.WriteLine(emptyAtDeath.Csv("empty_cells_at_death", "шт"));
    Console.WriteLine(rescues.Csv("tray_rescues", "шт"));
    Console.WriteLine($"lost_at_refill,%,{100f * lostAtRefill / games:0.##},,,,,");
    Console.WriteLine($"unfair_deaths,шт,{unfair},,,,,");
    return 0;
}

Console.WriteLine($"Ink Flow · прогін {games} забігів · бот {botName} · сід {seed} · шум {noise} · {stopwatch.Elapsed.TotalSeconds:0.0} с");
Console.WriteLine();
Console.WriteLine(placements.Row("розміщень за партію", "шт"));
Console.WriteLine(rounds.Row("лотків (раундів)", "шт"));
Console.WriteLine(score.Row("очки", "очок"));
Console.WriteLine(lines.Row("ліній за партію", "шт"));
Console.WriteLine(pureShare.Row("частка чистих ліній", "%"));
Console.WriteLine(bestChain.Row("найдовший ланцюг", "ліній"));
Console.WriteLine(paint.Row("фарби за партію", "од"));
Console.WriteLine(paintPerPlacement.Row("фарби на розміщення", "од"));
Console.WriteLine(paintPerLine.Row("фарби на лінію", "од"));
Console.WriteLine(splashes.Row("виплесків за партію", "шт"));
Console.WriteLine(placementsPerSplash.Row("розміщень на виплеск", "шт"));
Console.WriteLine(pressure.Row("початок тиску (розміщ.)", "шт"));
Console.WriteLine(pressureShare.Row("початок тиску (% партії)", "%"));
Console.WriteLine(emptyAtDeath.Row("вільних клітинок у смерть", "шт"));
Console.WriteLine(rescues.Row("рятувань мішка", "шт"));
Console.WriteLine();
Console.WriteLine($"тиск не настав узагалі: {neverPressured} з {games} ({100f * neverPressured / games:0.#} %)");
Console.WriteLine($"відтінки виплесків: чисті {baseShare:0.#} % · вторинні {secondaryShare:0.#} % · коричневі {brownShare:0.#} %");
Console.WriteLine($"партій без жодного виплеску: {noSplash} з {games} ({100f * noSplash / games:0.#} %)");
Console.WriteLine($"смерть одразу після поповнення лотка: {lostAtRefill} з {games} ({100f * lostAtRefill / games:0.#} %)");
Console.WriteLine($"смертей не з вини гравця (мішок дав неможливий набір, хоч 2-клітинкова влазила): {unfair}");
return unfair == 0 ? 0 : 1;

static RunStats PlayOne(BalanceData balance, PieceCatalogData catalog, uint runSeed, float noise, BotWeights weights)
{
    var session = new RunSession(balance, catalog, new XorShiftRandom(runSeed));
    var bot = new RunBot(weights, new XorShiftRandom(unchecked(runSeed ^ 0x9E3779B9u)), noise);

    var pressureAt = -1;
    var lostAtRefill = false;
    var guard = 0;

    while (!session.IsOver && guard++ < 10_000)
    {
        if (!bot.TryChooseMove(session, out var index, out var anchor))
            break;

        var result = session.TryPlace(index, anchor);
        if (!result.Accepted)
            break;

        // «Тиск» — перше розміщення, після якого хоч одна фігура з руки вже нікуди не
        // влазить: із цього моменту гравець грає не «куди хочу», а «куди можна».
        if (pressureAt < 0 && !session.IsOver && session.AnyPieceStuck())
            pressureAt = session.PlacementCount;

        if (session.IsOver)
            lostAtRefill = result.Has(GameEventType.TrayRefilled);
    }

    var unfair = false;
    if (lostAtRefill)
        for (var i = 0; i < catalog.Count; i++)
            if (catalog[i].Size == catalog.MinSize && PlacementRules.AnyFit(session.Board, catalog[i]))
                unfair = true;

    return new RunStats(session.PlacementCount, session.Round, session.Score, session.LinesCleared,
        session.PureLinesCleared, session.BestChain, session.PaintYielded, pressureAt,
        session.TrayRescues, lostAtRefill, unfair, session.Board.CountEmpty(),
        session.Splashes, CountHues(session, Hues.IsBase), CountHues(session, Hues.IsSecondary),
        session.SplashesByHue[(int)Hue.Brown]);
}

static int CountHues(RunSession session, Func<Hue, bool> filter)
{
    var n = 0;
    foreach (var hue in Hues.All)
        if (filter(hue))
            n += session.SplashesByHue[(int)hue];
    return n;
}
