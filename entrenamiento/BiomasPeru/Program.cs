using System.Diagnostics;
using System.Text.Json;
using BiomasPeru;
using Hba.NeuralNet;
using Spectre.Console;
using Spectre.Console.Rendering;
using Grid = BiomasPeru.Grid;

// Uso: dotnet run -c Release -- [--minutos 12] [--benchmark]
double minutes = args.SkipWhile(a => a != "--minutos").Skip(1).Select(double.Parse).FirstOrDefault(12);
bool benchmarkOnly = args.Contains("--benchmark");
const int Seed = 42;
Console.OutputEncoding = System.Text.Encoding.UTF8;

string root = Directory.GetCurrentDirectory();
string dataDir = Path.Combine(root, "datos"), outDir = Path.Combine(root, "salida");
Directory.CreateDirectory(Path.Combine(outDir, "frames"));
var total = Stopwatch.StartNew();
var timings = new Dictionary<string, double>();

// ---------- Ayudas de presentación ----------
void Mark(string name, Stopwatch sw)
{
    timings[name] = Math.Round(sw.Elapsed.TotalSeconds, 1);
    AnsiConsole.MarkupLine($"[green]✓[/] [grey]{name.Replace('_', ' ')} · {sw.Elapsed:mm\\:ss} · total {total.Elapsed:mm\\:ss}[/]");
    AnsiConsole.WriteLine();
}
void Stage(int n, string title) =>
    AnsiConsole.Write(new Rule($"[bold aqua]{n}[/] [bold]{Markup.Escape(title)}[/]").LeftJustified().RuleStyle("grey"));
static string Bar(double frac, int width = 20)
{
    int n = (int)Math.Round(Math.Clamp(frac, 0, 1) * width);
    return new string('█', n) + new string('░', width - n);
}
static Color BiomeColor(Bioma c) => new((byte)(c.Rgba >> 24), (byte)(c.Rgba >> 16), (byte)(c.Rgba >> 8));
static string Esc(object? o) => Markup.Escape(o?.ToString() ?? "—");

AnsiConsole.Write(new FigletText("BiomasPeru").Color(Color.Aqua));
AnsiConsole.Write(new Panel(new Markup(
        "Clasificación de biomas de Perú con [bold]Hba.NeuralNet[/] + búsqueda de hiperparámetros con [bold]NSGA-II (HBA.Evolutionary)[/]\n" +
        $"[grey]Datos: NASA GIBS (MODIS NDVI, LST, IGBP, SRTM) · 2001–2024 · presupuesto de búsqueda: {minutes} min · semilla {Seed}[/]"))
    .Header("[bold]SONORA · entrenamiento[/]").BorderColor(Color.Grey).Expand());
AnsiConsole.WriteLine();

// 1. Datos NASA GIBS
Stage(1, "Datos NASA GIBS");
var sw = Stopwatch.StartNew();
var (pending, cached) = Datos.Pending(dataDir);
if (pending == 0)
    AnsiConsole.MarkupLine($"[grey]Los {cached} archivos ya están en caché ({Esc(dataDir)}).[/]");
else
    await AnsiConsole.Progress()
        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn(), new SpinnerColumn())
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask($"Descargando {pending} archivos (en caché: {cached})", maxValue: pending);
            await Datos.DownloadAsync(dataDir, (done, _) => task.Value = done);
        });
var d = AnsiConsole.Status().Spinner(Spinner.Known.Dots).Start("Decodificando mapas de NASA…", _ => Datos.Load(dataDir));
var b = new Biomas(d);
Mapas.PeruMask = d.Peru;
Mark("datos", sw);

var counts = b.LabelCounts(Seed);
var labels = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .Title("[bold]Celdas etiquetadas por bioma[/]")
    .AddColumn("Bioma").AddColumn(new TableColumn("Entren.").RightAligned())
    .AddColumn(new TableColumn("Valid.").RightAligned()).AddColumn(new TableColumn("Prueba").RightAligned());
