using WorldEngine.Countries;
using WorldEngine.Routing;

namespace WorldEngine.Interactions;

public sealed class InteractionBook
{
    private readonly List<Interaction> items = new();
    private int nextId = 1;

    public IReadOnlyList<Interaction> All => items;

    public IReadOnlyList<Interaction> Active =>
        items.Where(i => i.Status == InteractionStatus.Active).ToList();

    public Interaction Open(
        InteractionKind kind,
        CountryId source,
        CountryId target,
        TransportMode mode,
        double cargo,
        string subject,
        long startTick,
        long dueTick,
        long resolveTick,
        InteractionStatus plannedStatus,
        ShipmentRoute? route = null,
        string cause = "")
    {
        var interaction = new Interaction(
            nextId++, kind, source, target, mode, cargo, subject, startTick, dueTick, resolveTick, plannedStatus, route, cause);
        items.Add(interaction);
        return interaction;
    }

    public IReadOnlyList<Interaction> Visible(long tick, int fadeTicks) =>
        items.Where(i => i.EndTick is null || tick - i.EndTick.Value <= fadeTicks).ToList();

    public void Prune(long tick, int keepTicks) =>
        items.RemoveAll(i => i.EndTick is not null && tick - i.EndTick.Value > keepTicks);
}
