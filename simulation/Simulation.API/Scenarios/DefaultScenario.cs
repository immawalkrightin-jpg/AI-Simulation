using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Interactions;
using WorldEngine.Routing;
using WorldEngine.Sensors;
using WorldEngine.Territory;
using WorldEngine.Units;
using WorldEngine.World;

namespace Simulation.API.Scenarios;

public static class DefaultScenario
{
    private const float WorldWidth = 2000;
    private const float WorldHeight = 1000;
    private const string ModelId = "USSR";
    private const float MaxScatter = 37;
    private const float ScatterShare = 0.4f;
    private const float AnchorSearch = 60;
    private const float BerthSearch = 40;
    private const int SettleAttempts = 6;
    private const float SettleShrink = 0.18f;
    private const float WaterReach = 4f;

    // Unit roster codes: I infantry, A artillery, K tank, C armored car, W warship, P warplane,
    // H helicopter, T truck, G cargo ship, L cargo plane. A code is repeated for each unit of that kind.
    // Ships need a port and are skipped for countries without one.
    private const string SuperRoster = "IIIAAKKKKCCWWWPPPPHHTTGL";
    private const string RegionalRoster = "IIAKKCWWPPHTG";

    private sealed record Entry(
        string Id,
        string Name,
        double Longitude,
        double Latitude,
        Bloc Bloc,
        string Landmass,
        double Supplies,
        double Ammunition,
        string Units);

    private static readonly Entry[] Entries =
    {
        new("USSR", "Soviet Union", 37.6, 55.75, Bloc.East, "EURASIA", 1200, 1000, SuperRoster),
        new("GDR", "East Germany", 13.4, 52.5, Bloc.East, "EURASIA", 350, 300, "IIAKCPHT"),
        new("POL", "Poland", 21.0, 52.2, Bloc.East, "EURASIA", 350, 300, "IIAKCWPHT"),
        new("CZE", "Czechoslovakia", 14.4, 50.1, Bloc.East, "EURASIA", 300, 250, "IIAKCPT"),
        new("HUN", "Hungary", 19.0, 47.5, Bloc.East, "EURASIA", 250, 200, "IAKCT"),
        new("ROU", "Romania", 26.1, 44.4, Bloc.East, "EURASIA", 250, 200, "IAKCWT"),
        new("BGR", "Bulgaria", 23.3, 42.7, Bloc.East, "EURASIA", 220, 180, "IAKCWT"),
        new("PRC", "China", 116.4, 39.9, Bloc.East, "EURASIA", 900, 700, "IIIAAKKKCCWWPPPHTTGL"),
        new("PRK", "North Korea", 125.75, 39.0, Bloc.East, "EURASIA", 200, 250, "IIAKCWPT"),
        new("VNM", "North Vietnam", 105.8, 21.0, Bloc.East, "EURASIA", 220, 220, "IIAKCWPT"),
        new("CUB", "Cuba", -82.4, 23.1, Bloc.East, "CUBA", 220, 200, "IAKCWT"),
        new("USA", "United States", -77.0, 38.9, Bloc.West, "NAMERICA", 1200, 1000, SuperRoster),
        new("CAN", "Canada", -75.7, 45.4, Bloc.West, "NAMERICA", 400, 300, "IIAKCWPHT"),
        new("GBR", "United Kingdom", -0.1, 51.5, Bloc.West, "BRITAIN", 500, 400, RegionalRoster),
        new("FRA", "France", 2.35, 48.85, Bloc.West, "EURASIA", 500, 400, RegionalRoster),
        new("FRG", "West Germany", 7.1, 50.7, Bloc.West, "EURASIA", 450, 380, RegionalRoster),
        new("ITA", "Italy", 12.5, 41.9, Bloc.West, "EURASIA", 350, 300, "IIAKCWWPHTG"),
        new("TUR", "Turkey", 32.85, 39.9, Bloc.West, "EURASIA", 300, 280, "IIAKKCWPPHTG"),
        new("JPN", "Japan", 139.7, 35.7, Bloc.West, "JAPAN", 450, 300, "IIAKCWWPPHTG"),
        new("KOR", "South Korea", 127.0, 37.55, Bloc.West, "EURASIA", 250, 260, "IIAKKCWPHTG"),
        new("AUS", "Australia", 149.1, -35.3, Bloc.West, "OCEANIA", 300, 220, "IIAKCWPT"),
        new("IND", "India", 77.2, 28.6, Bloc.NonAligned, "EURASIA", 600, 450, RegionalRoster),
        new("EGY", "Egypt", 31.2, 30.0, Bloc.NonAligned, "AFRICA", 280, 250, "IIAKCWPT"),
        new("YUG", "Yugoslavia", 20.5, 44.8, Bloc.NonAligned, "EURASIA", 250, 220, "IIAKCPT"),
        new("BRA", "Brazil", -47.9, -15.8, Bloc.NonAligned, "SAMERICA", 400, 300, "IIAKCWPT"),
        new("IRN", "Iran", 51.4, 35.7, Bloc.NonAligned, "EURASIA", 300, 250, "IIAKCPHT")
    };

