// Прогонник ядра піксельних картинок: тисяча забігів бота замість десяти ручних партій.
//
//   dotnet run --project Tools/InkFlow.Sim -c Release -- --games 1000 --seed 42 --noise 3
//   dotnet run --project Tools/InkFlow.Sim -c Release -- --games 1000 --csv
//   dotnet run --project Tools/InkFlow.Sim -c Release -- --seconds-per-move 4
//
// Робочий цикл: змінив число в BalanceData → прогнав → подивився на цифри → лишив або відкотив.
// Бот — жадібний із шумом (RunBot): моделює звичайного гравця, а не оптимальну гру.
// «Хвилини на картинку» = розміщень на картинку × секунд на розміщення (--seconds-per-move,
// типово 4 с — оцінка для мобільного гравця; міряється руками в Фазі 2).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InkFlow.Core;
using InkFlow.Sim;

var games = 1000;
var seed = 42u;
var noise = 3f;
var csv = false;
var carry = true;
var secondsPerMove = 4f;
var botName = "default";
var weights = BotWeights.Default;
string? picturesDir = null;
string? dangerCsv = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--games": games = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--seed": seed = uint.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--noise": noise = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--seconds-per-move": secondsPerMove = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--pictures": picturesDir = args[++i]; break;
        case "--csv": csv = true; break;
        case "--danger-csv": dangerCsv = args[++i]; break;
        case "--no-carry": carry = false; break;
        case "--bot":
            botName = args[++i];
            // «Акуратний» цілиться в чисті лінії, «неакуратний» не бачить кольору взагалі —
            // різниця між ними в пікселях і є критерієм §5 («чиста лінія має важити»).
            weights = botName switch
            {
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
            var parts = args[++i].Split(',');
            if (parts.Length != 5)
                throw new ArgumentException("--weights чекає п'ять чисел через кому: lineCleared,pureLine,emptyCell,fragmentation,purityPotential");
            float W(int k) => float.Parse(parts[k], CultureInfo.InvariantCulture);
            weights = new BotWeights(W(0), W(1), W(2), W(3), W(4));
            botName = "custom(" + args[i] + ")";
            break;
        }
        case "--help":
            Console.WriteLine("--games N  --seed S  --noise F  --seconds-per-move F  --pictures DIR  --bot default|careful|sloppy  --weights a,b,c,d,e  --no-carry  --csv  --danger-csv FILE");
            return 0;
        default:
            Console.Error.WriteLine($"Невідомий аргумент: {args[i]}");
            return 2;
    }
}

var balance = BalanceData.Default;
var catalog = PieceCatalogData.Default;
var library = PictureLibrary.LoadFromDirectory(picturesDir ?? FindPictures());
var stopwatch = Stopwatch.StartNew();
var results = new RunStats[games];
var timing = new PictureTiming(Rarities.Count);
// Колекція бота: колода віддає невидані спершу (§7), тож без неї частки рідкостей брехали б.
// Через неї забіги залежать один від одного — граємо послідовно, як гравець; --no-carry — паралельно.
var collection = new HashSet<string>(StringComparer.Ordinal);

// --danger-csv: стан поля після кожного ходу кожного забігу — сирі дані для калібрування пульсації (§11).
var dangerRows = dangerCsv is null ? null : new List<string>(games * 80);
if (carry)
{
    for (var i = 0; i < games; i++)
    {
        var runSeed = unchecked(seed + (uint)i * 2654435761u);
        results[i] = PlayOne(balance, catalog, library, runSeed, noise, weights, collection, timing, dangerRows, i);
    }
}
else
{
    var gate = new object();
    Parallel.For(0, games, i =>
    {
        var runSeed = unchecked(seed + (uint)i * 2654435761u);
        var local = new PictureTiming(Rarities.Count);
        results[i] = PlayOne(balance, catalog, library, runSeed, noise, weights, null, local);
        lock (gate)
            for (var r = 0; r < Rarities.Count; r++)
                for (var k = 0; k < local.CountOf(r); k++)
                    timing.Add(r, (int)local.Of(r).Percentile(0f) /* усі значення однакові лише в межах одного запису */);
    });
}

