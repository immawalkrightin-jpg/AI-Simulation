using System.Numerics;

namespace Simulation.API.Scenarios;

// The seeded differences between one world and the next. Every figure stays inside Cold War limits: the
// same countries, alliances, fronts and tiers of power, with other stockpiles, forces, moods and positions.
// It draws from its own random stream, so it never shifts the draws of the world or its sensors.
public sealed class ScenarioVariation
{
    public const double StockSpread = 0.10;
    public const double RelationSpread = 0.08;
    public const float MinimumReach = 0.8f;
    private const double LineMargin = 0.01;
    private const int Stream = 0x19C01D;

    // Relation values the engine and the doctrine decide on. A varied relation never crosses one of these,
    // and one that sits exactly on a line stays there.
    private static readonly double[] Lines = { -0.8, -0.7, -0.3, -0.2, 0.0, 0.15, 0.3, 0.4, 0.7 };

    private readonly Random rng;

    public ScenarioVariation(int seed)
    {
        rng = new Random(seed ^ Stream);
    }

    public static IReadOnlyList<double> RelationLines => Lines;

    private double Spread(double limit) => (rng.NextDouble() * 2 - 1) * limit;

    public double Stock(double historical) => Math.Round(historical * (1 + Spread(StockSpread)));

    public double Relation(double historical)
    {
        var shift = Spread(RelationSpread);
        if (Lines.Any(line => Math.Abs(historical - line) < 1e-9))
        {
            return historical;
        }
        var low = Lines.Where(line => line < historical).Select(line => line + LineMargin).DefaultIfEmpty(-1.0).Max();
        var high = Lines.Where(line => line > historical).Select(line => line - LineMargin).DefaultIfEmpty(1.0).Min();
        return Math.Clamp(historical + shift, low, high);
    }

    // A quarter of the countries field one more infantry unit, a quarter field one fewer. The last
    // infantry unit is never taken away.
    public string Roster(string historical)
    {
        var roll = rng.NextDouble();
        if (roll < 0.25)
        {
            return historical + "I";
        }
        if (roll < 0.5 && historical.Count(code => code == 'I') > 1)
        {
            return historical.Remove(historical.LastIndexOf('I'), 1);
        }
        return historical;
    }

    // How one country's land and air units are turned around their anchor and how far out they stand.
    public (float Turn, float Reach) Deployment() =>
        ((float)(rng.NextDouble() * Math.PI * 2), MinimumReach + (float)rng.NextDouble() * (1 - MinimumReach));

    // A small shift of one ship around its berth.
    public Vector2 Anchorage() => new((float)Spread(1.5), (float)Spread(1.5));
}
