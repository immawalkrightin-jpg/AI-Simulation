using System.Collections.Concurrent;
using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Interactions;
using WorldEngine.Territory;
using WorldEngine.Units;

namespace WorldEngine.Routing;

public sealed record Port(string Id, CountryId Owner, string Name, Vector2 Berth);

public sealed record Canal(string Name, Vector2 From, Vector2 To);

public sealed class RouteNetwork
{
    public const double HostileThreshold = -0.3;
    private const float Blocked = float.PositiveInfinity;
    private const int SmoothLookahead = 40;
    private const float AnchorSearch = 80f;
    private const float LandingSearch = 40f;
    private const float HarbourSearch = 40f;

    private enum Medium
    {
        Sea,
        Land,
        Air
    }

    private sealed record PathResult(Vector2[] Points, int Exposure);

    [ThreadStatic] private static float[]? gScore;
    [ThreadStatic] private static int[]? parents;
    [ThreadStatic] private static int[]? seen;
    [ThreadStatic] private static int[]? closed;
    [ThreadStatic] private static int stamp;

    private readonly TerritoryMap map;
    private readonly int columns;
    private readonly int rows;
    private readonly float cellSize;
    private readonly byte[] kind;
    private readonly short[] owner;
    private readonly bool[] canalCell;
    private readonly Dictionary<string, short> ownerIndex = new();
    private readonly List<Port> ports;
    private readonly List<Canal> canals;
    private readonly ConcurrentDictionary<string, PathResult?> cache = new();
    private readonly ConcurrentDictionary<string, Vector2?> anchors = new();

    public RouteNetwork(TerritoryMap map, IEnumerable<Port> ports, IEnumerable<Canal>? canals = null)
    {
        this.map = map;
        columns = map.Columns;
        rows = map.Rows;
        cellSize = map.CellSize;
        kind = new byte[columns * rows];
        owner = new short[columns * rows];
        canalCell = new bool[columns * rows];
        for (var i = 0; i < kind.Length; i++)
        {
            var center = Center(i);
            if (map.IsSea(center))
            {
                kind[i] = 0;
                owner[i] = -1;
                continue;
            }
            var id = map.OwnerAt(center);
            if (id is null)
            {
                kind[i] = 1;
                owner[i] = -1;
                continue;
            }
            if (!ownerIndex.TryGetValue(id.Value.Value, out var index))
            {
                index = (short)ownerIndex.Count;
                if (index >= 64)
                {
                    throw new InvalidOperationException("The route network supports at most 64 territory owners.");
                }
                ownerIndex[id.Value.Value] = index;
            }
            kind[i] = 2;
            owner[i] = index;
        }
        this.canals = (canals ?? Array.Empty<Canal>()).ToList();
        foreach (var canal in this.canals)
        {
            Carve(canal);
        }
        this.ports = ports
            .Select(p => Harbour(p with { Berth = Center(Index(p.Berth)) }))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
    }

    public IReadOnlyList<Port> Ports => ports;
    public IReadOnlyList<Canal> Canals => canals;

    private int Index(Vector2 point)
    {
        var column = Math.Clamp((int)(point.X / cellSize), 0, columns - 1);
        var row = Math.Clamp((int)(point.Y / cellSize), 0, rows - 1);
        return row * columns + column;
    }

    private Vector2 Center(int index) =>
        new((index % columns + 0.5f) * cellSize, (index / columns + 0.5f) * cellSize);

    private void Carve(Canal canal)
    {
        var length = Vector2.Distance(canal.From, canal.To);
        var steps = Math.Max(1, (int)Math.Ceiling(length / (cellSize * 0.25f)));
        var previous = -1;
        for (var i = 0; i <= steps; i++)
        {
            var point = Vector2.Lerp(canal.From, canal.To, i / (float)steps);
            var index = Index(point);
            if (previous >= 0 && index % columns != previous % columns && index / columns != previous / columns)
            {
                CarveCell(previous / columns * columns + index % columns);
            }
            CarveCell(index);
            previous = index;
        }
    }

    private void CarveCell(int index)
    {
        canalCell[index] = canalCell[index] || kind[index] != 0;
        kind[index] = 0;
        owner[index] = -1;
    }

