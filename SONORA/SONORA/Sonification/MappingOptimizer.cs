using System.Diagnostics;
using HBA.Evolutionary.Core;
using HBA.Evolutionary.MultiObjective;
using HBA.Evolutionary.Operators;
using SONORA.Client.Models;

namespace SONORA.Sonification;

/// <summary>
/// Motor musical en C#. DEBE ser idéntico a <c>ema()</c> y <c>compile()</c> de <c>SONORA.Client/wwwroot/js/sonora.js</c>;
/// el cliente lo verifica al cargar cada frente de Pareto.
/// </summary>
public sealed class MusicEngine(SonificationFeatures data)
{
    public const int ScaleSize = 15; // pentatónica de do menor, 3 octavas

    readonly double[] _mel = data.Layers["melody"];
    readonly double[] _rhy = data.Layers["rhythm"];
    readonly double[] _bass = data.Layers["bass"];
    readonly int _n = data.Steps;

    public static double[] Ema(double[] x, double a)
    {
        int n = x.Length; var outp = new double[n]; double s = x[0];
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++) { s = a * s + (1 - a) * x[i]; if (pass == 1) outp[i] = s; }
        return outp;
    }

    static int RoundHalfUp(double v) => (int)Math.Floor(v + 0.5); // igual que Math.round de JS

    public (int[] Mi, int[] Hits, bool[] Bass) Compile(MappingParams p)
    {
        var sm = Ema(_mel, p.SmoothMel); var sr = Ema(_rhy, p.SmoothRhy);
        var mi = new int[_n]; var hits = new int[_n]; var bass = new bool[_n];
        for (int c = 0; c < _n; c++)
        {
            mi[c] = sm[c] > p.MelThr ? Math.Min(ScaleSize - 1, p.PitchLow + RoundHalfUp(Math.Pow(sm[c], p.Gamma) * (p.PitchSpan - 1))) : -1;
            hits[c] = sr[c] > p.Rhy4 ? 4 : sr[c] > p.Rhy2 ? 2 : sr[c] > p.Rhy1 ? 1 : 0;
            bass[c] = _bass[c] > p.BassThr;
        }
        return (mi, hits, bass);
    }

    public (double Fidelity, double Musicality) Score(MappingParams p)
    {
        var (mi, hits, bass) = Compile(p);

        // FIDELIDAD: ¿la música sube y baja cuando los datos suben y bajan?
        var melLevel = mi.Select(m => m < 0 ? 0.0 : (m + 1.0) / ScaleSize).ToArray();
        var rhyLevel = hits.Select(h => h / 4.0).ToArray();
        var bassLevel = Enumerable.Range(0, _n).Select(c => bass[c] ? 0.3 + 0.7 * _bass[c] : 0.0).ToArray();
        double fidelity = (Math.Max(0, Pearson(_mel, melLevel)) + Math.Max(0, Pearson(_rhy, rhyLevel)) + Math.Max(0, Pearson(_bass, bassLevel))) / 3.0;

        // MUSICALIDAD
        var notes = mi.Where(m => m >= 0).ToArray();
        double smoothness = 0, variety = 0;
        if (notes.Length >= 2)
        {
            double leap = 0; for (int i = 0; i < notes.Length; i++) leap += Math.Abs(notes[(i + 1) % notes.Length] - notes[i]);
            smoothness = 1 - Math.Min(1, leap / notes.Length / 6.0);        // saltos melódicos pequeños
            variety = Math.Min(1, notes.Distinct().Count() / 5.0);          // no una sola nota repetida
        }
        double density = 1 - Math.Min(1, Math.Abs(notes.Length / (double)_n - 0.7) / 0.7); // ~70 % de pasos con nota
        int rhyChanges = 0, bassChanges = 0;
        for (int c = 0; c < _n; c++)
        {
            if (hits[c] != hits[(c + 1) % _n]) rhyChanges++;
            if (bass[c] != bass[(c + 1) % _n]) bassChanges++;
        }
        double rhythm = 1 - rhyChanges / (double)_n, bassFlow = 1 - bassChanges / (double)_n;
        return (fidelity, 0.30 * smoothness + 0.20 * variety + 0.20 * density + 0.20 * rhythm + 0.10 * bassFlow);
    }

    static double Pearson(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average(), sab = 0, saa = 0, sbb = 0;
        for (int i = 0; i < a.Length; i++) { double da = a[i] - ma, db = b[i] - mb; sab += da * db; saa += da * da; sbb += db * db; }
        return saa < 1e-12 || sbb < 1e-12 ? 0 : sab / Math.Sqrt(saa * sbb);
    }
}

