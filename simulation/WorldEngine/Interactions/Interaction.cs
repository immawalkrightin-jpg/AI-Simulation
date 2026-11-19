using WorldEngine.Countries;

namespace WorldEngine.Interactions;

public enum InteractionKind
{
    Negotiation,
    SupplyShipment,
    Clash,
    MissileStrike
}

public enum TransportMode
{
    None,
    Ground,
    Naval,
    Air
}

public enum InteractionStatus
{
    Active,
    Succeeded,
    Cancelled,
    Intercepted,
    Resolved
}

public sealed class Interaction
{
    public int Id { get; }
    public InteractionKind Kind { get; }
    public CountryId Source { get; }
    public CountryId Target { get; }
    public TransportMode Mode { get; }
    public double Cargo { get; }
    public string Subject { get; }
    public long StartTick { get; }
    public long DueTick { get; }
    public long ResolveTick { get; }
    public InteractionStatus PlannedStatus { get; }
    public InteractionStatus Status { get; private set; } = InteractionStatus.Active;
    public long? EndTick { get; private set; }

    public Interaction(
        int id,
        InteractionKind kind,
        CountryId source,
        CountryId target,
        TransportMode mode,
        double cargo,
        string subject,
        long startTick,
        long dueTick,
        long resolveTick,
        InteractionStatus plannedStatus)
    {
        Id = id;
        Kind = kind;
        Source = source;
        Target = target;
        Mode = mode;
        Cargo = cargo;
        Subject = subject;
        StartTick = startTick;
        DueTick = Math.Max(startTick + 1, dueTick);
        ResolveTick = resolveTick;
        PlannedStatus = plannedStatus;
    }

    public void Complete(long tick)
    {
        Status = PlannedStatus;
        EndTick = tick;
    }
}
