using Simulation.API.Contracts.Observations;
using WorldEngine.Sensors;

namespace Simulation.API.Services;

public static class ObservationMapper
{
    public static ObservationDto ToDto(long sequence, Observation observation) =>
        new(
            sequence,
            observation.SensorId,
            observation.Source.ToString(),
            observation.ObservedTick,
            observation.DeliveryTick,
            observation.Position.X,
            observation.Position.Y,
            observation.PositionSigma,
            observation.Confidence.ToString(),
            observation.ClassifiedKind?.ToString(),
            observation.ClassifiedOwner?.Value);
}
