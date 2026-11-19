using WorldEngine.Countries;

namespace WorldEngine.Diplomacy;

public sealed class RelationMatrix
{
    private readonly Dictionary<(string, string), double> values = new();

    private static (string, string) Key(CountryId a, CountryId b) =>
        string.CompareOrdinal(a.Value, b.Value) <= 0 ? (a.Value, b.Value) : (b.Value, a.Value);

    public double Get(CountryId a, CountryId b) =>
        values.TryGetValue(Key(a, b), out var value) ? value : 0;

    public void Set(CountryId a, CountryId b, double value)
    {
        if (a == b)
        {
            throw new ArgumentException("A country has no relation with itself.");
        }
        values[Key(a, b)] = Math.Clamp(value, -1, 1);
    }

    public double Adjust(CountryId a, CountryId b, double delta)
    {
        var next = Math.Clamp(Get(a, b) + delta, -1, 1);
        Set(a, b, next);
        return next;
    }

    public RelationState StateOf(CountryId a, CountryId b)
    {
        var value = Get(a, b);
        return value switch
        {
            < -0.6 => RelationState.Hostile,
            < -0.2 => RelationState.Tense,
            < 0.3 => RelationState.Neutral,
            < 0.7 => RelationState.Friendly,
            _ => RelationState.Allied
        };
    }
}
