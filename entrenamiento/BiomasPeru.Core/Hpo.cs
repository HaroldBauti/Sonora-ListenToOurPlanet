using System.Collections.Concurrent;
using System.Diagnostics;
using Hba.NeuralNet;
using Hba.NeuralNet.Initializers;
using Hba.NeuralNet.Optimizers;
using HBA.Evolutionary.Core;
using HBA.Evolutionary.MultiObjective;
using HBA.Evolutionary.Operators;

namespace BiomasPeru;

/// <summary>Hiperparámetros de la red (lo que el algoritmo evolutivo busca).</summary>
public sealed record HyperParams(double LearningRate, int[] Hidden, ActivationType Activation, int BatchSize, double L2, OptimizerType Optimizer)
{
    public int Parameters(int input, int output)
    {
        int p = 0, prev = input;
        foreach (var h in Hidden.Append(output)) { p += prev * h + h; prev = h; }
        return p;
    }

    public Perceptron Build(int seed) => new(Biomas.Dim,
        [.. Hidden.Select(h => new LayerConfig(h, Activation)), new LayerConfig(Biomas.K, ActivationType.Softmax)],
        InitializerType.Auto, seed);

    public NeuralNetConfig Config(int maxIterations, int patience) => new()
    {
        LearningRate = LearningRate,
        MaxIterations = maxIterations,
        BatchSize = BatchSize,
        L2Lambda = L2,
        Optimizer = Optimizer,
        LossFunction = LossFunctionType.CategoricalCrossEntropy,
        EarlyStopping = true,
        Patience = patience,
        EvaluationInterval = 1,
        RestoreBestWeights = true,
        MaxError = 0,
    };

    public override string ToString() =>
        $"capas [{string.Join("-", Hidden)}] {Activation}, {Optimizer} lr={LearningRate:G3}, lote={BatchSize}, L2={L2:G2}";

    /// <summary>Configuración "razonable a mano", sin optimizar: la línea base contra la que se compara.</summary>
    public static readonly HyperParams Baseline = new(0.01, [32], ActivationType.ReLU, 32, 0, OptimizerType.Adam);
}

public sealed record Datasets(List<double[]> TrainX, List<double[]> TrainY, List<double[]> ValX, List<double[]> ValY, List<int> ValClass);

/// <summary>Cromosoma de 7 genes reales que se decodifican a <see cref="HyperParams"/>.</summary>
public sealed class HyperChromosome : IMultiObjectiveChromosome<HyperChromosome>, IRealVectorChromosome<HyperChromosome>
{
    //                                     log10 lr  capas  log2 neuronas  activación  log2 lote  log10 L2  optimizador
    static readonly double[] Lo = { -3.5, 0.5, 3.0, 0.0, 4.0, -6.0, 0.0 };
    static readonly double[] Hi = { -1.0, 3.49, 7.0, 1.0, 8.0, -2.0, 2.99 };

    public required Evaluator Evaluator { get; init; }
    public double[] Genes { get; private set; } = new double[Lo.Length];
    public double[] LowerBounds => Lo;
    public double[] UpperBounds => Hi;

    public void Randomize(GeneticRandom random)
    {
        for (int i = 0; i < Genes.Length; i++) Genes[i] = random.NextDouble(Lo[i], Hi[i]);
    }

    public HyperParams Decode()
    {
        double G(int i) => Math.Clamp(Genes[i], Lo[i], Hi[i]);
        int layers = (int)Math.Round(G(1));
        int first = (int)Math.Round(Math.Pow(2, G(2)));
        var hidden = Enumerable.Range(0, layers).Select(l => Math.Max(4, first >> l)).ToArray(); // pirámide: n, n/2, n/4
        return new HyperParams(
            Math.Round(Math.Pow(10, G(0)), 5),
            hidden,
            G(3) < 0.5 ? ActivationType.ReLU : ActivationType.Tanh,
            (int)Math.Round(Math.Pow(2, G(4))),
            Math.Round(Math.Pow(10, G(5)), 7),
            (OptimizerType)(int)Math.Floor(G(6)));
    }

    public double[] EvaluateObjectives() => Evaluator.Evaluate(Decode());
    public double ConstraintViolation() => 0;
    public HyperChromosome Clone() => new() { Evaluator = Evaluator, Genes = (double[])Genes.Clone() };
}

/// <summary>
/// Evaluación barata ("baja fidelidad"): subconjunto de datos, pocas iteraciones y parada temprana.
/// Caché por hiperparámetros decodificados para no reentrenar duplicados.
/// </summary>
public sealed class Evaluator(Datasets data, int iterations, int patience, int seed)
{
    readonly ConcurrentDictionary<string, double[]> _cache = new();
    public int Trainings;
    public int CacheHits;
    public readonly ConcurrentBag<(HyperParams P, double BalancedAccuracy, int Params, double Seconds)> Log = [];