foreach (var c in Biomas.Clases)
    labels.AddRow(new Markup($"[{BiomeColor(c).ToMarkup()}]■[/] {Esc(c.Nombre)}"),
        new Text($"{counts[0, c.Id]:N0}"), new Text($"{counts[1, c.Id]:N0}"), new Text($"{counts[2, c.Id]:N0}"));
labels.Caption($"[grey]Celdas válidas: {b.Valid.Count(v => v):N0} · etiquetadas: {b.Label.Count(l => l >= 0):N0}[/]");
AnsiConsole.Write(labels);
AnsiConsole.WriteLine();

// 2. Muestras estratificadas (celda, año) con partición espacial
Stage(2, "Muestreo con partición espacial");
sw.Restart();
var sets = b.Sample([2500, 500, 800], Seed);
var (train, val, test) = (sets[0], sets[1], sets[2]);
AnsiConsole.MarkupLine($"Entrenamiento [bold]{train.X.Count:N0}[/] · validación [bold]{val.X.Count:N0}[/] · prueba [bold]{test.X.Count:N0}[/] · {Biomas.Dim} características");
Mark("muestreo", sw);

// 3. Benchmark: ¿cuánto cuesta una iteración?
Stage(3, "Benchmark");
int lowN = train.X.Count * 3 / 10, lowV = val.X.Count / 2;
double Bench(HyperParams p, int n, int iters)
{
    var net = p.Build(Seed);
    var t = Stopwatch.StartNew();
    net.Train(train.X.Take(n).ToList(), train.Y.Take(n).ToList(), p.Config(iters, 1000), CancellationToken.None);
    return t.Elapsed.TotalSeconds / Math.Max(1, net.TrainingHistory.Last?.Iteration ?? iters);
}
var (secLow, secFull) = AnsiConsole.Status().Spinner(Spinner.Known.Dots).Start("Midiendo el coste de una iteración…", _ =>
{
    Bench(HyperParams.Baseline, 500, 2); // calentamiento (JIT)
    return (Bench(HyperParams.Baseline, lowN, 5), Bench(HyperParams.Baseline, train.X.Count, 5));
});
AnsiConsole.Write(new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .Title($"[bold]Red base {Esc($"[{string.Join("-", HyperParams.Baseline.Hidden)}]")}[/]")
    .AddColumn("Filas").AddColumn(new TableColumn("s / iteración").RightAligned())
    .AddRow($"{lowN:N0} (búsqueda)", $"{secLow:F3}")
    .AddRow($"{train.X.Count:N0} (completo)", $"{secFull:F3}"));
AnsiConsole.WriteLine();
if (benchmarkOnly) return;

// 4. Búsqueda evolutiva de hiperparámetros (NSGA-II, baja fidelidad)
Stage(4, "Búsqueda de hiperparámetros · NSGA-II");
sw.Restart();
const int LowIterations = 30, LowPatience = 5, Population = 20, Generations = 25;
var lowData = new Datasets(train.X.Take(lowN).ToList(), train.Y.Take(lowN).ToList(), val.X.Take(lowV).ToList(), val.Y.Take(lowV).ToList(), val.Clase.Take(lowV).ToList());
var ev = new Evaluator(lowData, LowIterations, LowPatience, Seed);
AnsiConsole.MarkupLine($"[grey]Población {Population} · hasta {Generations} generaciones o {minutes} min · cada evaluación: {lowN:N0} filas, ≤{LowIterations} iteraciones, paciencia {LowPatience}[/]");

