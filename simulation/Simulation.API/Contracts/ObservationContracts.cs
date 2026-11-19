namespace Simulation.API.Contracts.Observations;

public sealed record ObservationDto(
    long Sequence,
    int SensorId,
    string Source,
    long ObservedTick,
    long DeliveryTick,
    float X,
    float Y,
    float PositionSigma,
    string Confidence,
    string? ClassifiedKind,
    string? ClassifiedOwner);

public sealed record ObservationBatch(
    long Tick,
    IReadOnlyList<ObservationDto> Observations);

public sealed record ObservationPage(
    long LatestSequence,
    IReadOnlyList<ObservationDto> Observations);
