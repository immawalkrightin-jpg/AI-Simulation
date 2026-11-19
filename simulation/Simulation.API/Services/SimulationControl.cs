using Microsoft.Extensions.Options;

namespace Simulation.API.Services;

public sealed class SimulationControl
{
    private volatile bool paused;

    public SimulationControl(IOptions<SimulationOptions> options)
    {
        paused = !options.Value.AutoStart;
    }

    public bool Paused
    {
        get => paused;
        set => paused = value;
    }
}
