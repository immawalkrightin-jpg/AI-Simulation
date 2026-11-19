namespace WorldEngine.Events;

public sealed class EventLog
{
    private readonly List<EventRecord> records = new();

    public IReadOnlyList<EventRecord> Records => records;

    public void Append(long tick, WorldEvent worldEvent) =>
        records.Add(new EventRecord(tick, worldEvent.Kind, worldEvent.Describe()));
}
