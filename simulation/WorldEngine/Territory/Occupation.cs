using System.Numerics;
using WorldEngine.Countries;

namespace WorldEngine.Territory;

// A patch of one country's land that another country holds after a decisive clash. It is an overlay on the
// world, never a change to the shared territory raster: the raster keeps saying who the land belongs to,
// the book says who holds it now.
public sealed class Occupation
{
    public Occupation(int id, CountryId holder, CountryId loser, Vector2 center, float radius, long startTick, string cause)
    {
        Id = id;
        Holder = holder;
        Loser = loser;
        Center = center;
        Radius = radius;
        StartTick = startTick;
        Cause = cause;
    }

    public int Id { get; }
    public CountryId Holder { get; }
    public CountryId Loser { get; }
    public Vector2 Center { get; }
    public float Radius { get; }
    public long StartTick { get; }
    public string Cause { get; }

    public bool Contains(Vector2 point) => Vector2.DistanceSquared(point, Center) <= Radius * Radius;
}

public sealed class OccupationBook
{
    private readonly List<Occupation> items = new();
    private int nextId = 1;

    public IReadOnlyList<Occupation> All => items;

    public Occupation Add(CountryId holder, CountryId loser, Vector2 center, float radius, long startTick, string cause)
    {
        var occupation = new Occupation(nextId++, holder, loser, center, radius, startTick, cause);
        items.Add(occupation);
        return occupation;
    }

    // Who holds the ground at this point, or nobody when it is not occupied.
    public CountryId? HolderAt(Vector2 point)
    {
        foreach (var item in items)
        {
            if (item.Contains(point))
            {
                return item.Holder;
            }
        }
        return null;
    }

    public int Count(CountryId holder, CountryId loser) =>
        items.Count(i => i.Holder == holder && i.Loser == loser);

    // Hands back the oldest piece of ground the two countries are contesting, in either direction, once it
    // has been held for at least minimumAge ticks. Returns it, or null when there is none to give back.
    public Occupation? Release(CountryId a, CountryId b, long tick, long minimumAge)
    {
        var found = items.FirstOrDefault(i =>
            tick - i.StartTick >= minimumAge
            && ((i.Holder == a && i.Loser == b) || (i.Holder == b && i.Loser == a)));
        if (found is not null)
        {
            items.Remove(found);
        }
        return found;
    }
}
