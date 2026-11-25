using System.Numerics;
using WorldEngine.Interactions;
using WorldEngine.Units;

namespace WorldEngine.Routing;

public readonly record struct RoutePosition(Vector2 Point, int LegIndex, UnitKind Carrier);

public sealed class RouteLeg
{
    public RouteLeg(UnitKind carrier, IReadOnlyList<Vector2> points)
    {
        if (points.Count < 2)
        {
            throw new ArgumentException("A leg needs at least two points.", nameof(points));
        }
        Carrier = carrier;
        Points = points;
        var length = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            length += Vector2.Distance(points[i - 1], points[i]);
        }
        Length = length;
    }

    public UnitKind Carrier { get; }
    public IReadOnlyList<Vector2> Points { get; }
    public double Length { get; }
    public double StartFraction { get; internal set; }
    public double EndFraction { get; internal set; }

    public TransportMode Mode => Carrier switch
    {
        UnitKind.Truck => TransportMode.Ground,
        UnitKind.CargoShip => TransportMode.Naval,
        _ => TransportMode.Air
    };
}

public sealed class ShipmentRoute
{
    public ShipmentRoute(IReadOnlyList<RouteLeg> legs, int exposure)
    {
        if (legs.Count == 0)
        {
            throw new ArgumentException("A route needs at least one leg.", nameof(legs));
        }
        Legs = legs;
        Exposure = exposure;
        var times = legs.Select(l => Math.Max(1e-6, l.Length) / SpeedOf(l.Carrier)).ToArray();
        TotalTicks = times.Sum();
        TotalLength = legs.Sum(l => l.Length);
        var elapsed = 0.0;
        for (var i = 0; i < legs.Count; i++)
        {
            legs[i].StartFraction = elapsed / TotalTicks;
            elapsed += times[i];
            legs[i].EndFraction = i == legs.Count - 1 ? 1.0 : elapsed / TotalTicks;
        }
    }

    public IReadOnlyList<RouteLeg> Legs { get; }
    public double TotalLength { get; }
    public double TotalTicks { get; }
    public int Exposure { get; }

    public Vector2 Start => Legs[0].Points[0];
    public Vector2 End => Legs[^1].Points[^1];

    public IReadOnlyList<Vector2> HandOffs => Legs.Skip(1).Select(l => l.Points[0]).ToList();

    public static double SpeedOf(UnitKind carrier) => carrier switch
    {
        UnitKind.Truck => 25.0,
        UnitKind.CargoShip => 18.0,
        UnitKind.Helicopter => 45.0,
        _ => 70.0
    };

    public int LegIndexAt(double progress) => At(progress).LegIndex;

    public RoutePosition At(double progress)
    {
        var p = Math.Clamp(progress, 0.0, 1.0);
        var index = 0;
        while (index < Legs.Count - 1 && p > Legs[index].EndFraction)
        {
            index++;
        }
        var leg = Legs[index];
        var span = leg.EndFraction - leg.StartFraction;
        var local = span <= 1e-12 ? 1.0 : Math.Clamp((p - leg.StartFraction) / span, 0.0, 1.0);
        var remaining = local * leg.Length;
        for (var i = 1; i < leg.Points.Count; i++)
        {
            var segment = Vector2.Distance(leg.Points[i - 1], leg.Points[i]);
            if (remaining <= segment || i == leg.Points.Count - 1)
            {
                var t = segment <= 1e-9 ? 1f : (float)Math.Min(1.0, remaining / segment);
                return new RoutePosition(Vector2.Lerp(leg.Points[i - 1], leg.Points[i], t), index, leg.Carrier);
            }
            remaining -= segment;
        }
        return new RoutePosition(leg.Points[^1], index, leg.Carrier);
    }
}
