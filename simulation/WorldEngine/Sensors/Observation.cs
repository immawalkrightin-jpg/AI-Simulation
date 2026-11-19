using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Units;

namespace WorldEngine.Sensors;

public sealed record Observation(
    int SensorId,
    SensorKind Source,
    long ObservedTick,
    long DeliveryTick,
    Vector2 Position,
    float PositionSigma,
    ObservationConfidence Confidence,
    UnitKind? ClassifiedKind,
    CountryId? ClassifiedOwner);
