namespace WorldEngine.Units;

public enum Domain
{
    Land,
    Sea,
    Air
}

public static class UnitKindExtensions
{
    public static Domain DomainOf(this UnitKind kind) => kind switch
    {
        UnitKind.Naval => Domain.Sea,
        UnitKind.Air => Domain.Air,
        _ => Domain.Land
    };
}