stopwatch.Stop();

if (dangerCsv != null && dangerRows != null)
{
    File.WriteAllLines(dangerCsv, new[] { "game,move,moves_to_end,lost,free,blocked,blocked_small,blocked_big,pieces,stuck,placeable,best_fits,level,cat_min_fits,cat_fits_sum,big_min_fits,squares3,frag,hand_min_fits,hand_ways,hand_solvable,tight1,tight3,tight6,log_sum10" }.Concat(dangerRows));
    Console.Error.WriteLine($"Сирі дані пульсації: {dangerRows.Count} ходів → {dangerCsv}");
}

var placements = new Distribution(results.Select(r => (float)r.Placements).ToArray());
var rounds = new Distribution(results.Select(r => (float)r.Rounds).ToArray());
var score = new Distribution(results.Select(r => (float)r.Score).ToArray());
var lines = new Distribution(results.Select(r => (float)r.Lines).ToArray());
var pureShare = new Distribution(results.Select(r => r.Lines == 0 ? 0f : 100f * r.PureLines / r.Lines).ToArray());
var bestChain = new Distribution(results.Select(r => (float)r.BestChain).ToArray());
var pixels = new Distribution(results.Select(r => (float)r.PixelsFilled).ToArray());
var pixelsPerPlacement = new Distribution(results.Select(r => r.Placements == 0 ? 0f : (float)r.PixelsFilled / r.Placements).ToArray());
var pixelsPerLine = new Distribution(results.Select(r => r.Lines == 0 ? 0f : (float)r.PixelsFilled / r.Lines).ToArray());
var pixelsTotal = results.Sum(r => (long)r.PixelsFilled);
var wastedTotal = results.Sum(r => (long)r.PixelsWasted);
var wastedShare = pixelsTotal + wastedTotal == 0 ? 0f : 100f * wastedTotal / (pixelsTotal + wastedTotal);
var pictures = new Distribution(results.Select(r => (float)r.Pictures).ToArray());
var fillAtDeath = new Distribution(results.Select(r => 100f * r.PictureFillAtDeath).ToArray());
var noPicture = results.Count(r => r.Pictures == 0);
var firstPicture = new Distribution(results.Where(r => r.PlacementsToFirstPicture >= 0).Select(r => (float)r.PlacementsToFirstPicture).ToArray());
var minutesPerRun = placements.Scaled(secondsPerMove / 60f);
var pressure = new Distribution(results.Where(r => r.PressureAt >= 0).Select(r => (float)r.PressureAt).ToArray());
var warnShare = new Distribution(results.Select(r => r.Placements == 0 ? 0f : 100f * r.WarnMoves / r.Placements).ToArray());
var strongShare = new Distribution(results.Select(r => r.Placements == 0 ? 0f : 100f * r.StrongMoves / r.Placements).ToArray());
var warnLead = new Distribution(results.Where(r => r.FirstWarnAt >= 0).Select(r => (float)(r.Placements - r.FirstWarnAt)).ToArray());
var strongLead = new Distribution(results.Where(r => r.FirstStrongAt >= 0).Select(r => (float)(r.Placements - r.FirstStrongAt)).ToArray());
var warnShareOfRun = new Distribution(results.Where(r => r.FirstWarnAt >= 0).Select(r => 100f * r.FirstWarnAt / Math.Max(1, r.Placements)).ToArray());
var neverWarned = results.Count(r => r.FirstWarnAt < 0);
var lostRuns = results.Where(r => r.Placements > 0).ToArray();
float LeadShare(Func<RunStats, int> lead, int atLeast) => lostRuns.Length == 0 ? 0f : 100f * lostRuns.Count(r => lead(r) >= atLeast) / lostRuns.Length;
var warnFinalLead = new Distribution(lostRuns.Select(r => (float)r.WarnLead).ToArray());
var strongFinalLead = new Distribution(lostRuns.Select(r => (float)r.StrongLead).ToArray());
var warnOnsets = new Distribution(lostRuns.Select(r => (float)r.WarnOnsets).ToArray());
var warnPooled = 100f * results.Sum(r => (long)r.WarnMoves) / Math.Max(1, results.Sum(r => (long)r.Placements));
var strongPooled = 100f * results.Sum(r => (long)r.StrongMoves) / Math.Max(1, results.Sum(r => (long)r.Placements));
var neverStrong = results.Count(r => r.FirstStrongAt < 0);
var pressureShare = new Distribution(results.Where(r => r.PressureAt >= 0)
    .Select(r => 100f * r.PressureAt / Math.Max(1, r.Placements)).ToArray());
