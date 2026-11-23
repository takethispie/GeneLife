using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttractivenessCalibration;
using AttractivenessService;
using Genelife.Components.Gen;

const string DefaultOutput = "attraction-coefficients.json";

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

try
{
    return args[0] switch
    {
        "--verify" => Verify(),
        "--generate" => Generate(args.Length > 1 ? args[1] : DefaultOutput),
        _ => Unknown(args[0])
    };
}
catch (Exception exception)
{
    Console.Error.WriteLine($"error: {exception.Message}");
    return 1;
}

static int Unknown(string argument)
{
    Console.Error.WriteLine($"unknown argument: {argument}");
    PrintUsage();
    return 2;
}

static void PrintUsage()
{
    Console.WriteLine("""
        Attractiveness calibration tool (offline oracle; never loaded by the simulation).

        Usage:
          dotnet run --project tools/attractiveness -- --verify
          dotnet run --project tools/attractiveness -- --generate [output.json]

        --verify    Load the ML.NET model, check the adapter covers every input column,
                    and print a sample prediction.
        --generate  Generate a deterministic synthetic pair sample, label it with the
                    model, fit integer trait weights, and write the coefficient fixture.
        """);
}

static int Verify()
{
    var traits = new Traits
    {
        EyeColor = EyeColor.Green,
        HairColor = HairColor.Ginger,
        Handedness = Handedness.Ambidextrous,
        Morphotype = Morphotype.Endomorph,
        Aptitude = Aptitude.Science,
        HeightPotential = HeightPotential.Tall,
        Sex = Sex.Female,
        Temperament = Temperament.Violent
    };
    var partner = new Traits
    {
        EyeColor = EyeColor.Brown,
        HairColor = HairColor.Blonde,
        Handedness = Handedness.Ambidextrous,
        Morphotype = Morphotype.Mesomorph,
        Aptitude = Aptitude.Art,
        HeightPotential = HeightPotential.Tall,
        Sex = Sex.Male,
        Temperament = Temperament.Peaceful
    };

    var input = AttractivenessCalibrationAdapter.Map(traits, 79, partner, 42);
    var missing = MissingColumns(input);
    if (missing.Count > 0)
    {
        Console.Error.WriteLine($"adapter is missing columns: {string.Join(", ", missing)}");
        return 1;
    }

    var mismatches = RowMismatches(input);
    if (mismatches.Count > 0)
    {
        foreach (var mismatch in mismatches)
            Console.Error.WriteLine($"adapter row mismatch: {mismatch}");
        return 1;
    }

    var labels = Attractiveness.PredictAllLabels(input).ToList();
    var compatibility = labels.FirstOrDefault(pair => pair.Key == "1").Value;
    Console.WriteLine($"adapter columns: {input.GetType().GetProperties().Length - 1} mapped");
    Console.WriteLine($"adapter row: F_EyeColor={input.F_EyeColor}, F_HairType={input.F_HairType}, F_Handedness={input.F_Handedness}, F_Morphotype={input.F_Morphotype}, F_Intelligence={input.F_Intelligence}, F_HeightPotential={input.F_HeightPotential}, F_Age={input.F_Age}, F_Sex={input.F_Sex}, F_BehaviorPropension={input.F_BehaviorPropension}");
    Console.WriteLine($"sample labels: {string.Join(", ", labels.Select(pair => $"{pair.Key}={pair.Value:0.0000}"))}");
    Console.WriteLine($"compatibility probability: {compatibility:0.0000}");
    Console.WriteLine("model: loaded");
    return 0;
}

static int Generate(string outputPath)
{
    var sample = SyntheticPairs.Generate(seed: 20241123, pairs: 2_000);
    var labeled = sample.Select(pair => (pair, probability: PredictCompatibility(pair))).ToList();
    var fit = CalibrationFit.Fit(labeled);

    var fixture = new CalibrationFixture
    {
        Version = "v4-attraction-2",
        ScoreScale = 10_000,
        Components = new CalibrationComponents
        {
            TraitCompatibility = 4200,
            AgePreference = 1800,
            Proximity = 1200,
            Stability = 1500,
            Health = 800,
            Availability = 500
        },
        TraitWeights = new CalibrationTraitWeights
        {
            EyeColor = fit.Weights[0],
            Hair = fit.Weights[1],
            Handedness = fit.Weights[2],
            Morphotype = fit.Weights[3],
            Aptitude = fit.Weights[4],
            HeightPotential = fit.Weights[5],
            Temperament = fit.Weights[6]
        },
        Fit = new CalibrationFitReport
        {
            SamplePairs = labeled.Count,
            MeanAbsoluteError = fit.MeanAbsoluteError,
            MeanSquaredError = fit.MeanSquaredError,
            Agreement = fit.Agreement,
            Seed = 20241123
        }
    };

    var json = JsonSerializer.Serialize(fixture, JsonOptions.Instance);
    File.WriteAllText(outputPath, json + Environment.NewLine);
    Console.WriteLine($"wrote {outputPath}");
    Console.WriteLine($"pairs: {labeled.Count}");
    Console.WriteLine($"weights: {string.Join(", ", fit.Weights)}");
    Console.WriteLine($"MAE: {fit.MeanAbsoluteError:0.0000}");
    Console.WriteLine($"MSE: {fit.MeanSquaredError:0.0000}");
    Console.WriteLine($"agreement: {fit.Agreement:0.0000}");
    return 0;
}

