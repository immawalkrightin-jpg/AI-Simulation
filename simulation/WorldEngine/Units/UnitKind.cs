namespace WorldEngine.Units;

// The first six values are the original roster and keep their numeric values. Armor, Naval, Air and
// SupplyConvoy are the generic kinds from before milestone 15; they stay valid so older worlds, tests and
// contracts keep working. The scenario fields the specific kinds below.
public enum UnitKind
{
    Infantry,
    Artillery,
    Armor,
    Naval,
    Air,
    SupplyConvoy,
    Tank,
    ArmoredCar,
    Warship,
    Warplane,
    Helicopter,
    Truck,
    CargoShip,
    CargoPlane
}
