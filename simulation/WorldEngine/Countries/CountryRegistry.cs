namespace WorldEngine.Countries;

public sealed class CountryRegistry
{
    private readonly Dictionary<CountryId, Country> countries = new();

    public IReadOnlyList<Country> All =>
        countries.Values.OrderBy(c => c.Id.Value, StringComparer.Ordinal).ToList();

    public void Add(Country country)
    {
        if (!countries.TryAdd(country.Id, country))
        {
            throw new InvalidOperationException($"Country {country.Id} already exists.");
        }
    }

    public Country Get(CountryId id) =>
        countries.TryGetValue(id, out var country)
            ? country
            : throw new KeyNotFoundException($"Unknown country {id}.");

    public bool Contains(CountryId id) => countries.ContainsKey(id);
}
