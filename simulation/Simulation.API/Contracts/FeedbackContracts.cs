namespace Simulation.API.Contracts.Feedback;

public sealed record CorrectionInput(
    string Operation,
    int Number,
    string Category,
    string Description);

public sealed record CorrectionDto(
    string Id,
    DateTimeOffset ReceivedAt,
    string Operation,
    int Number,
    string Category,
    string Description,
    long Tick,
    int Seed);