    private static readonly Dictionary<string, (double Longitude, double Latitude)> Ports = new()
    {
        ["USSR"] = (33.1, 69.3), ["USA"] = (-76.0, 36.9), ["CAN"] = (-63.6, 44.6), ["GBR"] = (-4.2, 50.35),
        ["FRA"] = (-4.5, 48.4), ["FRG"] = (10.1, 54.35), ["ITA"] = (14.2, 40.8), ["TUR"] = (29.0, 41.0),
        ["JPN"] = (139.6, 35.3), ["KOR"] = (129.0, 35.1), ["AUS"] = (151.2, -33.9), ["PRC"] = (121.5, 31.2),
        ["PRK"] = (127.6, 39.3), ["VNM"] = (106.8, 20.8), ["CUB"] = (-82.3, 23.2), ["IND"] = (72.8, 18.9),
        ["EGY"] = (32.3, 31.25), ["BRA"] = (-43.2, -22.9), ["POL"] = (18.6, 54.55), ["ROU"] = (28.65, 44.15),
        ["BGR"] = (27.9, 43.2)
    };

    // Every harbour that ships from or to a country. Berths snap to the nearest sea cell; a harbour that
    // cannot be snapped is skipped.
    private static readonly (string Owner, string Name, double Longitude, double Latitude)[] PortSites =
    {
        ("USSR", "Murmansk", 33.1, 69.3), ("USSR", "Baltiysk", 20.0, 54.65),
        ("USSR", "Sevastopol", 33.5, 44.6), ("USSR", "Vladivostok", 131.9, 43.1),
        ("USA", "Norfolk", -76.0, 36.9), ("USA", "San Diego", -117.2, 32.7),
        ("CAN", "Halifax", -63.6, 44.6), ("CAN", "Vancouver", -123.1, 49.3),
        ("GBR", "Plymouth", -4.2, 50.35),
        ("FRA", "Brest", -4.5, 48.4), ("FRA", "Toulon", 5.9, 43.1),
        ("FRG", "Kiel", 10.1, 54.35),
        ("ITA", "Naples", 14.2, 40.8),
        ("TUR", "Istanbul", 29.0, 41.0), ("TUR", "Izmir", 27.1, 38.4),
        ("JPN", "Yokosuka", 139.6, 35.3), ("JPN", "Sasebo", 129.7, 33.2),
        ("KOR", "Busan", 129.0, 35.1),
        ("AUS", "Sydney", 151.2, -33.9), ("AUS", "Fremantle", 115.7, -32.1),
        ("PRC", "Shanghai", 121.5, 31.2), ("PRC", "Guangzhou", 113.6, 22.3),
        ("PRK", "Wonsan", 127.6, 39.3),
        ("VNM", "Haiphong", 106.8, 20.8),
        ("CUB", "Havana", -82.3, 23.2),
        ("IND", "Mumbai", 72.8, 18.9), ("IND", "Kolkata", 88.3, 22.5),
        ("EGY", "Port Said", 32.3, 31.25),
        ("BRA", "Rio de Janeiro", -43.2, -22.9),
        ("POL", "Gdynia", 18.6, 54.55),
        ("ROU", "Constanta", 28.65, 44.15),
        ("BGR", "Varna", 27.9, 43.2)
    };

