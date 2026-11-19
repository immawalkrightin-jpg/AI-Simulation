namespace WorldEngine.SimulationClock;

public sealed class SimClock
{
    public long Tick { get; private set; }
    public double TickSeconds { get; }
    public double ElapsedSeconds => Tick * TickSeconds;

    public SimClock(double tickSeconds = 60)
    {
        if (tickSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickSeconds));
        }
        TickSeconds = tickSeconds;
    }

    public void Advance() => Tick++;
}
