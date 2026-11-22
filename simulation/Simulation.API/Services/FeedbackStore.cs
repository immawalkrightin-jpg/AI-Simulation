using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Simulation.API.Contracts.Feedback;

namespace Simulation.API.Services;

public sealed record FeedbackResult(CorrectionDto? Correction, string? Error);

public sealed partial class FeedbackStore
{
    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "target_missed",
        "wrong_target_assessment",
        "friendly_fire",
        "moral_judgement",
        "tactical_mistake",
        "supply_allocation",
        "supply_transport"
    };

    private const int MaxDescription = 2000;
    private const int MaxNumber = 999_999_999;
    private const string FileName = "corrections.jsonl";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly object gate = new();
    private readonly string path;

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_-]{0,39}\z")]
    private static partial Regex OperationPattern();

    public FeedbackStore(IOptions<SimulationOptions> options)
    {
        path = Path.Combine(ResolveDirectory(options.Value.FeedbackPath), FileName);
    }

    public string FilePath => path;

    public FeedbackResult Add(CorrectionInput input, long tick, int seed)
    {
        var operation = input.Operation ?? string.Empty;
        if (!OperationPattern().IsMatch(operation))
        {
            return Fail("Operation must be a name of letters, digits, dashes or underscores.");
        }
        if (input.Number < 0 || input.Number > MaxNumber)
        {
            return Fail("Number is out of range.");
        }
        if (input.Category is null || !Categories.Contains(input.Category, StringComparer.Ordinal))
        {
            return Fail("Unknown category.");
        }
        var description = (input.Description ?? string.Empty).Trim();
        if (description.Length == 0 || description.Length > MaxDescription)
        {
            return Fail($"Description must be 1 to {MaxDescription} characters.");
        }

        var correction = new CorrectionDto(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            operation,
            input.Number,
            input.Category,
            description,
            tick,
            seed);
        var line = JsonSerializer.Serialize(correction, Json);
        lock (gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, line + Environment.NewLine);
        }
        return new FeedbackResult(correction, null);
    }

    public IReadOnlyList<CorrectionDto> Recent(int count)
    {
        lock (gate)
        {
            if (!File.Exists(path))
            {
                return Array.Empty<CorrectionDto>();
            }
            var items = new List<CorrectionDto>();
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                try
                {
                    var item = JsonSerializer.Deserialize<CorrectionDto>(line, Json);
                    if (item is not null)
                    {
                        items.Add(item);
                    }
                }
                catch (JsonException)
                {
                }
            }
            return items.Skip(Math.Max(0, items.Count - count)).ToList();
        }
    }

    private static FeedbackResult Fail(string error) => new(null, error);

    private static string ResolveDirectory(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "data", "feedback");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        return Path.Combine(AppContext.BaseDirectory, "feedback");
    }
}
