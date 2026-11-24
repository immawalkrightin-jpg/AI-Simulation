using WorldEngine.Units;
using WorldEngine.World;

namespace WorldEngine.Countries;

public sealed record PowerBreakdown(double Forces, double Stockpiles, double Total);

// National power is computed from the world every time it is asked for; nothing stores it.
// Forces: every fielded (not destroyed) unit counts its catalog weight times its share of full strength,
// so a damaged formation is worth less. Stockpiles: supplies and ammunition at fixed weights.
public static class NationalPower
{
    public const double SuppliesWeight = 0.02;
    public const double AmmunitionWeight = 0.05;

    public static PowerBreakdown Of(WorldState world, CountryId owner)
    {
        var country = world.Countries.Get(owner);
        var forces = 0.0;
        foreach (var unit in world.Units)
        {
            if (unit.Owner != owner || unit.Status == UnitStatus.Destroyed)
            {
                continue;
            }
            var spec = UnitCatalog.Of(unit.Kind);
            forces += spec.PowerWeight * Math.Min(1.0, unit.Strength / spec.Strength);
        }
        var stockpiles = country.Supplies * SuppliesWeight + country.Ammunition * AmmunitionWeight;
        return new PowerBreakdown(forces, stockpiles, forces + stockpiles);
    }
}
