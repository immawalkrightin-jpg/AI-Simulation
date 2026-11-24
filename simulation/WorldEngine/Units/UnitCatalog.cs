namespace WorldEngine.Units;

public enum UnitRole
{
    Combat,
    Carrier
}

// Everything the engine knows about a kind of unit, in one place.
// Speed is engine units per second, Strength the starting (and full) strength, Range the operating reach
// in engine units (weapon reach for combat units, trip length for carriers), PowerWeight the value of a
// full-strength unit in national power.
public sealed record UnitSpec(
    UnitKind Kind,
    Domain Domain,
    UnitRole Role,
    double Speed,
    double Strength,
    float Range,
    double PowerWeight);

public static class UnitCatalog
{
    private static readonly Dictionary<UnitKind, UnitSpec> Specs = Build();

    public static IReadOnlyCollection<UnitSpec> All => Specs.Values;

    public static UnitSpec Of(UnitKind kind) =>
        Specs.TryGetValue(kind, out var spec)
            ? spec
            : throw new KeyNotFoundException($"No spec for unit kind {kind}.");

    private static Dictionary<UnitKind, UnitSpec> Build()
    {
        var specs = new[]
        {
            // Combat core.
            new UnitSpec(UnitKind.Infantry, Domain.Land, UnitRole.Combat, 0.04, 100, 3, 1.0),
            new UnitSpec(UnitKind.Artillery, Domain.Land, UnitRole.Combat, 0.03, 100, 40, 2.0),
            new UnitSpec(UnitKind.Tank, Domain.Land, UnitRole.Combat, 0.08, 100, 6, 3.0),
            new UnitSpec(UnitKind.ArmoredCar, Domain.Land, UnitRole.Combat, 0.11, 70, 5, 1.5),
            new UnitSpec(UnitKind.Warship, Domain.Sea, UnitRole.Combat, 0.12, 120, 25, 5.0),
            new UnitSpec(UnitKind.Warplane, Domain.Air, UnitRole.Combat, 0.4, 80, 80, 4.0),
            new UnitSpec(UnitKind.Helicopter, Domain.Air, UnitRole.Combat, 0.25, 70, 30, 2.5),
            // Carriers move cargo and do not fight.
            new UnitSpec(UnitKind.Truck, Domain.Land, UnitRole.Carrier, 0.06, 60, 120, 0.3),
            new UnitSpec(UnitKind.CargoShip, Domain.Sea, UnitRole.Carrier, 0.09, 150, 600, 0.5),
            new UnitSpec(UnitKind.CargoPlane, Domain.Air, UnitRole.Carrier, 0.3, 90, 500, 0.5),
            // Generic kinds from before milestone 15, with their original figures.
            new UnitSpec(UnitKind.Armor, Domain.Land, UnitRole.Combat, 0.08, 100, 6, 3.0),
            new UnitSpec(UnitKind.Naval, Domain.Sea, UnitRole.Combat, 0.12, 120, 25, 5.0),
            new UnitSpec(UnitKind.Air, Domain.Air, UnitRole.Combat, 0.4, 80, 80, 4.0),
            new UnitSpec(UnitKind.SupplyConvoy, Domain.Land, UnitRole.Carrier, 0.06, 60, 120, 0.3)
        };
        return specs.ToDictionary(s => s.Kind);
    }
}
