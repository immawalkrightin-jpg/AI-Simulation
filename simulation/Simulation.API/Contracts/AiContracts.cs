namespace Simulation.API.Contracts.Ai;

public sealed record AssetSensorDto(
    int Id,
    string Kind,
    float X,
    float Y,
    float Range);

public sealed record AssetPicture(
    long Tick,
    float Width,
    float Height,
    IReadOnlyList<AssetSensorDto> Sensors);

public sealed record ClockDto(long Tick);

public sealed record OperationalPointInput(
    string Kind,
    string Confidence,
    float X,
    float Y,
    float Radius,
    string? Label,
    IReadOnlyList<float>? Path);

public sealed record OperationalPointDto(
    string Id,
    string Kind,
    string Confidence,
    float X,
    float Y,
    float Radius,
    string Label,
    IReadOnlyList<float> Path,
    long CreatedTick,
    long UpdatedTick);

public sealed record OperationalMapDto(
    long Version,
    IReadOnlyList<OperationalPointDto> Points);