var budget = TimeSpan.FromMinutes(minutes);
var genRows = new List<Evaluator.GenerationInfo>();
IRenderable SearchView()
{
    var last = genRows.LastOrDefault();
    var elapsed = last?.Elapsed ?? TimeSpan.Zero;
    double frac = Math.Max(elapsed / budget, (last?.Generation ?? 0) / (double)Generations);
    var head = new Markup(
        $"[aqua]{Bar(frac, 40)}[/] [bold]{frac:P0}[/]\n" +
        $"Generación [bold]{last?.Generation ?? 0}[/]/{Generations} · tiempo [bold]{elapsed:mm\\:ss}[/]/{budget:mm\\:ss} · " +
        $"entrenamientos [bold]{last?.Trainings ?? 0}[/] (caché {last?.CacheHits ?? 0})");
    var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey).Expand()
        .AddColumn(new TableColumn("Gen").RightAligned()).AddColumn("Tiempo").AddColumn(new TableColumn("Entren.").RightAligned())
        .AddColumn("Mejor exactitud equilibrada").AddColumn("Mejor configuración");
    double prev = double.NaN;
    var shown = new List<(Evaluator.GenerationInfo G, bool Up)>();
    foreach (var g in genRows)
    {
        double acc = g.BestBalancedAccuracy ?? double.NaN;
        shown.Add((g, !double.IsNaN(prev) && acc > prev + 1e-9));
        prev = acc;
    }
    foreach (var (g, up) in shown.TakeLast(12))
    {
        double acc = g.BestBalancedAccuracy ?? 0;
        table.AddRow(new Text($"{g.Generation}"), new Text($"{g.Elapsed:mm\\:ss}"), new Text($"{g.Trainings}"),
            new Markup($"[green]{Bar(acc, 16)}[/] {acc:P1}{(up ? " [bold green]▲[/]" : "")}"),
            new Markup(up ? $"[bold]{Esc(g.Best)}[/]" : $"[grey]{Esc(g.Best)}[/]"));
    }
    if (genRows.Count > 12) table.Caption($"[grey]mostrando las últimas 12 de {genRows.Count} generaciones[/]");
    return new Rows(head, table);
}
var hpo = AnsiConsole.Live(SearchView()).AutoClear(false).Start(ctx =>
    Evaluator.Run(ev, Population, Generations, budget, Seed, info =>
    {
        lock (genRows) { genRows.Add(info); ctx.UpdateTarget(SearchView()); }
    }));
AnsiConsole.MarkupLine($"[grey]Parada: {Esc(hpo.StopReason)} · {hpo.GenerationsExecuted} generaciones · {hpo.Evaluations} evaluaciones[/]");
Mark("busqueda_hiperparametros", sw);

var front = hpo.ParetoFront.Select(i => (P: i.Genes.Decode(), Bacc: 1 - i.Objectives[0], Params: (int)Math.Round(Math.Pow(10, i.Objectives[1]))))
    .DistinctBy(f => f.P.ToString()).OrderByDescending(f => f.Bacc).ToList();
var best = front[0].P;
var frontTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .Title("[bold]Frente de Pareto[/] [grey](exactitud equilibrada en validación vs. tamaño de la red)[/]")
    .AddColumn("Exactitud equilibrada").AddColumn(new TableColumn("Parámetros").RightAligned()).AddColumn("Configuración");
int maxParams = front.Max(f => f.Params);
foreach (var f in front)
{
    bool chosen = f.P.ToString() == best.ToString();
    frontTable.AddRow(new Markup($"[green]{Bar(f.Bacc, 16)}[/] {f.Bacc:P1}"),
        new Markup($"[yellow]{Bar(f.Params / (double)maxParams, 8)}[/] {f.Params:N0}"),
        new Markup(chosen ? $"[bold aqua]★ {Esc(f.P)}[/]" : Esc(f.P)));
}
frontTable.Caption("[aqua]★[/] [grey]= configuración elegida (la más exacta)[/]");
AnsiConsole.Write(frontTable);
AnsiConsole.WriteLine();