/// <summary>Cromosoma de 10 genes reales que se decodifican a <see cref="MappingParams"/>.</summary>
public sealed class MappingChromosome : IMultiObjectiveChromosome<MappingChromosome>, IRealVectorChromosome<MappingChromosome>
{
    //                                     smoothMel smoothRhy melThr gamma pitchLow pitchSpan rhy1 gap2 gap4 bassThr
    static readonly double[] Lo = { 0.00, 0.00, 0.00, 0.40, 0, 3, 0.00, 0.05, 0.05, 0.00 };
    static readonly double[] Hi = { 0.85, 0.85, 0.50, 2.50, 6, 15, 0.50, 0.40, 0.40, 0.50 };

    public required MusicEngine Engine { get; init; }
    public double[] Genes { get; private set; } = new double[Lo.Length];
    public double[] LowerBounds => Lo;
    public double[] UpperBounds => Hi;

    public void Randomize(GeneticRandom random)
    {
        for (int i = 0; i < Genes.Length; i++) Genes[i] = random.NextDouble(Lo[i], Hi[i]);
    }

    public MappingParams Decode()
    {
        int low = (int)Math.Floor(Genes[4] + 0.5);
        int span = Math.Min((int)Math.Floor(Genes[5] + 0.5), MusicEngine.ScaleSize - low);
        double r1 = Genes[6], r2 = r1 + Genes[7], r4 = r2 + Genes[8];
        return new MappingParams(R(Genes[0]), R(Genes[1]), R(Genes[2]), R(Genes[3]), low, span, R(r1), R(r2), R(r4), R(Genes[9]));
    }

    static double R(double v) => Math.Round(v, 4);

    public double[] EvaluateObjectives()
    {
        var (f, m) = Engine.Score(Decode());
        return [f, m];
    }

    public MappingChromosome Clone() => new() { Engine = Engine, Genes = (double[])Genes.Clone() };
}

public static class MappingOptimizer
{
    public const int MaxSteps = 256;

    public static string? Validate(SonificationFeatures f)
    {
        if (f.Steps < 4 || f.Steps > MaxSteps) return $"Steps debe estar entre 4 y {MaxSteps}.";
        foreach (var role in new[] { "melody", "rhythm", "bass" })
        {
            if (!f.Layers.TryGetValue(role, out var v)) return $"Falta la capa '{role}'.";
            if (v.Length != f.Steps) return $"La capa '{role}' debe tener {f.Steps} valores.";
            if (v.Any(x => !double.IsFinite(x) || x < 0 || x > 1)) return $"La capa '{role}' debe tener valores entre 0 y 1.";
        }
        return null;
    }

    public static ParetoResult Run(SonificationFeatures features, int generations = 250, int seed = 42, CancellationToken ct = default)
    {
        var engine = new MusicEngine(features);
        var sw = Stopwatch.StartNew();
        var config = new NSGA2Configuration<MappingChromosome>
        {
            PopulationSize = 120,
            Generations = generations,
            CrossoverProbability = 0.9,
            MutationProbability = 1.0,
            Seed = seed,
            MaxDegreeOfParallelism = -1,
            RemoveDuplicateObjectives = true,
            MaxDuration = TimeSpan.FromSeconds(20),
            Crossover = new SimulatedBinaryCrossover<MappingChromosome>(),
            Mutation = new PolynomialMutation<MappingChromosome>(),
        };

        var result = NSGA2Solver.Solve(new MappingChromosome { Engine = engine }, config, cancellationToken: ct);

        ParetoSolution Entry(MappingParams p, double f, double m)
        {
            var (mi, hits, _) = engine.Compile(p);
            return new ParetoSolution(p, Math.Round(f, 4), Math.Round(m, 4), mi, hits);
        }

        var front = result.ParetoFront
            .Where(i => i.IsFeasible)
            .Select(i => (P: i.Genes.Decode(), F: i.Objectives[0], M: i.Objectives[1]))
            .OrderBy(s => s.F)        // de más musical a más científico
            .ThenByDescending(s => s.M)
            .Select(s => Entry(s.P, s.F, s.M))
            .ToList();

        double hv = ParetoMetrics.Hypervolume2D(result.ParetoFront.Select(i => i.Objectives), [0.0, 0.0], minimize: [false, false]);
        var (mf, mm) = engine.Score(MappingParams.Manual);

        return new ParetoResult("HBA.Evolutionary 2.0.0 · NSGA-II", seed, result.GenerationsExecuted, result.Evaluations,
            Math.Round(hv, 4), Math.Round(sw.Elapsed.TotalMilliseconds), Entry(MappingParams.Manual, mf, mm), front);
    }
}