    public bool IsSeaCell(Vector2 point) => kind[Index(point)] == 0;

    private Port? Harbour(Port port)
    {
        var country = OwnerIndex(port.Owner);
        if (country < 0)
        {
            return null;
        }
        var origin = Index(port.Berth);
        var originColumn = origin % columns;
        var originRow = origin / columns;
        var reach = (int)Math.Ceiling(HarbourSearch / cellSize);
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var row = Math.Max(0, originRow - reach); row <= Math.Min(rows - 1, originRow + reach); row++)
        {
            for (var column = Math.Max(0, originColumn - reach); column <= Math.Min(columns - 1, originColumn + reach); column++)
            {
                var index = row * columns + column;
                if (kind[index] != 0 || canalCell[index] || !TouchesLand(column, row, country))
                {
                    continue;
                }
                var distance = Vector2.DistanceSquared(Center(index), port.Berth);
                if (distance < bestDistance)
                {
                    best = index;
                    bestDistance = distance;
                }
            }
        }
        return best < 0 ? null : port with { Berth = Center(best) };
    }

    private bool TouchesLand(int column, int row, short country)
    {
        for (var i = 0; i < 4; i++)
        {
            var nx = column + Neighbours[i].X;
            var ny = row + Neighbours[i].Y;
            if (nx < 0 || ny < 0 || nx >= columns || ny >= rows)
            {
                continue;
            }
            var index = ny * columns + nx;
            if (kind[index] == 2 && owner[index] == country)
            {
                return true;
            }
        }
        return false;
    }

    private float Cost(int index, Medium medium, short own, short target, ulong blocked)
    {
        var k = kind[index];
        switch (medium)
        {
            case Medium.Sea:
                return k == 0 ? 1f : Blocked;
            case Medium.Land:
                if (k == 0)
                {
                    return canalCell[index] ? 1.5f : Blocked;
                }
                if (k == 1)
                {
                    return 1.1f;
                }
                break;
            default:
                if (k <= 1)
                {
                    return 1f;
                }
                break;
        }
        var o = owner[index];
        if (o == own || o == target)
        {
            return 1f;
        }
        if ((blocked & (1UL << o)) != 0)
        {
            return Blocked;
        }
        return medium == Medium.Land ? 1.8f : 1.6f;
    }

    private bool LineClear(Vector2 from, Vector2 to, Medium medium, short own, short target, ulong blocked)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(from, to) / (cellSize * 0.25f)));
        for (var s = 0; s <= steps; s++)
        {
            var point = Vector2.Lerp(from, to, s / (float)steps);
            if (float.IsPositiveInfinity(Cost(Index(point), medium, own, target, blocked)))
            {
                return false;
            }
        }
        return WalkClear(from, to, medium, own, target, blocked);
    }

    private bool WalkClear(Vector2 from, Vector2 to, Medium medium, short own, short target, ulong blocked)
    {
        var startX = from.X / (double)cellSize;
        var startY = from.Y / (double)cellSize;
        var finishX = to.X / (double)cellSize;
        var finishY = to.Y / (double)cellSize;
        var x = Math.Min(columns - 1, (int)Math.Floor(startX));
        var y = Math.Min(rows - 1, (int)Math.Floor(startY));
        var endX = Math.Min(columns - 1, (int)Math.Floor(finishX));
        var endY = Math.Min(rows - 1, (int)Math.Floor(finishY));
        var deltaX = finishX - startX;
        var deltaY = finishY - startY;
        var stepX = Math.Sign(deltaX);
        var stepY = Math.Sign(deltaY);
        var nextX = stepX == 0 ? double.PositiveInfinity : ((stepX > 0 ? x + 1 : x) - startX) / deltaX;
        var nextY = stepY == 0 ? double.PositiveInfinity : ((stepY > 0 ? y + 1 : y) - startY) / deltaY;
        var strideX = stepX == 0 ? double.PositiveInfinity : stepX / deltaX;
        var strideY = stepY == 0 ? double.PositiveInfinity : stepY / deltaY;
        var budget = Math.Abs(endX - x) + Math.Abs(endY - y) + 1;
        for (var i = 0; i <= budget; i++)
        {
            if (float.IsPositiveInfinity(Cost(y * columns + x, medium, own, target, blocked)))
            {
                return false;
            }
            if (x == endX && y == endY)
            {
                return true;
            }
            if (nextX < nextY)
            {
                x = Math.Clamp(x + stepX, 0, columns - 1);
                nextX += strideX;
            }
            else
            {
                y = Math.Clamp(y + stepY, 0, rows - 1);
                nextY += strideY;
            }
        }
        return !float.IsPositiveInfinity(Cost(endY * columns + endX, medium, own, target, blocked));
    }

    private static readonly (int X, int Y)[] Neighbours =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)
    };

    private PathResult? FindPath(Medium medium, Vector2 from, Vector2 to, short own, short target, ulong blocked)
    {
        if (medium == Medium.Sea)
        {
            own = target = -1;
            blocked = 0;
        }
        var start = Index(from);
        var goal = Index(to);
        var key = $"{(int)medium}|{start}|{goal}|{own}|{target}|{blocked}";
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var result = Search(medium, start, goal, own, target, blocked);
        cache[key] = result;
        return result;
    }

    private PathResult? Search(Medium medium, int start, int goal, short own, short target, ulong blocked)
    {
        if (float.IsPositiveInfinity(Cost(start, medium, own, target, blocked))
            || float.IsPositiveInfinity(Cost(goal, medium, own, target, blocked)))
        {
            return null;
        }
        var size = columns * rows;
        if (gScore is null || gScore.Length != size)
        {
            gScore = new float[size];
            parents = new int[size];
            seen = new int[size];
            closed = new int[size];
            stamp = 0;
        }
        var g = gScore;
        var parent = parents!;
        var seenAt = seen!;
        var closedAt = closed!;
        var token = ++stamp;
        var goalX = goal % columns;
        var goalY = goal / columns;
        float Heuristic(int index)
        {
            var dx = Math.Abs(index % columns - goalX);
            var dy = Math.Abs(index / columns - goalY);
            return Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy);
        }

        var open = new PriorityQueue<int, float>();
        g[start] = 0;
        parent[start] = -1;
        seenAt[start] = token;
        open.Enqueue(start, Heuristic(start));
        var found = false;
        while (open.TryDequeue(out var current, out _))
        {
            if (closedAt[current] == token)
            {
                continue;
            }
            closedAt[current] = token;
            if (current == goal)
            {
                found = true;
                break;
            }
            var cx = current % columns;
            var cy = current / columns;
            foreach (var (dx, dy) in Neighbours)
            {
                var nx = cx + dx;
                var ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= columns || ny >= rows)
                {
                    continue;
                }
                var next = ny * columns + nx;
                var cost = Cost(next, medium, own, target, blocked);
                if (float.IsPositiveInfinity(cost))
                {
                    continue;
                }
                var diagonal = dx != 0 && dy != 0;
                if (diagonal
                    && (float.IsPositiveInfinity(Cost(cy * columns + nx, medium, own, target, blocked))
                        || float.IsPositiveInfinity(Cost(ny * columns + cx, medium, own, target, blocked))))
                {
                    continue;
                }
                var candidate = g[current] + (diagonal ? 1.4142f : 1f) * cost;
                if (seenAt[next] != token || candidate < g[next])
                {
                    seenAt[next] = token;
                    g[next] = candidate;
                    parent[next] = current;
                    open.Enqueue(next, candidate + Heuristic(next));
                }
            }
        }
        if (!found)
        {
            return null;
        }
        var cells = new List<int>();
        for (var c = goal; c >= 0; c = parent[c])
        {
            cells.Add(c);
        }
        cells.Reverse();
        var exposure = 0;
        if (medium != Medium.Sea)
        {
            foreach (var c in cells)
            {
                if (kind[c] == 2 && owner[c] != own && owner[c] != target)
                {
                    exposure++;
                }
            }
        }
        var points = new List<Vector2> { Center(cells[0]) };
        var at = 0;
        while (at < cells.Count - 1)
        {
            var far = Math.Min(cells.Count - 1, at + SmoothLookahead);
            while (far > at + 1
                && !LineClear(Center(cells[at]), Center(cells[far]), medium, own, target, blocked))
            {
                far--;
            }
            points.Add(Center(cells[far]));
            at = far;
        }
        if (points.Count == 1)
        {
            points.Add(points[0]);
        }
        return new PathResult(points.ToArray(), exposure);
    }

    public double? SeaDistance(Vector2 from, Vector2 to)
    {
        var path = FindPath(Medium.Sea, from, to, -1, -1, 0);
        if (path is null)
        {
            return null;
        }
        var length = 0.0;
        for (var i = 1; i < path.Points.Length; i++)
        {
            length += Vector2.Distance(path.Points[i - 1], path.Points[i]);
        }
        return length;
    }

    private Vector2? AnchorOf(Country country) =>
        anchors.GetOrAdd(country.Id.Value, _ =>
        {
            var capital = country.Capital;
            if (kind[Index(capital)] == 2 && owner[Index(capital)] == OwnerIndex(country.Id))
            {
                return Center(Index(capital));
            }
            return map.FindNearest(Domain.Land, country.Id, capital, AnchorSearch);
        });

    private short OwnerIndex(CountryId id) =>
        ownerIndex.TryGetValue(id.Value, out var index) ? index : (short)-1;

    private ulong BlockedFor(
        CountryId source,
        CountryId target,
        IEnumerable<Country> countries,
        Func<CountryId, bool> isHostile)
    {
        ulong mask = 0;
        foreach (var country in countries)
        {
            if (country.Id == source || country.Id == target || !isHostile(country.Id))
            {
                continue;
            }
            if (ownerIndex.TryGetValue(country.Id.Value, out var index))
            {
                mask |= 1UL << index;
            }
        }
        return mask;
    }

    private static RouteLeg? Leg(UnitKind carrier, IReadOnlyList<Vector2> points)
    {
        if (points.Count < 2)
        {
            return null;
        }
        var leg = new RouteLeg(carrier, points);
        return leg.Length < 1e-3 ? null : leg;
    }

    private static List<Vector2> Join(IEnumerable<Vector2> first, IEnumerable<Vector2> second)
    {
        var all = first.ToList();
        foreach (var point in second)
        {
            if (all.Count == 0 || Vector2.DistanceSquared(all[^1], point) > 1e-6f)
            {
                all.Add(point);
            }
        }
        return all;
    }

    public ShipmentRoute? Plan(
        TransportMode mode,
        Country source,
        Country target,
        IEnumerable<Country> countries,
        Func<CountryId, bool> isHostile)
    {
        var from = AnchorOf(source);
        var to = AnchorOf(target);
        if (from is null || to is null)
        {
            return null;
        }
        var own = OwnerIndex(source.Id);
        var other = OwnerIndex(target.Id);
        var blocked = BlockedFor(source.Id, target.Id, countries, isHostile);
        switch (mode)
        {
            case TransportMode.Ground:
            {
                var path = FindPath(Medium.Land, from.Value, to.Value, own, other, blocked);
                var leg = path is null ? null : Leg(UnitKind.Truck, path.Points);
                return leg is null ? null : new ShipmentRoute(new[] { leg }, path!.Exposure);
            }
            case TransportMode.Air:
            {
                var path = FindPath(Medium.Air, from.Value, to.Value, own, other, blocked);
                var leg = path is null ? null : Leg(UnitKind.CargoPlane, path.Points);
                return leg is null ? null : new ShipmentRoute(new[] { leg }, path!.Exposure);
            }
            case TransportMode.Naval:
                return PlanNaval(source, target, from.Value, to.Value, own, other, blocked);
            default:
                return null;
        }
    }

    private ShipmentRoute? PlanNaval(
        Country source,
        Country target,
        Vector2 from,
        Vector2 to,
        short own,
        short other,
        ulong blocked)
    {
        var sourcePorts = ports.Where(p => p.Owner == source.Id).ToList();
        var targetPorts = ports.Where(p => p.Owner == target.Id).ToList();
        if (sourcePorts.Count == 0 || targetPorts.Count == 0)
        {
            return null;
        }
        var combos = sourcePorts
            .SelectMany(s => targetPorts.Select(t => (S: s, T: t)))
            .OrderBy(c => Vector2.Distance(c.S.Berth, target.Capital) + Vector2.Distance(c.T.Berth, source.Capital))
            .ThenBy(c => c.S.Id, StringComparer.Ordinal)
            .ThenBy(c => c.T.Id, StringComparer.Ordinal);
        foreach (var (sourcePort, targetPort) in combos)
        {
            var sea = FindPath(Medium.Sea, sourcePort.Berth, targetPort.Berth, own, other, blocked);
            if (sea is null)
            {
                continue;
            }
            var legs = new List<RouteLeg>();
            var exposure = 0;
            var landingOut = map.FindNearest(Domain.Land, source.Id, sourcePort.Berth, LandingSearch);
            var landingIn = map.FindNearest(Domain.Land, target.Id, targetPort.Berth, LandingSearch);
            if (landingOut is null || landingIn is null)
            {
                continue;
            }

            var outbound = FindPath(Medium.Land, from, landingOut.Value, own, own, blocked);
            if (outbound is null)
            {
                continue;
            }
            var truckOut = Leg(UnitKind.Truck, Join(outbound.Points, new[] { sourcePort.Berth }));
            if (truckOut is not null && Vector2.Distance(from, landingOut.Value) > cellSize * 1.5f)
            {
                legs.Add(truckOut);
            }
            exposure += outbound.Exposure;

            var ship = Leg(UnitKind.CargoShip, sea.Points);
            if (ship is null)
            {
                continue;
            }
            legs.Add(ship);

            var onward = FindPath(Medium.Land, landingIn.Value, to, other, other, blocked);
            if (onward is not null)
            {
                var truckIn = Leg(UnitKind.Truck, Join(new[] { targetPort.Berth }, onward.Points));
                if (truckIn is not null && Vector2.Distance(landingIn.Value, to) > cellSize * 1.5f)
                {
                    legs.Add(truckIn);
                }
            }
            else
            {
                var flight = FindPath(Medium.Air, targetPort.Berth, to, other, other, blocked);
                var helicopter = flight is null ? null : Leg(UnitKind.Helicopter, flight.Points);
                if (helicopter is null)
                {
                    continue;
                }
                legs.Add(helicopter);
            }
            return new ShipmentRoute(legs, exposure);
        }
        return null;
    }

    public bool IsLegal(
        ShipmentRoute route,
        Country source,
        Country target,
        IEnumerable<Country> countries,
        Func<CountryId, bool> isHostile)
    {
        var blocked = BlockedFor(source.Id, target.Id, countries, isHostile);
        foreach (var leg in route.Legs)
        {
            for (var i = 1; i < leg.Points.Count; i++)
            {
                var from = leg.Points[i - 1];
                var to = leg.Points[i];
                var steps = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(from, to) / (cellSize * 0.25f)));
                for (var s = 0; s <= steps; s++)
                {
                    var point = Vector2.Lerp(from, to, s / (float)steps);
                    var index = Index(point);
                    var isDock = Vector2.Distance(point, leg.Points[0]) <= cellSize * 1.5f
                        || Vector2.Distance(point, leg.Points[^1]) <= cellSize * 1.5f;
                    switch (leg.Carrier)
                    {
                        case UnitKind.CargoShip:
                            if (kind[index] != 0)
                            {
                                return false;
                            }
                            break;
                        case UnitKind.Truck:
                            if (kind[index] == 0 && !canalCell[index] && !isDock)
                            {
                                return false;
                            }
                            break;
                    }
                    if (kind[index] == 2 && (blocked & (1UL << owner[index])) != 0)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    public (TransportMode Mode, ShipmentRoute Route)? PlanBest(
        TransportMode preferred,
        Country source,
        Country target,
        IEnumerable<Country> countries,
        Func<CountryId, bool> isHostile)
    {
        var order = preferred switch
        {
            TransportMode.Ground => new[] { TransportMode.Ground, TransportMode.Naval, TransportMode.Air },
            TransportMode.Naval => new[] { TransportMode.Naval, TransportMode.Air, TransportMode.Ground },
            _ => new[] { TransportMode.Air, TransportMode.Naval, TransportMode.Ground }
        };
        var all = countries.ToList();
        foreach (var mode in order)
        {
            var route = Plan(mode, source, target, all, isHostile);
            if (route is not null)
            {
                return (mode, route);
            }
        }
        return null;
    }
}
