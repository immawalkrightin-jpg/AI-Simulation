namespace WorldEngine.Units;

// Rules for units that lie in wait. Only land combat units can do it, artillery excepted (guns are not
// hidden by a ditch). Cover cuts the chance that a sensor reports the unit, it does not remove it.
public static class AmbushRules
{
    // Engine units. A hidden unit springs on anything hostile that comes this close.
    public const float Radius = 6f;

    // Share of the normal detection chance that survives cover.
    public const double Concealment = 0.2;

    public static bool CanAmbush(UnitKind kind)
    {
        var spec = UnitCatalog.Of(kind);
        return spec.Domain == Domain.Land && spec.Role == UnitRole.Combat && kind != UnitKind.Artillery;
    }
}