static double PredictCompatibility((Traits First, int FirstAge, Traits Second, int SecondAge) pair)
{
    var input = AttractivenessCalibrationAdapter.Map(pair.First, pair.FirstAge, pair.Second, pair.SecondAge);
    var labels = Attractiveness.PredictAllLabels(input).ToList();
    return labels.FirstOrDefault(item => item.Key == "1").Value;
}

static List<string> RowMismatches(Attractiveness.ModelInput input)
{
    var expectedRow = new (string Name, object? Actual, object? Expected)[]
    {
        (nameof(Attractiveness.ModelInput.F_EyeColor), input.F_EyeColor, "Green"),
        (nameof(Attractiveness.ModelInput.F_HairType), input.F_HairType, "Ginger"),
        (nameof(Attractiveness.ModelInput.F_Handedness), input.F_Handedness, "Ambidextrous"),
        (nameof(Attractiveness.ModelInput.F_Morphotype), input.F_Morphotype, "Endomorph"),
        (nameof(Attractiveness.ModelInput.F_Intelligence), input.F_Intelligence, "Science"),
        (nameof(Attractiveness.ModelInput.F_HeightPotential), input.F_HeightPotential, "Tall"),
        (nameof(Attractiveness.ModelInput.F_Age), input.F_Age, 79f),
        (nameof(Attractiveness.ModelInput.F_Sex), input.F_Sex, "Female"),
        (nameof(Attractiveness.ModelInput.F_BehaviorPropension), input.F_BehaviorPropension, "Violent"),
        (nameof(Attractiveness.ModelInput.S_EyeColor), input.S_EyeColor, "Brown"),
        (nameof(Attractiveness.ModelInput.S_HairType), input.S_HairType, "Blonde"),
        (nameof(Attractiveness.ModelInput.S_Handedness), input.S_Handedness, "Ambidextrous"),
        (nameof(Attractiveness.ModelInput.S_Morphotype), input.S_Morphotype, "Mesomorph"),
        (nameof(Attractiveness.ModelInput.S_Intelligence), input.S_Intelligence, "Art"),
        (nameof(Attractiveness.ModelInput.S_HeightPotential), input.S_HeightPotential, "Tall"),
        (nameof(Attractiveness.ModelInput.S_Age), input.S_Age, 42f),
        (nameof(Attractiveness.ModelInput.S_Sex), input.S_Sex, "Male"),
        (nameof(Attractiveness.ModelInput.S_BehaviorPropension), input.S_BehaviorPropension, "Peaceful")
    };

    var mismatches = new List<string>();
    foreach (var (name, actual, expected) in expectedRow)
    {
        if (!Equals(actual, expected))
            mismatches.Add($"{name}: expected '{expected}', got '{actual}'");
    }

    return mismatches;
}

static List<string> MissingColumns(Attractiveness.ModelInput input)
{
    var missing = new List<string>();
    foreach (var property in typeof(Attractiveness.ModelInput).GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        if (property.Name == nameof(Attractiveness.ModelInput.Result))
            continue;

        var value = property.GetValue(input);
        if (value is null)
            missing.Add(property.Name);
    }

    return missing;
}

static class SyntheticPairs
{
    public static List<(Traits First, int FirstAge, Traits Second, int SecondAge)> Generate(int seed, int pairs)
    {
        var random = new Random(seed);
        var result = new List<(Traits, int, Traits, int)>(pairs);
        for (var i = 0; i < pairs; i++)
        {
            result.Add((
                RandomTraits(random),
                random.Next(18, 80),
                RandomTraits(random),
                random.Next(18, 80)));
        }

        return result;
    }

    private static Traits RandomTraits(Random random)
    {
        return new Traits
        {
            EyeColor = (EyeColor)random.Next(0, Enum.GetValues<EyeColor>().Length),
            HairColor = (HairColor)random.Next(0, Enum.GetValues<HairColor>().Length),
            Handedness = (Handedness)random.Next(0, Enum.GetValues<Handedness>().Length),
            Morphotype = (Morphotype)random.Next(0, Enum.GetValues<Morphotype>().Length),
            Aptitude = (Aptitude)random.Next(0, Enum.GetValues<Aptitude>().Length),
            HeightPotential = (HeightPotential)random.Next(0, Enum.GetValues<HeightPotential>().Length),
            Sex = (Sex)random.Next(0, Enum.GetValues<Sex>().Length),
            Temperament = (Temperament)random.Next(0, Enum.GetValues<Temperament>().Length)
        };
    }
}