    // Passages the 110m map cannot resolve. They are cut through the raster for shipping only.
    private static readonly (string Name, double FromLon, double FromLat, double ToLon, double ToLat)[] CanalSites =
    {
        ("Turkish Straits", 26.82, 40.7, 26.82, 40.3),
        ("Suez Canal", 32.30, 31.3, 32.58, 29.4),
        ("Gulf of Suez", 32.58, 29.4, 32.94, 28.6),
        ("Red Sea north", 32.94, 28.6, 34.5, 26.0),
        ("Red Sea middle", 34.5, 26.0, 36.0, 23.5),
        ("Red Sea south", 36.0, 23.5, 38.0, 20.0),
        ("Panama Canal", -79.38, 9.7, -79.38, 7.4)
    };

    // The network only depends on the map, so every scenario build shares one (and its path cache).
    private static readonly Lazy<RouteNetwork?> SharedRoutes = new(BuildRoutes);

    private static RouteNetwork? BuildRoutes()
    {
        var territory = TerritoryLoader.Shared;
        if (territory is null)
        {
            return null;
        }
        var ports = new List<Port>();
        var counts = new Dictionary<string, int>();
        foreach (var (ownerId, name, longitude, latitude) in PortSites)
        {
            var owner = new CountryId(ownerId);
            var berth = SeaBerth(territory, owner, Place(longitude, latitude));
            if (berth is null)
            {
                continue;
            }
            counts.TryGetValue(ownerId, out var count);
            counts[ownerId] = count + 1;
            ports.Add(new Port($"{ownerId}-{count + 1}", owner, name, berth.Value));
        }
        var canals = CanalSites
            .Select(c => new Canal(c.Name, Place(c.FromLon, c.FromLat), Place(c.ToLon, c.ToLat)))
            .ToList();
        return new RouteNetwork(territory, ports, canals, WaterReach);
    }

    private static readonly (string, string, double)[] RelationOverrides =
    {
        ("USSR", "USA", -0.75), ("USSR", "GDR", 0.9), ("USSR", "POL", 0.6), ("USSR", "CZE", 0.5),
        ("USSR", "HUN", 0.45), ("USSR", "ROU", 0.25), ("USSR", "BGR", 0.8), ("USSR", "PRC", 0.1),
        ("USSR", "PRK", 0.7), ("USSR", "VNM", 0.7), ("USSR", "CUB", 0.9), ("USSR", "IND", 0.6),
        ("USSR", "EGY", 0.5), ("USSR", "YUG", -0.1), ("USSR", "IRN", -0.2), ("USSR", "JPN", -0.5),
        ("USA", "CUB", -0.9), ("USA", "VNM", -0.85), ("USA", "PRK", -0.85), ("USA", "PRC", -0.7),
        ("USA", "GBR", 0.9), ("USA", "CAN", 0.85), ("USA", "JPN", 0.8), ("USA", "KOR", 0.8),
        ("USA", "AUS", 0.8), ("USA", "IND", 0.2), ("USA", "EGY", -0.1), ("USA", "BRA", 0.4),
        ("USA", "IRN", 0.5), ("USA", "YUG", 0.2), ("FRG", "GDR", -0.85), ("PRK", "KOR", -0.9),
        ("IND", "PRC", -0.75), ("PRC", "VNM", -0.2), ("PRC", "PRK", 0.5), ("PRC", "CUB", 0.0),
        ("FRA", "FRG", 0.75), ("GBR", "FRA", 0.5), ("JPN", "PRC", -0.5), ("EGY", "GBR", -0.3),
        ("EGY", "FRA", -0.3)
    };