    public double[] Evaluate(HyperParams p)
    {
        string key = p.ToString();
        if (_cache.TryGetValue(key, out var hit)) { Interlocked.Increment(ref CacheHits); return hit; }

        var sw = Stopwatch.StartNew();
        var net = p.Build(seed);
        // Copia propia de los datos: la librería podría normalizar en sitio y hay entrenamientos en paralelo.
        net.Train(Copy(data.TrainX), Copy(data.TrainY), Copy(data.ValX), Copy(data.ValY), p.Config(iterations, patience), CancellationToken.None);
        double bacc = Metrics.BalancedAccuracy(net, data.ValX, data.ValClass);
        int prm = p.Parameters(Biomas.Dim, Biomas.K);
        Interlocked.Increment(ref Trainings);
        Log.Add((p, bacc, prm, sw.Elapsed.TotalSeconds));

        double[] obj = [1 - bacc, Math.Log10(prm)];
        _cache[key] = obj;
        return obj;
    }

    static List<double[]> Copy(List<double[]> l) => l.Select(r => (double[])r.Clone()).ToList();

    /// <summary>Estado de la búsqueda al terminar cada generación (para mostrar el progreso).</summary>
    public sealed record GenerationInfo(int Generation, TimeSpan Elapsed, int Trainings, int CacheHits, double? BestBalancedAccuracy, HyperParams? Best);

    /// <param name="onGeneration">Se llama al terminar cada generación. Si es null, se escribe una línea en la consola.</param>
    public static NSGA2Result<HyperChromosome> Run(Evaluator ev, int population, int generations, TimeSpan maxDuration, int seed,
                                                   Action<GenerationInfo>? onGeneration = null)
    {
        var config = new NSGA2Configuration<HyperChromosome>
        {
            PopulationSize = population,
            Generations = generations,
            CrossoverProbability = 0.9,
            MutationProbability = 1.0,
            Seed = seed,
            MaxDegreeOfParallelism = -1,
            ReuseUnchangedEvaluations = true,
            RemoveDuplicateObjectives = true,
            MaxDuration = maxDuration,
            MinimizeObjectives = [true, true],
            Crossover = new SimulatedBinaryCrossover<HyperChromosome>(),
            Mutation = new PolynomialMutation<HyperChromosome>(),
        };
        var sw = Stopwatch.StartNew();
        return NSGA2Solver.Solve(new HyperChromosome { Evaluator = ev }, config, (gen, pop) =>
        {
            var best = pop.Where(i => i.IsEvaluated).MinBy(i => i.Objectives[0]);
            if (onGeneration is not null)
                onGeneration(new GenerationInfo(gen, sw.Elapsed, ev.Trainings, ev.CacheHits,
                    best is null ? null : 1 - best.Objectives[0], best?.Genes.Decode()));
            else
                Console.WriteLine($"  gen {gen,2} | {sw.Elapsed:mm\\:ss} | entrenamientos {ev.Trainings,3} (caché {ev.CacheHits}) | " +
                                  $"mejor exactitud equilibrada {1 - best?.Objectives[0]:P1} → {best?.Genes.Decode()}");
        }, CancellationToken.None);
    }
}

public static class Metrics
{
    public static int ArgMax(double[] v) { int b = 0; for (int i = 1; i < v.Length; i++) if (v[i] > v[b]) b = i; return b; }

    public static int[,] Confusion(Perceptron net, List<double[]> x, List<int> y)
    {
        var c = new int[Biomas.K, Biomas.K];
        var pred = new int[x.Count];
        Parallel.For(0, x.Count, i => pred[i] = ArgMax(net.Predict(x[i])));
        for (int i = 0; i < x.Count; i++) c[y[i], pred[i]]++;
        return c;
    }

    public static double[] Recall(int[,] c) => Enumerable.Range(0, Biomas.K).Select(k =>
    {
        int n = 0; for (int j = 0; j < Biomas.K; j++) n += c[k, j];
        return n == 0 ? double.NaN : c[k, k] / (double)n;
    }).ToArray();

    public static double BalancedAccuracy(Perceptron net, List<double[]> x, List<int> y) =>
        Recall(Confusion(net, x, y)).Where(r => !double.IsNaN(r)).Average();

    public static double Accuracy(int[,] c)
    {
        int ok = 0, n = 0;
        for (int i = 0; i < Biomas.K; i++) for (int j = 0; j < Biomas.K; j++) { n += c[i, j]; if (i == j) ok += c[i, j]; }
        return ok / (double)n;
    }
}
