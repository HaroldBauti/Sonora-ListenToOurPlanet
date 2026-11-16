// Earth Jukebox · Optimizador NSGA-II con HBA.Evolutionary
// Lee data/features.json (exportado por la página web) y busca parámetros musicales
// que equilibren FIDELIDAD a los datos y MUSICALIDAD. Escribe data/pareto.json.
//
// Uso (desde la carpeta optimizer):  dotnet run -c Release [generaciones] [semilla]

using System.Text.Json;
using HBA.Evolutionary.Core;
using HBA.Evolutionary.MultiObjective;
using HBA.Evolutionary.Operators;

var root = FindRoot();
var featuresPath = Path.Combine(root, "data", "features.json");
var outPath = Path.Combine(root, "data", "pareto.json");
int generations = args.Length > 0 ? int.Parse(args[0]) : 250;
int seed = args.Length > 1 ? int.Parse(args[1]) : 42;

if (!File.Exists(featuresPath))
{
    Console.Error.WriteLine($"No existe {featuresPath}. Abre la página, pulsa \"Exportar características\" y guarda el archivo en data/.");
    return 1;
}

var features = JsonSerializer.Deserialize<Features>(File.ReadAllText(featuresPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Engine.Data = features;
Console.WriteLine($"Características: {features.Cols} columnas · {features.Image}");

var manual = Params.Manual;
var manualScore = Engine.Score(manual);
Console.WriteLine($"Parámetros manuales → fidelidad {manualScore.Fidelity:F3} · musicalidad {manualScore.Musicality:F3}");

var config = new NSGA2Configuration<MappingChromosome>
{
    PopulationSize = 120,
    Generations = generations,
    CrossoverProbability = 0.9,
    MutationProbability = 1.0,
    Seed = seed,
    MaxDegreeOfParallelism = -1,
    RemoveDuplicateObjectives = true,
    Crossover = new SimulatedBinaryCrossover<MappingChromosome>(),
    Mutation = new PolynomialMutation<MappingChromosome>(),
};

var result = NSGA2Solver.Solve(
    prototype: new MappingChromosome(),
    configuration: config,
    onGenerationCompleted: (gen, front) =>
    {
        if (gen % 50 == 0) Console.WriteLine($"Generación {gen}: {front.Count} soluciones en el frente");
    });

var front = result.ParetoFront
    .Where(i => i.IsFeasible)
    .Select(i => (P: i.Genes.Decode(), F: i.Objectives[0], M: i.Objectives[1]))
    .OrderBy(s => s.F) // de más musical a más científico
    .ToList();

double hv = ParetoMetrics.Hypervolume2D(result.ParetoFront.Select(i => i.Objectives), new[] { 0.0, 0.0 }, minimize: new[] { false, false });
Console.WriteLine($"Frente final: {front.Count} soluciones · hipervolumen {hv:F4} · {result.Evaluations} evaluaciones · {result.Elapsed.TotalSeconds:F1} s");
foreach (var s in front.Where((_, i) => i % Math.Max(1, front.Count / 6) == 0))
    Console.WriteLine($"  fidelidad {s.F:F3} · musicalidad {s.M:F3} · suavizado {s.P.SmoothMel:F2} · rango {s.P.PitchSpan} notas");

object Entry(Params p, double f, double m)
{
    var seq = Engine.Compile(p);
    return new { @params = p, fidelity = Math.Round(f, 4), musicality = Math.Round(m, 4), melody = seq.Mi, hits = seq.Hits };
}

var output = new
{
    generatedAt = DateTime.UtcNow.ToString("u"),
    library = "HBA.Evolutionary 2.0.0 · NSGA-II",
    seed,
    generations = result.GenerationsExecuted,
    evaluations = result.Evaluations,
    hypervolume = Math.Round(hv, 4),
    objectives = new[] { "fidelity", "musicality" },
    manual = Entry(manual, manualScore.Fidelity, manualScore.Musicality),
    front = front.Select(s => Entry(s.P, s.F, s.M)).ToList(),
};
File.WriteAllText(outPath, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
Console.WriteLine($"Guardado: {outPath}");
return 0;

static string FindRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "index.html"))) dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}

// ---------------------------------------------------------------------------

public sealed record Features(int Cols, string Image, Dictionary<string, double[]> Layers);

/// <summary>Parámetros de mapeo imagen → música (mismos nombres que en index.html).</summary>
public sealed record Params(
    double SmoothMel, double SmoothRhy, double SulThr, double Gamma, int PitchLow, int PitchSpan,
    double Dust1, double Dust2, double Dust4, double SmokeThr)
{
    public static readonly Params Manual = new(0, 0, 0.10, 1.0, 0, 15, 0.12, 0.45, 0.75, 0.12);
}

public sealed record Sequence(int[] Mi, int[] Hits, bool[] Bass);

public static class Engine
{
    public const int ScaleSize = 15; // pentatónica de do menor, 3 octavas
    public static Features Data = null!;

