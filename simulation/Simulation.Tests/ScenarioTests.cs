using Simulation.API.Scenarios;
using Simulation.API.Services;
using WorldEngine.Countries;
using Xunit;

namespace Simulation.Tests;

public class ScenarioTests
{
    [Fact]
    public void ScenarioHasTwentySixCountriesAcrossAllBlocs()
    {
        var state = DefaultScenario.Build(42).State;
        Assert.Equal(26, state.Countries.All.Count);
        Assert.Equal(11, state.Countries.All.Count(c => c.Bloc == Bloc.East));
        Assert.Equal(10, state.Countries.All.Count(c => c.Bloc == Bloc.West));
        Assert.Equal(5, state.Countries.All.Count(c => c.Bloc == Bloc.NonAligned));
    }

    [Fact]
    public void SovietUnionIsTheModelCountryAndOwnsTheSensors()
    {
        var simulation = DefaultScenario.Build(42);
        Assert.Equal(new CountryId("USSR"), simulation.State.ModelCountry);
        Assert.NotNull(simulation.Sensors);
        Assert.All(simulation.Sensors!.Sensors, s => Assert.Equal(new CountryId("USSR"), s.Owner));
    }

    [Fact]
    public void SnapshotCarriesBlocsAndEveryRelationPair()
    {
        var snapshot = GroundTruthMapper.Map(DefaultScenario.Build(42));
        Assert.All(snapshot.Countries, c => Assert.Contains(c.Bloc, new[] { "East", "West", "NonAligned" }));
        var count = snapshot.Countries.Count;
        Assert.Equal(count * (count - 1) / 2, snapshot.Relations.Count);
    }

    [Fact]
    public void SovietUnionNeverFightsTheWestDirectly()
    {
        var simulation = DefaultScenario.Build(42);
        simulation.Run(1500);
        var westNames = simulation.State.Countries.All.Where(c => c.Bloc == Bloc.West).Select(c => c.Name).ToList();
        var direct = simulation.State.Log.Records.Where(r =>
            (r.Kind.StartsWith("clash") || r.Kind.StartsWith("missile"))
            && r.Description.Contains("Soviet Union")
            && westNames.Any(name => r.Description.Contains(name)));
        Assert.Empty(direct);
    }

    [Fact]
    public void LongRunProducesEveryKindOfInteraction()
    {
        var simulation = DefaultScenario.Build(42);
        simulation.Run(2000);
        var kinds = simulation.State.Log.Records.Select(r => r.Kind).ToHashSet();
        Assert.Contains("negotiation_started", kinds);
        Assert.Contains("supply_delivered", kinds);
        Assert.Contains("clash_started", kinds);
        Assert.Contains("missile_launched", kinds);
    }

    [Fact]
    public void ScenarioIsDeterministicForASeed()
    {
        var first = DefaultScenario.Build(7);
        var second = DefaultScenario.Build(7);
        first.Run(400);
        second.Run(400);
        Assert.Equal(
            first.State.Log.Records.Select(r => (r.Tick, r.Kind, r.Description)),
            second.State.Log.Records.Select(r => (r.Tick, r.Kind, r.Description)));
    }
}