    private static readonly string[] EastEuropeanAllies = { "GDR", "POL", "CZE", "HUN", "ROU", "BGR", "PRC" };

    private static AidTie Aid(string patron, string client, string reason) =>
        new(new CountryId(patron), new CountryId(client), reason);

    private static ProxyFront Front(string name, string first, string second, Rung start, Rung ceiling, string reason) =>
        new(name, new CountryId(first), new CountryId(second), start, ceiling, reason);

    // Who may trade, talk and fight, and why. Direct Soviet-American and Pact-NATO war is forbidden outright.
    private static Doctrine BuildDoctrine(IEnumerable<(CountryId, CountryId)> forbidden)
    {
        const string pact = "the Warsaw Pact keeps its allied armies supplied";
        const string nato = "NATO allies draw on American stocks";
        var aid = new List<AidTie>
        {
            Aid("USSR", "GDR", pact), Aid("USSR", "POL", pact), Aid("USSR", "CZE", pact),
            Aid("USSR", "HUN", pact), Aid("USSR", "ROU", pact), Aid("USSR", "BGR", pact),
            Aid("USSR", "PRK", "Moscow sustains North Korea against the South"),
            Aid("USSR", "VNM", "Moscow arms Hanoi against the American intervention"),
            Aid("USSR", "CUB", "Moscow props up its Caribbean client"),
            Aid("USSR", "IND", "Moscow courts non-aligned India with arms credits"),
            Aid("USSR", "EGY", "Moscow courts Egypt after the Suez crisis"),
            Aid("PRC", "PRK", "Beijing backs its neighbour on the Korean front"),
            Aid("USA", "GBR", nato), Aid("USA", "CAN", nato), Aid("USA", "FRA", nato), Aid("USA", "FRG", nato),
            Aid("USA", "ITA", nato), Aid("USA", "TUR", nato),
            Aid("USA", "JPN", "Washington underwrites Japanese security"),
            Aid("USA", "KOR", "Washington underwrites South Korean security"),
            Aid("USA", "AUS", "Washington underwrites the Pacific alliance"),
            Aid("USA", "BRA", "Washington keeps South America inside its sphere"),
            Aid("USA", "IRN", "Washington keeps Iran inside its sphere"),
            Aid("USA", "YUG", "Washington rewards Belgrade for breaking with Moscow")
        };
        var fronts = new List<ProxyFront>
        {
            Front("Korean peninsula", "PRK", "KOR", Rung.Incident, Rung.Missile, "the armistice line is still disputed"),
            Front("Indochina front", "USA", "VNM", Rung.Clash, Rung.Missile, "American forces back the south against Hanoi"),
            Front("Sino-Indian border", "IND", "PRC", Rung.Tension, Rung.Clash, "the Himalayan frontier is disputed"),
            Front("inner German border", "FRG", "GDR", Rung.Tension, Rung.Incident, "the divided frontier is watched by both sides"),
            Front("Caribbean standoff", "USA", "CUB", Rung.Tension, Rung.Incident, "Washington and Havana confront each other over the embargo"),
            Front("Suez dispute", "EGY", "GBR", Rung.Calm, Rung.Clash, "the canal remains contested")
        };
        var summits = new List<SummitTie>
        {
            new(new CountryId("USSR"), new CountryId("USA"), "arms limitation", "the nuclear standoff demands a channel between the superpowers"),
            new(new CountryId("USA"), new CountryId("PRC"), "opening talks", "Washington tries to split Beijing from Moscow")
        };
        return new Doctrine(aid, fronts, summits, forbidden);
    }

    private static readonly Dictionary<UnitKind, int> KindSector = new()
    {
        [UnitKind.Infantry] = 0,
        [UnitKind.Artillery] = 1,
        [UnitKind.Tank] = 2,
        [UnitKind.Armor] = 2,
        [UnitKind.ArmoredCar] = 3,
        [UnitKind.Warplane] = 4,
        [UnitKind.Air] = 4,
        [UnitKind.Helicopter] = 5,
        [UnitKind.Truck] = 6,
        [UnitKind.SupplyConvoy] = 6
    };

