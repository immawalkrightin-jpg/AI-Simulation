using System.Text.RegularExpressions;
using Simulation.API.Contracts.Ai;

namespace Simulation.API.Services;

public enum OperationalPointKind
{
    ExpectedActivity,
    SuspectedTarget,
    Surveillance,
    ArtilleryEngagement,
    SupplyRoute,
    IntelligenceRequired,
    Tracking
}

public enum PointConfidence
{
    Confirmed,
    Estimated,
    Suspected,
    Predicted
}

public sealed record OperationalResult(OperationalMapDto? Map, string? Error);

public sealed partial class OperationalMap
{
    private const int Capacity = 500;
    private const int MaxPathValues = 128;
    private const int MaxLabel = 80;
    private const float MaxRadius = 500;

    private readonly object gate = new();
    private readonly Dictionary<string, OperationalPointDto> points = new(StringComparer.Ordinal);
    private long version;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex IdPattern();

    public OperationalMapDto Snapshot()
    {
        lock (gate)
        {
            return Build();
        }
    }

    public OperationalResult Upsert(string id, OperationalPointInput input, long tick, float width, float height)
    {
        if (!IdPattern().IsMatch(id))
        {
            return Fail("Id must be 1 to 40 letters, digits, dashes or underscores.");
        }
        if (!TryEnum(input.Kind, out OperationalPointKind kind))
        {
            return Fail("Unknown point kind.");
        }
        if (!TryEnum(input.Confidence, out PointConfidence confidence))
        {
            return Fail("Unknown confidence.");
        }
        if (!Inside(input.X, input.Y, width, height))
        {
            return Fail("Position is outside the map.");
        }
        if (!float.IsFinite(input.Radius) || input.Radius < 0 || input.Radius > MaxRadius)
        {
            return Fail("Radius is out of range.");
        }
        var label = (input.Label ?? string.Empty).Trim();
        if (label.Length > MaxLabel)
        {
            return Fail("Label is too long.");
        }
        var path = input.Path ?? Array.Empty<float>();
        if (path.Count % 2 != 0 || path.Count > MaxPathValues)
        {
            return Fail("Path must hold coordinate pairs.");
        }
        for (var i = 0; i < path.Count; i += 2)
        {
            if (!Inside(path[i], path[i + 1], width, height))
            {
                return Fail("Path leaves the map.");
            }
        }

        lock (gate)
        {
            var exists = points.TryGetValue(id, out var current);
            if (!exists && points.Count >= Capacity)
            {
                return Fail("Point capacity reached.");
            }
            points[id] = new OperationalPointDto(
                id,
                kind.ToString(),
                confidence.ToString(),
                input.X,
                input.Y,
                input.Radius,
                label,
                path.ToList(),
                exists ? current!.CreatedTick : tick,
                tick);
            version++;
            return new OperationalResult(Build(), null);
        }
    }

    public OperationalMapDto? Remove(string id)
    {
        lock (gate)
        {
            if (!points.Remove(id))
            {
                return null;
            }
            version++;
            return Build();
        }
    }

    private OperationalMapDto Build() =>
        new(
            version,
            points.Values
                .OrderBy(p => p.CreatedTick)
                .ThenBy(p => p.Id, StringComparer.Ordinal)
                .ToList());

    private static OperationalResult Fail(string error) => new(null, error);

    private static bool Inside(float x, float y, float width, float height) =>
        float.IsFinite(x) && float.IsFinite(y) && x >= 0 && y >= 0 && x <= width && y <= height;

    private static bool TryEnum<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        return !string.IsNullOrEmpty(value)
            && char.IsLetter(value[0])
            && Enum.TryParse(value, true, out result)
            && Enum.IsDefined(result);
    }
}
