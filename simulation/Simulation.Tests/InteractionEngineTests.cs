using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Interactions;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class InteractionEngineTests
{
    private static readonly CountryId A = new("A");
    private static readonly CountryId B = new("B");
    private static readonly CountryId C = new("C");

    private static WorldSimulation Build(int seed, IEnumerable<(CountryId, CountryId)>? noDirect = null)
    {
        var world = new WorldState(seed, 2000, 1000);
        world.Countries.Add(new Country(A, "Alpha", new Vector2(200, 300), 600, 500, Bloc.East, "L1"));
        world.Countries.Add(new Country(B, "Bravo", new Vector2(900, 400), 600, 500, Bloc.West, "L2"));
        world.Countries.Add(new Country(C, "Charlie", new Vector2(500, 700), 600, 500, Bloc.NonAligned, "L1"));
        world.Relations.Set(A, B, -0.95);
        world.Relations.Set(A, C, 0.6);
        world.Relations.Set(B, C, 0.4);
        return new WorldSimulation(world, null, null, new InteractionEngine(noDirect, spawnChance: 1.0));
    }

    [Fact]
    public void RunningProducesAndResolvesInteractions()
    {
        var simulation = Build(5);
        simulation.Run(300);
        var kinds = simulation.State.Log.Records.Select(r => r.Kind).ToHashSet();
        Assert.Contains("negotiation_started", kinds);
        Assert.Contains("supply_dispatched", kinds);
        Assert.Contains(kinds, k => k == "negotiation_concluded" || k == "negotiation_cancelled");
        Assert.Contains(kinds, k => k == "supply_delivered" || k == "supply_intercepted");
    }

    [Fact]
    public void ForbiddenPairsNeverClashOrLaunchMissiles()
    {
        var simulation = Build(5, new[] { (A, B) });
        simulation.Run(300);
        Assert.DoesNotContain(
            simulation.State.Log.Records,
            r => r.Kind.StartsWith("clash") || r.Kind.StartsWith("missile"));
    }

    [Fact]
    public void HostilePairsEventuallyClash()
    {
        var simulation = Build(5);
        simulation.Run(300);
        Assert.Contains(simulation.State.Log.Records, r => r.Kind == "clash_started");
    }

    [Fact]
    public void SameSeedGivesSameHistory()
    {
        var first = Build(9);
        var second = Build(9);
        first.Run(200);
        second.Run(200);
        Assert.Equal(
            first.State.Log.Records.Select(r => (r.Tick, r.Kind, r.Description)),
            second.State.Log.Records.Select(r => (r.Tick, r.Kind, r.Description)));
    }

    [Fact]
    public void FinishedInteractionsFadeOutOfTheVisibleSet()
    {
        var simulation = Build(5);
        simulation.Run(300);
        var tick = simulation.State.Clock.Tick;
        foreach (var item in simulation.State.Interactions.Visible(tick, InteractionEngine.FadeTicks))
        {
            Assert.True(item.EndTick is null || tick - item.EndTick.Value <= InteractionEngine.FadeTicks);
        }
    }
}
