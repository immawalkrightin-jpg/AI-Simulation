using Microsoft.Extensions.Options;

namespace Simulation.API.Services;

public sealed class SimulationHost : BackgroundService
{
    private readonly SimulationBroadcaster broadcaster;
    private readonly SimulationControl control;
    private readonly SimulationOptions options;
    private readonly ILogger<SimulationHost> logger;

    public SimulationHost(
        SimulationBroadcaster broadcaster,
        SimulationControl control,
        IOptions<SimulationOptions> options,
        ILogger<SimulationHost> logger)
    {
        this.broadcaster = broadcaster;
        this.control = control;
        this.options = options.Value;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(10, options.TickIntervalMs));
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (control.Paused)
                {
                    continue;
                }
                try
                {
                    await broadcaster.StepAsync(1, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Simulation tick failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
