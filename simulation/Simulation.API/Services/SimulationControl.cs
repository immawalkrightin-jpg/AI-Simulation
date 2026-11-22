using Microsoft.Extensions.Options;

namespace Simulation.API.Services;

public sealed class SimulationControl
{
    public const double MinSpeed = 0.25;
    public const double MaxSpeed = 8;

    private volatile bool paused;
    private double speed = 1;

    public SimulationControl(IOptions<SimulationOptions> options)
    {
        paused = !options.Value.AutoStart;
    }

    public bool Paused
    {
        get => paused;
        set => paused = value;
    }

    public double Speed
    {
        get => Volatile.Read(ref speed);
        set => Volatile.Write(ref speed, Math.Clamp(value, MinSpeed, MaxSpeed));
    }
}
