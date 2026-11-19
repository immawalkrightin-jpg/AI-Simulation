namespace Simulation.API.Contracts.GroundTruth;

public sealed record CountryDto(
    string Id,
    string Name,
    float X,
    float Y,
    double Supplies,
    double Ammunition);

public sealed record UnitDto(
    int Id,
    string Owner,
    string Kind,
    float X,
    float Y,
    float? DestinationX,
    float? DestinationY,
    double Speed,
    double Strength,
    string Status);

public sealed record RelationDto(
    string First,
    string Second,
    double Value,
    string State);

public sealed record SensorDto(
    int Id,
    string Kind,
    string Owner,
    float X,
    float Y,
    float Range);

public sealed record EventDto(
    long Tick,
    string Kind,
    string Description);

public sealed record GroundTruthSnapshot(
    long Tick,
    double ElapsedSeconds,
    float Width,
    float Height,
    IReadOnlyList<CountryDto> Countries,
    IReadOnlyList<UnitDto> Units,
    IReadOnlyList<RelationDto> Relations,
    IReadOnlyList<SensorDto> Sensors,
    IReadOnlyList<EventDto> Events);

public sealed record SimulationStatus(long Tick, bool Paused);
