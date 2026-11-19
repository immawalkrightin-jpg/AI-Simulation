namespace WorldEngine.Events;

public sealed class EventScheduler
{
    private readonly PriorityQueue<WorldEvent, (long Tick, long Sequence)> queue = new();
    private long sequence;

    public int Pending => queue.Count;

    public void Schedule(WorldEvent worldEvent) =>
        queue.Enqueue(worldEvent, (worldEvent.ScheduledTick, sequence++));

    public IReadOnlyList<WorldEvent> TakeDue(long tick)
    {
        var due = new List<WorldEvent>();
        while (queue.TryPeek(out _, out var priority) && priority.Tick <= tick)
        {
            due.Add(queue.Dequeue());
        }
        return due;
    }
}
