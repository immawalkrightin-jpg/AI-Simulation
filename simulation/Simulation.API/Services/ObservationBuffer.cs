using Simulation.API.Contracts.Observations;
using WorldEngine.Sensors;

namespace Simulation.API.Services;

public sealed class ObservationBuffer
{
    private const int Capacity = 5000;

    private readonly object gate = new();
    private readonly List<ObservationDto> items = new();
    private long sequence;

    public ObservationBatch Append(long tick, IReadOnlyList<Observation> observations)
    {
        lock (gate)
        {
            var batch = new List<ObservationDto>();
            foreach (var observation in observations)
            {
                sequence++;
                batch.Add(ObservationMapper.ToDto(sequence, observation));
            }
            items.AddRange(batch);
            if (items.Count > Capacity)
            {
                items.RemoveRange(0, items.Count - Capacity);
            }
            return new ObservationBatch(tick, batch);
        }
    }

    public ObservationPage ReadAfter(long after)
    {
        lock (gate)
        {
            var page = items.Where(i => i.Sequence > after).ToList();
            return new ObservationPage(sequence, page);
        }
    }
}