var emptyAtDeath = new Distribution(results.Select(r => (float)r.EmptyAtDeath).ToArray());
var rescues = new Distribution(results.Select(r => (float)r.Rescues).ToArray());
var lostAtRefill = results.Count(r => r.LostAtRefill);
var unfair = results.Count(r => r.Unfair);
var neverPressured = results.Count(r => r.PressureAt < 0);
var seenByRarity = new int[Rarities.Count];
var doneByRarity = new int[Rarities.Count];
foreach (var r in results)
    for (var k = 0; k < Rarities.Count; k++)
    {
        seenByRarity[k] += r.SeenByRarity[k];
        doneByRarity[k] += r.DoneByRarity[k];
    }
var seenTotal = Math.Max(1, seenByRarity.Sum());

if (csv)
{
    Console.WriteLine("metric,unit,mean,p10,median,p90,min,max");
    Console.WriteLine(placements.Csv("placements_per_run", "шт"));
    Console.WriteLine(minutesPerRun.Csv("minutes_per_run", "хв"));
    Console.WriteLine(rounds.Csv("rounds_per_run", "шт"));
    Console.WriteLine(score.Csv("score", "очок"));
    Console.WriteLine(lines.Csv("lines_per_run", "шт"));
    Console.WriteLine(pureShare.Csv("pure_line_share", "%"));
    Console.WriteLine(bestChain.Csv("best_chain", "ліній"));
    Console.WriteLine(pixels.Csv("pixels_filled", "px"));
    Console.WriteLine(pixelsPerPlacement.Csv("pixels_per_placement", "px"));
    Console.WriteLine(pixelsPerLine.Csv("pixels_per_line", "px"));
    Console.WriteLine($"pixels_wasted_share,%,{F(wastedShare)},,,,,");
    Console.WriteLine(pictures.Csv("pictures_completed", "шт"));
    Console.WriteLine(fillAtDeath.Csv("picture_fill_at_death", "%"));
    Console.WriteLine(firstPicture.Csv("placements_to_first_picture", "шт"));
    Console.WriteLine($"runs_without_picture,%,{F(100f * noPicture / games)},,,,,");
    for (var k = 0; k < Rarities.Count; k++)
    {
        var id = Rarities.IdOf((Rarity)k);
        Console.WriteLine($"seen_{id},%,{F(100f * seenByRarity[k] / seenTotal)},,,,,");
        Console.WriteLine($"done_{id},шт,{doneByRarity[k]},,,,,");
        if (timing.CountOf(k) > 0)
            Console.WriteLine(timing.Of(k).Scaled(secondsPerMove / 60f).Csv($"minutes_per_picture_{id}", "хв"));
    }
    Console.WriteLine(pressure.Csv("pressure_onset_placement", "шт"));
    Console.WriteLine(pressureShare.Csv("pressure_onset_share", "% партії"));
    Console.WriteLine(warnShare.Csv("pulse_warn_moves_share", "%"));
    Console.WriteLine(strongShare.Csv("pulse_strong_moves_share", "%"));
    Console.WriteLine(warnLead.Csv("pulse_warn_moves_before_death", "шт"));
    Console.WriteLine(strongLead.Csv("pulse_strong_moves_before_death", "шт"));
    Console.WriteLine($"pulse_never_warned,%,{F(100f * neverWarned / games)},,,,,");
    Console.WriteLine(warnFinalLead.Csv("pulse_warn_lead_before_loss", "шт"));
    Console.WriteLine($"pulse_warn_lead_ge2,%,{F(LeadShare(r => r.WarnLead, 2))},,,,,");
    Console.WriteLine($"pulse_warn_lead_ge3,%,{F(LeadShare(r => r.WarnLead, 3))},,,,,");
    Console.WriteLine($"pulse_warn_pooled_share,%,{F(warnPooled)},,,,,");
    Console.WriteLine(emptyAtDeath.Csv("empty_cells_at_death", "шт"));
    Console.WriteLine(rescues.Csv("tray_rescues", "шт"));
    Console.WriteLine($"lost_at_refill,%,{F(100f * lostAtRefill / games)},,,,,");
    Console.WriteLine($"unfair_deaths,шт,{unfair},,,,,");
    return unfair == 0 ? 0 : 1;
}