    /// <summary>Suavizado exponencial cíclico (una vuelta de calentamiento). Idéntico a ema() en index.html.</summary>
    public static double[] Ema(double[] x, double a)
    {
        int n = x.Length; var outp = new double[n]; double s = x[0];
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++) { s = a * s + (1 - a) * x[i]; if (pass == 1) outp[i] = s; }
        return outp;
    }

    static int RoundHalfUp(double v) => (int)Math.Floor(v + 0.5); // igual que Math.round de JS

    public static Sequence Compile(Params p)
    {
        var sul = Data.Layers["sulfate"]; var dust = Data.Layers["dust"]; var smoke = Data.Layers["smoke"];
        var sm = Ema(sul, p.SmoothMel); var sd = Ema(dust, p.SmoothRhy);
        int n = Data.Cols; var mi = new int[n]; var hits = new int[n]; var bass = new bool[n];
        for (int c = 0; c < n; c++)
        {
            mi[c] = sm[c] > p.SulThr ? Math.Min(ScaleSize - 1, p.PitchLow + RoundHalfUp(Math.Pow(sm[c], p.Gamma) * (p.PitchSpan - 1))) : -1;
            hits[c] = sd[c] > p.Dust4 ? 4 : sd[c] > p.Dust2 ? 2 : sd[c] > p.Dust1 ? 1 : 0;
            bass[c] = smoke[c] > p.SmokeThr;
        }
        return new Sequence(mi, hits, bass);
    }

    public static (double Fidelity, double Musicality) Score(Params p)
    {
        var s = Compile(p); int n = Data.Cols;
        var sul = Data.Layers["sulfate"]; var dust = Data.Layers["dust"]; var smoke = Data.Layers["smoke"];

        // FIDELIDAD: ¿la música sube y baja cuando los datos suben y bajan?
        var melLevel = s.Mi.Select(m => m < 0 ? 0.0 : (m + 1.0) / ScaleSize).ToArray();
        var rhyLevel = s.Hits.Select(h => h / 4.0).ToArray();
        var bassLevel = Enumerable.Range(0, n).Select(c => s.Bass[c] ? 0.3 + 0.7 * smoke[c] : 0.0).ToArray();
        double fidelity = (Math.Max(0, Pearson(sul, melLevel)) + Math.Max(0, Pearson(dust, rhyLevel)) + Math.Max(0, Pearson(smoke, bassLevel))) / 3.0;

        // MUSICALIDAD
        var notes = s.Mi.Where(m => m >= 0).ToArray();
        double smoothness = 0, variety = 0;
        if (notes.Length >= 2)
        {
            double leap = 0; for (int i = 0; i < notes.Length; i++) leap += Math.Abs(notes[(i + 1) % notes.Length] - notes[i]);
            smoothness = 1 - Math.Min(1, leap / notes.Length / 6.0);           // saltos melódicos pequeños
            variety = Math.Min(1, notes.Distinct().Count() / 5.0);             // no una sola nota repetida
        }
        double density = 1 - Math.Min(1, Math.Abs(notes.Length / (double)n - 0.7) / 0.7); // ~70 % de pasos con nota
        int rhyChanges = 0, bassChanges = 0;
        for (int c = 0; c < n; c++)
        {
            if (s.Hits[c] != s.Hits[(c + 1) % n]) rhyChanges++;
            if (s.Bass[c] != s.Bass[(c + 1) % n]) bassChanges++;
        }
        double rhythm = 1 - rhyChanges / (double)n, bassFlow = 1 - bassChanges / (double)n;
        double musicality = 0.30 * smoothness + 0.20 * variety + 0.20 * density + 0.20 * rhythm + 0.10 * bassFlow;
        return (fidelity, musicality);
    }

    static double Pearson(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average(), sab = 0, saa = 0, sbb = 0;
        for (int i = 0; i < a.Length; i++) { double da = a[i] - ma, db = b[i] - mb; sab += da * db; saa += da * da; sbb += db * db; }
        return saa < 1e-12 || sbb < 1e-12 ? 0 : sab / Math.Sqrt(saa * sbb);
    }
}

/// <summary>Cromosoma: 10 genes reales que se decodifican a <see cref="Params"/>.</summary>
public sealed class MappingChromosome : IMultiObjectiveChromosome<MappingChromosome>, IRealVectorChromosome<MappingChromosome>
{
    //                                     smoothMel smoothRhy sulThr gamma pitchLow pitchSpan dust1 gap2 gap4 smokeThr
    static readonly double[] Lo = { 0.00, 0.00, 0.00, 0.40, 0, 3, 0.00, 0.05, 0.05, 0.00 };
    static readonly double[] Hi = { 0.85, 0.85, 0.50, 2.50, 6, 15, 0.50, 0.40, 0.40, 0.50 };

    public double[] Genes { get; private set; } = new double[Lo.Length];
    public double[] LowerBounds => Lo;
    public double[] UpperBounds => Hi;

    public void Randomize(GeneticRandom random)
    {
        for (int i = 0; i < Genes.Length; i++) Genes[i] = random.NextDouble(Lo[i], Hi[i]);
    }

    public Params Decode()
    {
        int low = (int)Math.Floor(Genes[4] + 0.5);
        int span = Math.Min((int)Math.Floor(Genes[5] + 0.5), Engine.ScaleSize - low);
        double d1 = Genes[6], d2 = d1 + Genes[7], d4 = d2 + Genes[8];
        return new Params(R(Genes[0]), R(Genes[1]), R(Genes[2]), R(Genes[3]), low, span, R(d1), R(d2), R(d4), R(Genes[9]));
    }

    static double R(double v) => Math.Round(v, 4); // los mismos valores que se exportan al JSON

    public double[] EvaluateObjectives()
    {
        var (f, m) = Engine.Score(Decode());
        return new[] { f, m };
    }

    public MappingChromosome Clone() => new() { Genes = (double[])Genes.Clone() };
}