static class CalibrationFit
{
    public static FitResult Fit(IReadOnlyList<((Traits First, int FirstAge, Traits Second, int SecondAge) Pair, double Probability)> labeled)
    {
        var weights = new[] { 100, 100, 100, 200, 200, 150, 250 };
        var best = Evaluate(weights, labeled);
        var bestWeights = (int[])weights.Clone();
        var improved = true;
        while (improved)
        {
            improved = false;
            for (var trait = 0; trait < weights.Length; trait++)
            {
                var original = weights[trait];
                foreach (var candidate in new[] { 0, 25, 50, 75, 100, 150, 200, 250, 300, 400, 500 })
                {
                    if (candidate == original)
                        continue;

                    weights[trait] = candidate;
                    var score = Evaluate(weights, labeled);
                    if (score.MeanSquaredError < best.MeanSquaredError)
                    {
                        best = score;
                        bestWeights = (int[])weights.Clone();
                        improved = true;
                    }
                }

                weights[trait] = original;
            }
        }

        return new FitResult(bestWeights, best.MeanAbsoluteError, best.MeanSquaredError, best.Agreement);
    }

    private static FitResult Evaluate(int[] weights, IReadOnlyList<((Traits First, int FirstAge, Traits Second, int SecondAge) Pair, double Probability)> labeled)
    {
        var totalWeight = weights.Sum();
        var absoluteError = 0.0;
        var squaredError = 0.0;
        var agree = 0;
        foreach (var (pair, probability) in labeled)
        {
            var matches = new[]
            {
                pair.First.EyeColor == pair.Second.EyeColor ? 1 : 0,
                pair.First.HairColor == pair.Second.HairColor ? 1 : 0,
                pair.First.Handedness == pair.Second.Handedness ? 1 : 0,
                pair.First.Morphotype == pair.Second.Morphotype ? 1 : 0,
                pair.First.Aptitude == pair.Second.Aptitude ? 1 : 0,
                pair.First.HeightPotential == pair.Second.HeightPotential ? 1 : 0,
                pair.First.Temperament == pair.Second.Temperament ? 1 : 0
            };
            var predicted = 0.0;
            for (var i = 0; i < weights.Length; i++)
                predicted += weights[i] * matches[i];
            predicted /= totalWeight;

            var error = predicted - probability;
            absoluteError += Math.Abs(error);
            squaredError += error * error;
            if ((predicted >= 0.5) == (probability >= 0.5))
                agree++;
        }

        var count = labeled.Count;
        return new FitResult(weights, absoluteError / count, squaredError / count, agree / (double)count);
    }

    public readonly record struct FitResult(int[] Weights, double MeanAbsoluteError, double MeanSquaredError, double Agreement);
}

sealed class CalibrationFixture
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "v4-attraction-2";

    [JsonPropertyName("scoreScale")]
    public int ScoreScale { get; init; } = 10_000;

    [JsonPropertyName("components")]
    public CalibrationComponents Components { get; init; } = new();

    [JsonPropertyName("traitWeights")]
    public CalibrationTraitWeights TraitWeights { get; init; } = new();

    [JsonPropertyName("fit")]
    public CalibrationFitReport Fit { get; init; } = new();
}

sealed class CalibrationComponents
{
    [JsonPropertyName("traitCompatibility")]
    public int TraitCompatibility { get; init; }

    [JsonPropertyName("agePreference")]
    public int AgePreference { get; init; }

    [JsonPropertyName("proximity")]
    public int Proximity { get; init; }

    [JsonPropertyName("stability")]
    public int Stability { get; init; }

    [JsonPropertyName("health")]
    public int Health { get; init; }

    [JsonPropertyName("availability")]
    public int Availability { get; init; }
}

sealed class CalibrationTraitWeights
{
    [JsonPropertyName("eyeColor")]
    public int EyeColor { get; init; }

    [JsonPropertyName("hair")]
    public int Hair { get; init; }

    [JsonPropertyName("handedness")]
    public int Handedness { get; init; }

    [JsonPropertyName("morphotype")]
    public int Morphotype { get; init; }

    [JsonPropertyName("aptitude")]
    public int Aptitude { get; init; }

    [JsonPropertyName("heightPotential")]
    public int HeightPotential { get; init; }

    [JsonPropertyName("temperament")]
    public int Temperament { get; init; }
}

sealed class CalibrationFitReport
{
    [JsonPropertyName("samplePairs")]
    public int SamplePairs { get; init; }

    [JsonPropertyName("meanAbsoluteError")]
    public double MeanAbsoluteError { get; init; }

    [JsonPropertyName("meanSquaredError")]
    public double MeanSquaredError { get; init; }

    [JsonPropertyName("agreement")]
    public double Agreement { get; init; }

    [JsonPropertyName("seed")]
    public int Seed { get; init; }
}

static class JsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