Console.WriteLine($"Ink Flow · прогін {games} забігів · бот {botName} · сід {seed} · шум {noise} · {secondsPerMove:0.#} с/хід · бібліотека {library.Count} · {stopwatch.Elapsed.TotalSeconds:0.0} с");
Console.WriteLine();
Console.WriteLine(placements.Row("розміщень за партію", "шт"));
Console.WriteLine(minutesPerRun.Row("хвилин за партію", "хв"));
Console.WriteLine(rounds.Row("лотків (раундів)", "шт"));
Console.WriteLine(score.Row("очки", "очок"));
Console.WriteLine(lines.Row("ліній за партію", "шт"));
Console.WriteLine(pureShare.Row("частка чистих ліній", "%"));
Console.WriteLine(bestChain.Row("найдовший ланцюг", "ліній"));
Console.WriteLine(pixels.Row("пікселів за партію", "px"));
Console.WriteLine(pixelsPerPlacement.Row("пікселів на розміщення", "px"));
Console.WriteLine(pixelsPerLine.Row("пікселів на лінію", "px"));
Console.WriteLine(pictures.Row("картинок за партію", "шт"));
Console.WriteLine(fillAtDeath.Row("поточна картинка в момент смерті", "%"));
Console.WriteLine(firstPicture.Row("розміщень до першої картинки", "шт"));
Console.WriteLine(pressure.Row("початок тиску (розміщ.)", "шт"));
Console.WriteLine(pressureShare.Row("початок тиску (% партії)", "%"));
Console.WriteLine(emptyAtDeath.Row("вільних клітинок у смерть", "шт"));
Console.WriteLine(rescues.Row("рятувань мішка", "шт"));
Console.WriteLine();
Console.WriteLine("пульсація «мало місця» (§11): попередження / сильна");
Console.WriteLine($"  попередження горить без перерви до програшу: ≥2 ходи — {LeadShare(r => r.WarnLead, 2):0.#} % програшів, ≥3 — {LeadShare(r => r.WarnLead, 3):0.#} %, 0 — {100f - LeadShare(r => r.WarnLead, 1):0.#} %");
Console.WriteLine(warnFinalLead.Row("  ходів попередження перед програшем", "шт"));
Console.WriteLine($"  сильна горить до програшу: ≥2 ходи — {LeadShare(r => r.StrongLead, 2):0.#} %, ≥1 — {LeadShare(r => r.StrongLead, 1):0.#} %");
Console.WriteLine(strongFinalLead.Row("  ходів сильної перед програшем", "шт"));
Console.WriteLine($"  частка ходів із пульсацією (усі забіги разом): попередження {warnPooled:0.#} %, сильна {strongPooled:0.#} %");
Console.WriteLine(warnOnsets.Row("  вмикань попередження за забіг", "шт"));
Console.WriteLine(warnShare.Row("  ходів із попередженням", "%"));
Console.WriteLine(strongShare.Row("  ходів із сильною", "%"));
Console.WriteLine(warnShareOfRun.Row("  перше попередження (% партії)", "%"));
Console.WriteLine(warnLead.Row("  ходів від першого поперед. до смерті", "шт"));
Console.WriteLine(strongLead.Row("  ходів від першої сильної до смерті", "шт"));
Console.WriteLine($"  без попередження взагалі: {neverWarned} з {games} ({100f * neverWarned / games:0.#} %), без сильної: {neverStrong} ({100f * neverStrong / games:0.#} %)");
Console.WriteLine();
Console.WriteLine("хвилин на картинку за рідкістю (§19: звичайна 1.5–2 хв, легендарна 5+):");
for (var k = 0; k < Rarities.Count; k++)
{
    var id = Rarities.IdOf((Rarity)k);
    Console.WriteLine(timing.CountOf(k) > 0
        ? timing.Of(k).Scaled(secondsPerMove / 60f).Row($"  {id} (n={timing.CountOf(k)})", "хв")
        : $"  {id,-28} —  жодної не закінчено");
}
Console.WriteLine();
Console.WriteLine($"пікселів згоріло (колір уже не потрібен): {wastedShare:0.#} % від усіх");
Console.WriteLine($"партій без жодної закінченої картинки: {noPicture} з {games} ({100f * noPicture / games:0.#} %)");
Console.WriteLine($"тиск не настав узагалі: {neverPressured} з {games} ({100f * neverPressured / games:0.#} %)");
Console.Write("рідкість нових витягів (§6: 45/25/15/9/5/1):");
for (var k = 0; k < Rarities.Count; k++)
    Console.Write($" {Rarities.IdOf((Rarity)k)} {100f * seenByRarity[k] / seenTotal:0.#} % (закінчено {doneByRarity[k]})");
