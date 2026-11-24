namespace WorldEngine.Units;

public enum Domain
{
    Land,
    Sea,
    Air
}

public static class UnitKindExtensions
{
    public static Domain DomainOf(this UnitKind kind) => UnitCatalog.Of(kind).Domain;

    public static UnitRole RoleOf(this UnitKind kind) => UnitCatalog.Of(kind).Role;
}