// 5. Entrenamiento final (todos los datos) del mejor y de la línea base, mismo presupuesto
Stage(5, "Entrenamiento final");
const int FinalIterations = 200, FinalPatience = 12;
(Perceptron Net, double Seconds, int Iter, string Stop) TrainFull(HyperParams p)
{
    var net = p.Build(Seed);
    var t = Stopwatch.StartNew();
    net.Train(train.X, train.Y, val.X, val.Y, p.Config(FinalIterations, FinalPatience), CancellationToken.None);
    return (net, t.Elapsed.TotalSeconds, net.TrainingHistory.Last?.Iteration ?? 0, net.TrainingHistory.StopReason?.ToString() ?? "?");
}
sw.Restart();
var ((net, finalSec, finalIter, finalStop), (baseNet, baseSec, baseIter, baseStop)) = await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots).StartAsync($"Entrenando la red elegida y la red base con {train.X.Count:N0} filas (hasta {FinalIterations} iteraciones)…", async _ =>
    {
        var finalTask = Task.Run(() => TrainFull(best));
        var baseTask = Task.Run(() => TrainFull(HyperParams.Baseline));
        return (await finalTask, await baseTask);
    });
Mark("entrenamiento_final", sw);

var cmTest = Metrics.Confusion(net, test.X, test.Clase);
var cmBase = Metrics.Confusion(baseNet, test.X, test.Clase);
double baccTest = Metrics.Recall(cmTest).Where(r => !double.IsNaN(r)).Average();
double baccBase = Metrics.Recall(cmBase).Where(r => !double.IsNaN(r)).Average();
double gain = baccTest - baccBase;
AnsiConsole.Write(new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .Title("[bold]Prueba[/] [grey](bloques espaciales no vistos)[/]")
    .AddColumn("Red").AddColumn("Exactitud equilibrada").AddColumn(new TableColumn("Iteraciones").RightAligned())
    .AddColumn(new TableColumn("Tiempo").RightAligned()).AddColumn("Parada")
    .AddRow(new Markup($"[bold aqua]Optimizada[/] [grey]{Esc(best)}[/]"), new Markup($"[green]{Bar(baccTest, 16)}[/] [bold]{baccTest:P1}[/]"),
        new Text($"{finalIter}"), new Text($"{finalSec:F0} s"), new Text(finalStop))
    .AddRow(new Markup($"Base [grey]{Esc(HyperParams.Baseline)}[/]"), new Markup($"[grey]{Bar(baccBase, 16)}[/] {baccBase:P1}"),
        new Text($"{baseIter}"), new Text($"{baseSec:F0} s"), new Text(baseStop))
    .Caption(gain >= 0 ? $"[green]NSGA-II mejora a la red base en {gain * 100:+0.0;-0.0} puntos[/]" : $"[red]La red base gana por {-gain * 100:0.0} puntos[/]"));
var recall = Metrics.Recall(cmTest);
var recallChart = new BarChart().Width(70).Label("[bold]Recall por bioma (prueba)[/]").CenterLabel().WithMaxValue(100);
foreach (var c in Biomas.Clases)
    recallChart.AddItem(c.Nombre, double.IsNaN(recall[c.Id]) ? 0 : Math.Round(recall[c.Id] * 100, 1), BiomeColor(c));
AnsiConsole.Write(recallChart);
AnsiConsole.WriteLine();
net.SaveNetwork(Path.Combine(outDir, "modelo_biomas.json"));

