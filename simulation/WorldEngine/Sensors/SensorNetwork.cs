using WorldEngine.World;

namespace WorldEngine.Sensors;

public sealed class SensorNetwork : IObservationSource
{
    private readonly List<Sensor> sensors = new();
    private readonly Random rng;
    private readonly PriorityQueue<Observation, (long Tick, long Sequence)> pending = new();
    private long sequence;

    public SensorNetwork(int seed)
    {
        rng = new Random(seed);
    }

    public IReadOnlyList<Sensor> Sensors => sensors.OrderBy(s => s.Id).ToList();

    public void Add(Sensor sensor)
    {
        if (sensors.Any(s => s.Id == sensor.Id))
        {
            throw new InvalidOperationException($"Sensor {sensor.Id} already exists.");
        }
        sensors.Add(sensor);
    }

    public void Sense(WorldState world)
    {
        foreach (var sensor in sensors.OrderBy(s => s.Id))
        {
            foreach (var observation in sensor.Sense(world, rng))
            {
                pending.Enqueue(observation, (observation.DeliveryTick, sequence++));
            }
        }
    }

    public IReadOnlyList<Observation> Collect(long tick)
    {
        var due = new List<Observation>();
        while (pending.TryPeek(out _, out var priority) && priority.Tick <= tick)
        {
            due.Add(pending.Dequeue());
        }
        return due;
    }
}
