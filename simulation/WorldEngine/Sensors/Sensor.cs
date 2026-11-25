using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Units;
using WorldEngine.World;

namespace WorldEngine.Sensors;

public sealed class Sensor
{
    private readonly HashSet<UnitKind> detectable;

    public int Id { get; }
    public SensorKind Kind { get; }
    public CountryId Owner { get; }
    public Vector2 Position { get; }
    public float Range { get; }
    public double BaseDetection { get; }
    public float BaseNoise { get; }
    public int DelayTicks { get; }
    public double ClassificationAccuracy { get; }

    public Sensor(
        int id,
        SensorKind kind,
        CountryId owner,
        Vector2 position,
        float range,
        double baseDetection,
        float baseNoise,
        int delayTicks,
        double classificationAccuracy,
        IEnumerable<UnitKind> detectable)
    {
        if (range <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(range));
        }
        Id = id;
        Kind = kind;
        Owner = owner;
        Position = position;
        Range = range;
        BaseDetection = baseDetection;
        BaseNoise = baseNoise;
        DelayTicks = delayTicks;
        ClassificationAccuracy = classificationAccuracy;
        this.detectable = new HashSet<UnitKind>(detectable);
    }

    public IReadOnlyList<Observation> Sense(WorldState world, Random rng)
    {
        var tick = world.Clock.Tick;
        var result = new List<Observation>();
        foreach (var unit in world.Units)
        {
            if (unit.Owner == Owner || unit.Status == UnitStatus.Destroyed || !detectable.Contains(unit.Kind))
            {
                continue;
            }
            var distance = Vector2.Distance(Position, unit.Position);
            if (distance > Range)
            {
                continue;
            }
            var ratio = distance / Range;
            var probability = BaseDetection * (1 - ratio * ratio);
            if (rng.NextDouble() >= probability)
            {
                continue;
            }
            var sigma = BaseNoise * (1 + ratio);
            var noisy = new Vector2(
                unit.Position.X + (float)(Gaussian(rng) * sigma),
                unit.Position.Y + (float)(Gaussian(rng) * sigma));
            var classified = rng.NextDouble() < ClassificationAccuracy
                ? unit.Kind
                : RandomOtherKind(unit.Kind, rng);
            var confidence = ratio > 0.8f
                ? ObservationConfidence.Suspected
                : Kind == SensorKind.Optical && ratio <= 0.5f
                    ? ObservationConfidence.Confirmed
                    : ObservationConfidence.Estimated;
            CountryId? owner = Kind == SensorKind.Optical && ratio <= 0.7f ? unit.Owner : null;
            result.Add(new Observation(
                Id, Kind, tick, tick + DelayTicks, noisy, sigma, confidence, classified, owner));
        }
        return result;
    }

    private static double Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static readonly UnitKind[] ReportableKinds = Enum.GetValues<UnitKind>()
        .Where(k => k is not (UnitKind.Armor or UnitKind.Naval or UnitKind.Air or UnitKind.SupplyConvoy))
        .ToArray();

    private static UnitKind RandomOtherKind(UnitKind actual, Random rng)
    {
        var kinds = ReportableKinds.Where(k => k != actual).ToArray();
        return kinds[rng.Next(kinds.Length)];
    }
}