// 6. Atlas 2001–2024 (red neuronal + tendencias) y mapas de calor para todo Perú
Stage(6, "Atlas y mapas 2001–2024");
sw.Restart();
int last = Datos.Years - 1;
Atlas atlas = null!;
void SaveMap(string file, string tipo, int anio)
{
    var m = atlas.Render(tipo, anio, "peru", sonora: false);
    Png.Save(Path.Combine(outDir, file), m.W, m.H, m.Rgba);
}
AnsiConsole.Progress()
    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn(), new SpinnerColumn())
    .Start(ctx =>
    {
        var tAtlas = ctx.AddTask("Construyendo el atlas (red neuronal + tendencias)", maxValue: 1);
        var tMaps = ctx.AddTask("Mapas y barras de color", maxValue: 8);
        var tFrames = ctx.AddTask("Fotogramas por año", maxValue: Datos.Years);

        atlas = Atlas.Build(net, b, d.Peru);
        tAtlas.Increment(1);

        SaveMap("biomas_2001.png", "biomas", 2001); tMaps.Increment(1);
        SaveMap("biomas_2024.png", "biomas", 2024); tMaps.Increment(1);
        SaveMap("tendencia_bosque.png", "cambio_bosque", 2024); tMaps.Increment(1);
        SaveMap("tendencia_ndvi.png", "tendencia_ndvi", 2024); tMaps.Increment(1);
        Mapas.Save(Path.Combine(outDir, "biomas_etiquetas_igbp.png"), b, i => b.Label[i] >= 0 ? Biomas.Clases[b.Label[i]].Rgba : 0x6B7280FF); tMaps.Increment(1);
        Mapas.Save(Path.Combine(outDir, "confianza_2024.png"), b, i => Mapas.Sequential((Enumerable.Range(0, Biomas.K).Max(k => atlas.Prob(last, i, k)) - 0.3) / 0.7)); tMaps.Increment(1);
        Mapas.ColorBar(Path.Combine(outDir, "cbar_tendencia.png"), Mapas.Diverging, -1, 1); tMaps.Increment(1);
        Mapas.ColorBar(Path.Combine(outDir, "cbar_probabilidad.png"), Mapas.Sequential, 0, 1); tMaps.Increment(1);

        for (int y = 0; y < Datos.Years; y++)
        {
            int yy = y, year = Datos.Year0 + y;
            Mapas.Save(Path.Combine(outDir, "frames", $"bosque_{year}.png"), b, i => Mapas.Sequential(atlas.Forest(yy, i)));
            SaveMap(Path.Combine("frames", $"ndvi_anomalia_{year}.png"), "anomalia_ndvi", year);
            tFrames.Increment(1);
        }
    });
Mark("mapas", sw);

// 7. Casos de estudio
Stage(7, "Casos de estudio");
var regionStats = Atlas.Regiones.Skip(1).Select(atlas.Stats).ToList();
var cases = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    .AddColumn("Región").AddColumn(new TableColumn("NDVI / década").RightAligned()).AddColumn(new TableColumn("Celdas bajando").RightAligned())
    .AddColumn("Bosque 2001 → 2024").AddColumn("Bioma dominante 2024");
foreach (var r in regionStats)
{
    string ndviColor = r.NdviTendenciaPorDecada < 0 ? "red" : "green";
    double dForest = r.BosqueProb2024 - r.BosqueProb2001;
    string forestColor = dForest < -0.01 ? "red" : dForest > 0.01 ? "green" : "grey";
    var dom = Biomas.Clases.FirstOrDefault(c => c.Nombre == r.BiomaDominante2024);
    cases.AddRow(new Text(r.Region), new Markup($"[{ndviColor}]{r.NdviTendenciaPorDecada:+0.000;-0.000}[/]"),
        new Text($"{r.PctNdviBajando} %"),
        new Markup($"{r.BosqueProb2001:P0} → [{forestColor}]{r.BosqueProb2024:P0}[/]"),
        new Markup(dom is null ? Esc(r.BiomaDominante2024) : $"[{BiomeColor(dom).ToMarkup()}]■[/] {Esc(dom.Nombre)}"));
}
AnsiConsole.Write(cases);
AnsiConsole.WriteLine();