    private const int SectorCount = 7;

    private static Vector2 Place(double longitude, double latitude) =>
        new(
            (float)((longitude + 180) / 360 * WorldWidth),
            (float)((90 - latitude) / 180 * WorldHeight));

    private static float NearestCapitalDistance(Entry entry)
    {
        var capital = Place(entry.Longitude, entry.Latitude);
        var nearest = float.MaxValue;
        foreach (var other in Entries)
        {
            if (other.Id == entry.Id)
            {
                continue;
            }
            nearest = Math.Min(nearest, Vector2.Distance(capital, Place(other.Longitude, other.Latitude)));
        }
        return nearest;
    }

    private static Vector2 ScatterOffset(UnitKind kind, int occurrence, float maxRadius)
    {
        var sector = KindSector.TryGetValue(kind, out var index) ? index : 0;
        var angle = sector * (2 * Math.PI / SectorCount) + occurrence * 0.45;
        var minRadius = maxRadius * 0.45f;
        var step = (sector % 3) / 2f;
        var radius = minRadius + (maxRadius - minRadius) * step + occurrence * maxRadius * 0.1f;
        radius = Math.Min(radius, maxRadius);
        return new Vector2((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius));
    }

    private static Vector2 AnchorOf(TerritoryMap? territory, CountryId owner, Vector2 capital)
    {
        if (territory is null || territory.Permits(Domain.Land, owner, capital))
        {
            return capital;
        }
        return territory.FindNearest(Domain.Land, owner, capital, AnchorSearch) ?? capital;
    }

    private static Vector2? SeaBerth(TerritoryMap? territory, CountryId owner, Vector2 port)
    {
        if (territory is null || territory.Permits(Domain.Sea, owner, port))
        {
            return port;
        }
        return territory.FindNearest(Domain.Sea, owner, port, BerthSearch);
    }

    private static Vector2 Settle(TerritoryMap? territory, Domain domain, CountryId owner, Vector2 anchor, Vector2 offset)
    {
        if (territory is null)
        {
            return anchor + offset;
        }
        for (var attempt = 0; attempt < SettleAttempts; attempt++)
        {
            var candidate = anchor + offset * (1f - attempt * SettleShrink);
            if (territory.Permits(domain, owner, candidate))
            {
                return candidate;
            }
        }
        return anchor;
    }

    private static double BaseRelation(Entry a, Entry b)
    {
        if (a.Bloc == b.Bloc && a.Bloc != Bloc.NonAligned)
        {
            return a.Bloc == Bloc.East ? 0.55 : 0.6;
        }
        if (a.Bloc != Bloc.NonAligned && b.Bloc != Bloc.NonAligned)
        {
            return -0.6;
        }
        return 0.1;
    }

    private static UnitKind? KindOf(char code) => code switch
    {
        'I' => UnitKind.Infantry,
        'A' => UnitKind.Artillery,
        'K' => UnitKind.Tank,
        'C' => UnitKind.ArmoredCar,
        'W' => UnitKind.Warship,
        'P' => UnitKind.Warplane,
        'H' => UnitKind.Helicopter,
        'T' => UnitKind.Truck,
        'G' => UnitKind.CargoShip,
        'L' => UnitKind.CargoPlane,
        _ => null
    };

