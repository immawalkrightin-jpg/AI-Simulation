using WorldEngine.Countries;

namespace WorldEngine.Diplomacy;

public enum Rung
{
    Calm,
    Tension,
    Incident,
    Clash,
    Missile
}

public sealed record AidTie(CountryId Patron, CountryId Client, string Reason);

public sealed record ProxyFront(
    string Name,
    CountryId First,
    CountryId Second,
    Rung Start,
    Rung Ceiling,
    string Reason);

public sealed record SummitTie(CountryId First, CountryId Second, string Subject, string Reason);

public sealed class Doctrine
{
    private readonly Dictionary<(string, string), AidTie> aid = new();
    private readonly Dictionary<(string, string), ProxyFront> fronts = new();
    private readonly Dictionary<(string, string), SummitTie> summits = new();
    private readonly HashSet<(string, string)> forbidden = new();

    public Doctrine(
        IEnumerable<AidTie>? aidTies = null,
        IEnumerable<ProxyFront>? proxyFronts = null,
        IEnumerable<SummitTie>? summitTies = null,
        IEnumerable<(CountryId, CountryId)>? forbiddenPairs = null)
    {
        Aid = (aidTies ?? Array.Empty<AidTie>()).ToList();
        Fronts = (proxyFronts ?? Array.Empty<ProxyFront>()).ToList();
        Summits = (summitTies ?? Array.Empty<SummitTie>()).ToList();
        foreach (var tie in Aid)
        {
            aid[(tie.Patron.Value, tie.Client.Value)] = tie;
        }
        foreach (var front in Fronts)
        {
            if (front.Start > front.Ceiling)
            {
                throw new ArgumentException($"Front {front.Name} starts above its ceiling.");
            }
            fronts[Key(front.First, front.Second)] = front;
        }
        foreach (var tie in Summits)
        {
            summits[Key(tie.First, tie.Second)] = tie;
        }
        foreach (var (first, second) in forbiddenPairs ?? Array.Empty<(CountryId, CountryId)>())
        {
            forbidden.Add(Key(first, second));
        }
    }

    public IReadOnlyList<AidTie> Aid { get; }
    public IReadOnlyList<ProxyFront> Fronts { get; }
    public IReadOnlyList<SummitTie> Summits { get; }

    private static (string, string) Key(CountryId a, CountryId b) =>
        string.CompareOrdinal(a.Value, b.Value) <= 0 ? (a.Value, b.Value) : (b.Value, a.Value);

    public AidTie? AidFor(CountryId patron, CountryId client) =>
        aid.TryGetValue((patron.Value, client.Value), out var tie) ? tie : null;

    public ProxyFront? FrontOf(CountryId a, CountryId b) =>
        fronts.TryGetValue(Key(a, b), out var front) ? front : null;

    public SummitTie? SummitOf(CountryId a, CountryId b) =>
        summits.TryGetValue(Key(a, b), out var tie) ? tie : null;

    public bool Forbids(CountryId a, CountryId b) => forbidden.Contains(Key(a, b));

    public bool MayFight(CountryId a, CountryId b) => !Forbids(a, b) && FrontOf(a, b) is not null;
}
