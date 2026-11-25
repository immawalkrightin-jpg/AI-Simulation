namespace Simulation.API.Contracts.GroundTruth;

public sealed record CountryDto(
    string Id,
    string Name,
    float X,
    float Y,
    double Supplies,
    double Ammunition,
    string Bloc,
    bool IsModel,
    double Power = 0);

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

public sealed record RouteLegDto(
    string Carrier,
    string Mode,
    double Start,
    double End,
    IReadOnlyList<float> Points);

public sealed record RouteDto(
    int Exposure,
    IReadOnlyList<RouteLegDto> Legs);

public sealed record InteractionDto(
    int Id,
    string Kind,
    string Source,
    string Target,
    string Mode,
    string Status,
    double Cargo,
    string Subject,
    long StartTick,
    long DueTick,
    long? EndTick,
    string? Carrier = null,
    RouteDto? Route = null,
    string? Cause = null,
    string? Hunter = null);

public sealed record FrontDto(
    string Name,
    string First,
    string Second,
    string Rung,
    string Ceiling,
    string Reason);

public sealed record ZoneDto(
    int Id,
    string Owner,
    float X,
    float Y,
    float Radius,
    double Severity,
    long StartTick,
    long RepairTick,
    string Cause);

public sealed record OccupationDto(
    int Id,
    string Holder,
    string Loser,
    float X,
    float Y,
    float Radius,
    long StartTick,
    string Cause);

public sealed record GroundTruthSnapshot(
    long Tick,
    double ElapsedSeconds,
    float Width,
    float Height,
    IReadOnlyList<CountryDto> Countries,
    IReadOnlyList<UnitDto> Units,
    IReadOnlyList<RelationDto> Relations,
    IReadOnlyList<SensorDto> Sensors,
    IReadOnlyList<EventDto> Events,
    IReadOnlyList<InteractionDto> Interactions,
    IReadOnlyList<FrontDto>? Fronts = null,
    IReadOnlyList<ZoneDto>? Zones = null,
    IReadOnlyList<OccupationDto>? Occupations = null);

public sealed record SimulationStatus(long Tick, bool Paused, int Seed = 0, double Speed = 1);
