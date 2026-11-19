namespace WorldEngine.Countries;

public readonly record struct CountryId(string Value)
{
    public override string ToString() => Value;
}