// 8. Resumen
int naiveTrainings = Population * Generations;
var summary = new
{
    generado = DateTime.Now,
    fuente = "NASA GIBS: MODIS Terra NDVI mensual (MOD13A3), MODIS Terra LST mensual, MODIS IGBP anual (MCD12Q1), SRTM. Rejilla 0,05° (~5,5 km), 2001–2024.",
    rejilla = new { ancho = Grid.W, alto = Grid.H, escala_png = Mapas.Scale, lon_min = Grid.LonMin, lat_max = Grid.LatMax, resolucion_grados = Grid.Res },
    biomas = Biomas.Clases.Select(c => new { c.Id, c.Nombre, color = $"#{c.Rgba >> 8:X6}" }),
    caracteristicas = Biomas.Features,
    muestras = new { entrenamiento = train.X.Count, validacion = val.X.Count, prueba = test.X.Count, particion = "bloques espaciales de 0,5°" },
    benchmark = new { seg_por_iteracion_baja_fidelidad = Math.Round(secLow, 4), seg_por_iteracion_completo = Math.Round(secFull, 4) },
    busqueda = new
    {
        algoritmo = "HBA.Evolutionary 2.0.0 · NSGA-II", poblacion = Population, generaciones = hpo.GenerationsExecuted, evaluaciones = hpo.Evaluations,
        entrenamientos_reales = ev.Trainings, aciertos_cache = ev.CacheHits, parada = hpo.StopReason.ToString(),
        baja_fidelidad = new { filas = lowN, iteraciones_max = LowIterations, paciencia = LowPatience },
        segundos = timings["busqueda_hiperparametros"],
        entrenamiento_promedio_s = Math.Round(ev.Log.Average(l => l.Seconds), 2),
        // Mismos entrenamientos pero con todos los datos y 200 iteraciones sin parada temprana, mismo paralelismo.
        estimacion_sin_atajos_s = Math.Round(timings["busqueda_hiperparametros"] * (secFull / secLow) * ((double)FinalIterations / LowIterations), 0),
        evaluaciones_maximas = naiveTrainings,
        frente_pareto = front.Select(f => new { exactitud_equilibrada = Math.Round(f.Bacc, 4), parametros = f.Params, hiperparametros = f.P.ToString() }),
    },
    final = new
    {
        hiperparametros = best.ToString(), parametros = best.Parameters(Biomas.Dim, Biomas.K),
        iteraciones = finalIter, parada = finalStop, segundos = Math.Round(finalSec, 1),
        prueba_exactitud_equilibrada = Math.Round(baccTest, 4), prueba_exactitud = Math.Round(Metrics.Accuracy(cmTest), 4),
        recall_por_bioma = Biomas.Clases.ToDictionary(c => c.Nombre, c => Math.Round(recall[c.Id], 4)),
        matriz_confusion = Enumerable.Range(0, Biomas.K).Select(i => Enumerable.Range(0, Biomas.K).Select(j => cmTest[i, j]).ToArray()),
    },
    linea_base = new
    {
        hiperparametros = HyperParams.Baseline.ToString(), iteraciones = baseIter, parada = baseStop, segundos = Math.Round(baseSec, 1),
        prueba_exactitud_equilibrada = Math.Round(baccBase, 4),
    },
    tendencias = new { metodo = "Theil-Sen + Mann-Kendall (|Z| ≥ 1,96)", rango_paleta_bosque_por_anio = 0.02, rango_paleta_ndvi_por_anio = 0.006 },
    casos_de_estudio = regionStats,
    tiempos_s = timings.Append(new("total", Math.Round(total.Elapsed.TotalSeconds, 1))).ToDictionary(),
};
File.WriteAllText(Path.Combine(outDir, "resumen.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

var times = new BreakdownChart().Width(70);
Color[] palette = [Color.Aqua, Color.Yellow, Color.Green, Color.Fuchsia, Color.Orange1, Color.Blue];
int ci = 0;
foreach (var (k, v) in timings) times.AddItem(k.Replace('_', ' '), v, palette[ci++ % palette.Length]);
AnsiConsole.Write(new Panel(new Rows(
        new Markup($"[bold green]Listo en {total.Elapsed:mm\\:ss}[/] · exactitud equilibrada en prueba [bold]{baccTest:P1}[/] (base {baccBase:P1})"),
        new Markup($"Resultados en [link]{Esc(outDir)}[/]"),
        new Text(""),
        new Markup("[grey]Tiempo por etapa (s):[/]"),
        times))
    .Header("[bold]Resumen[/]").BorderColor(Color.Green).Expand());