Console.WriteLine();
Console.WriteLine($"смерть одразу після поповнення лотка: {lostAtRefill} з {games} ({100f * lostAtRefill / games:0.#} %)");
Console.WriteLine($"смертей не з вини гравця (мішок дав неможливий набір, хоч 2-клітинкова влазила): {unfair}");
return unfair == 0 ? 0 : 1;

static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

static string FindPictures()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Assets", "_Pictures");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
    }
    throw new DirectoryNotFoundException("Не знайшов Assets/_Pictures — вкажи --pictures DIR.");
}

static RunStats PlayOne(BalanceData balance, PieceCatalogData catalog, PictureLibrary library, uint runSeed, float noise,
    BotWeights weights, HashSet<string>? collection, PictureTiming timing, List<string>? dangerRows = null, int gameIndex = 0)
{
    var trace = dangerRows is null ? null : new List<(int move, string row)>(96);
    Func<string, bool>? isCollected = collection is null ? null : id => collection.Contains(id);
    var session = new RunSession(balance, catalog, new XorShiftRandom(runSeed), library, isCollected);
    var bot = new RunBot(weights, new XorShiftRandom(unchecked(runSeed ^ 0x9E3779B9u)), noise);

    var pressureAt = -1;
    var firstWarnAt = -1;
    var firstStrongAt = -1;
    var warnMoves = 0;
    var strongMoves = 0;
    var warnStreak = 0;
    var strongStreak = 0;
    var warnOnsets = 0;
    var lostAtRefill = false;
    var firstPictureAt = -1;
    var pictureStartedAt = 0; // розміщення, на якому почалась поточна картинка
    var seen = new int[Rarities.Count];
    var done = new int[Rarities.Count];
    seen[(int)session.Picture.Picture.Rarity]++;
    var guard = 0;

    while (!session.IsOver && guard++ < 10_000)
    {
        if (!bot.TryChooseMove(session, out var index, out var anchor))
            break;

        var result = session.TryPlace(index, anchor);
        if (!result.Accepted)
            break;

        for (var e = 0; e < result.Events.Count; e++)
        {
            var ev = result.Events[e];
            if (ev.Type == GameEventType.PictureCompleted)
            {
                var rarity = library[ev.Value].Rarity;
                done[(int)rarity]++;
                timing.Add((int)rarity, session.PlacementCount - pictureStartedAt);
                if (firstPictureAt < 0)
                    firstPictureAt = session.PlacementCount;
                collection?.Add(library[ev.Value].Id);
            }
            else if (ev.Type == GameEventType.PictureStarted)
            {
                seen[(int)library[ev.Value].Rarity]++;
                pictureStartedAt = session.PlacementCount;
            }
        }

        // «Тиск» — перше розміщення, після якого хоч одна фігура з руки вже нікуди не
        // влазить: із цього моменту гравець грає не «куди хочу», а «куди можна».
        if (pressureAt < 0 && !session.IsOver && session.AnyPieceStuck())
            pressureAt = session.PlacementCount;

        // Сирі дані для калібрування: скільки вільних клітинок, скільки форм каталогу вже нікуди
        // не влазить (усього / малих ≤ 3 / великих ≥ 4), стан руки й рівень за чинним правилом.
        if (trace != null && !session.IsOver)
        {
            var blocked = 0; var blockedSmall = 0; var blockedBig = 0;
            for (var c = 0; c < catalog.Count; c++)
            {
                if (PlacementRules.AnyFit(session.Board, catalog[c]))
                    continue;
                blocked++;
                if (catalog[c].Size <= 3) blockedSmall++; else blockedBig++;
            }
            var d = session.Danger;
            var f = DangerStudy.Features(session.Board, catalog, session.TrayPieces);
            trace.Add((session.PlacementCount, string.Join(",",
                session.Board.CountEmpty(), blocked, blockedSmall, blockedBig,
                d.Pieces, d.Stuck, d.Placeable, d.BestFits, (int)d.Level,
                f.CatalogMinFits, f.CatalogFitsSum, f.BigMinFits, f.Squares3, f.Fragmentation, f.HandMinFits, f.HandWays, f.HandSolvable ? 1 : 0, f.Tight1, f.Tight3, f.Tight6, (int)(f.LogSum * 10))));
        }

        // Пульсація «мало місця» (§11): рівень після кожного ходу — те, що бачив би гравець.
        if (!session.IsOver)
        {
            var danger = session.Danger.Level;
            if (danger != DangerLevel.None)
            {
                warnMoves++;
                if (warnStreak == 0) warnOnsets++;
                warnStreak++;
                if (firstWarnAt < 0) firstWarnAt = session.PlacementCount;
            }
            else
                warnStreak = 0;
            if (danger == DangerLevel.Strong)
            {
                strongMoves++;
                strongStreak++;
                if (firstStrongAt < 0) firstStrongAt = session.PlacementCount;
            }
            else
                strongStreak = 0;
        }

        if (session.IsOver)
            lostAtRefill = result.Has(GameEventType.TrayRefilled);
    }

    if (trace != null && dangerRows != null)
        foreach (var (move, row) in trace)
            dangerRows.Add($"{gameIndex},{move},{session.PlacementCount - move},{(session.IsOver ? 1 : 0)},{row}");

    var unfair = false;
    if (lostAtRefill)
        for (var i = 0; i < catalog.Count; i++)
            if (catalog[i].Size == catalog.MinSize && PlacementRules.AnyFit(session.Board, catalog[i]))
                unfair = true;

    return new RunStats(session.PlacementCount, session.Round, session.Score, session.LinesCleared,
        session.PureLinesCleared, session.BestChain, session.PixelsFilled, session.PixelsWasted, pressureAt,
        session.TrayRescues, lostAtRefill, unfair, session.Board.CountEmpty(),
        session.PicturesCompleted, session.Picture.FilledFraction, firstPictureAt,
        done, seen, firstWarnAt, firstStrongAt, warnMoves, strongMoves,
        session.IsOver ? warnStreak : 0, session.IsOver ? strongStreak : 0, warnOnsets);
}
