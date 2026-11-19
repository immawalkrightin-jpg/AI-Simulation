using WorldEngine.Countries;
using WorldEngine.World;

namespace WorldEngine.Events;

public sealed class RelationShiftEvent : WorldEvent
{
    public CountryId First { get; }
    public CountryId Second { get; }
    public double Delta { get; }
    public string Reason { get; }

    public override string Kind => "relation_shift";

    public RelationShiftEvent(CountryId first, CountryId second, double delta, string reason)
    {
        First = first;
        Second = second;
        Delta = delta;
        Reason = reason;
    }

    public override void Apply(WorldState world) => world.Relations.Adjust(First, Second, Delta);

    public override string Describe() => $"{First} and {Second} relations {Delta:+0.00;-0.00}: {Reason}";
}
