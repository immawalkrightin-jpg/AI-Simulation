namespace Simulation.API.Services;

public sealed class SimulationOptions
{
    public int Seed { get; set; } = 42;
    public int TickIntervalMs { get; set; } = 1000;
    public bool AutoStart { get; set; } = true;
    public string GroundTruthKey { get; set; } = "dev-ground-truth-key";
    public string AiKey { get; set; } = "dev-ai-key";
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
