namespace WorldEngine.Events;

public sealed record EventRecord(long Tick, string Kind, string Description);