    public static WorldSimulation Build(int seed)
    {
        var world = new WorldState(seed, WorldWidth, WorldHeight);
        world.Territory = TerritoryLoader.Shared;
        var territory = world.Territory;
        foreach (var entry in Entries)
        {
            world.Countries.Add(new Country(
                new CountryId(entry.Id),
                entry.Name,
                Place(entry.Longitude, entry.Latitude),
                entry.Supplies,
                entry.Ammunition,
                entry.Bloc,
                entry.Landmass));
        }
        world.ModelCountry = new CountryId(ModelId);

        for (var i = 0; i < Entries.Length; i++)
        {
            for (var j = i + 1; j < Entries.Length; j++)
            {
                world.Relations.Set(
                    new CountryId(Entries[i].Id),
                    new CountryId(Entries[j].Id),
                    BaseRelation(Entries[i], Entries[j]));
            }
        }
        foreach (var (first, second, value) in RelationOverrides)
        {
            world.Relations.Set(new CountryId(first), new CountryId(second), value);
        }

        foreach (var entry in Entries)
        {
            var owner = new CountryId(entry.Id);
            var capital = Place(entry.Longitude, entry.Latitude);
            var anchor = AnchorOf(territory, owner, capital);
            var maxRadius = Math.Max(
                0f,
                Math.Min(MaxScatter, NearestCapitalDistance(entry) * ScatterShare) - Vector2.Distance(anchor, capital));
            var occurrences = new Dictionary<UnitKind, int>();
            var shipsAtPort = 0;
            for (var k = 0; k < entry.Units.Length; k++)
            {
                var kind = KindOf(entry.Units[k]);
                if (kind is null)
                {
                    continue;
                }
                Vector2 position;
                occurrences.TryGetValue(kind.Value, out var occurrence);
                occurrences[kind.Value] = occurrence + 1;
                if (kind.Value.DomainOf() == Domain.Sea)
                {
                    if (!Ports.TryGetValue(entry.Id, out var port))
                    {
                        continue;
                    }
                    var berth = SeaBerth(territory, owner, Place(port.Longitude, port.Latitude));
                    if (berth is null)
                    {
                        continue;
                    }
                    var shipSlot = shipsAtPort++;
                    var nudge = new Vector2(shipSlot * 2.2f, (shipSlot % 2 == 0 ? 1f : -1f) * 1.4f);
                    position = Settle(territory, Domain.Sea, owner, berth.Value, nudge);
                }
                else
                {
                    position = Settle(
                        territory,
                        kind.Value.DomainOf(),
                        owner,
                        anchor,
                        ScatterOffset(kind.Value, occurrence, maxRadius));
                }
                world.AddUnit(owner, kind.Value, position);
            }
        }
        var soviet = new CountryId(ModelId);
        world.Routes = territory is null ? null : SharedRoutes.Value;
        var pacificBase = Place(131.9, 43.1);
        world.AddUnit(soviet, UnitKind.Warship, SeaBerth(territory, soviet, pacificBase) ?? pacificBase);

        var sensors = new SensorNetwork(seed + 1000);
        sensors.Add(SensorProfiles.Radar(1, soviet, Place(37.6, 55.75)));
        sensors.Add(SensorProfiles.Radar(2, soviet, Place(33.1, 69.3)));
        sensors.Add(SensorProfiles.Radar(3, soviet, Place(131.9, 43.1)));
        sensors.Add(SensorProfiles.Optical(4, soviet, Place(21.0, 54.7)));
        sensors.Add(SensorProfiles.Sonar(5, soviet, Place(33.1, 69.3)));
        sensors.Add(SensorProfiles.Sonar(6, soviet, Place(131.9, 43.1)));

        var noDirect = new List<(CountryId, CountryId)>();
        foreach (var entry in Entries.Where(e => e.Bloc == Bloc.West))
        {
            noDirect.Add((soviet, new CountryId(entry.Id)));
        }
        foreach (var id in EastEuropeanAllies)
        {
            noDirect.Add((new CountryId("USA"), new CountryId(id)));
        }
        noDirect.Add((new CountryId("USA"), soviet));
        world.Doctrine = BuildDoctrine(noDirect);
        world.Escalation = new EscalationLadder(world.Doctrine);

        return new WorldSimulation(
            world,
            new WorldActivityGenerator(interval: 15, probability: 0.2, dispatchUnits: true, patrolRadius: 30),
            sensors,
            new InteractionEngine(noDirect));
    }
}
