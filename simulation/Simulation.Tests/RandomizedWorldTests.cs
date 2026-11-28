using System.Numerics;
using Simulation.API.Scenarios;
using WorldEngine.Countries;
using WorldEngine.Diplomacy;
using WorldEngine.Interactions;
using WorldEngine.Units;
using WorldEngine.World;
using Xunit;

namespace Simulation.Tests;

public class RandomizedWorldTests
{
    private const int Historical = DefaultScenario.HistoricalSeed;

    private static readonly string[] Superpowers = { "USSR", "USA" };
    private static readonly string[] RegionalPowers = { "PRC", "GBR", "FRA", "FRG", "IND", "JPN", "ITA", "TUR", "KOR" };
    private static readonly string[] SmallStates = { "HUN", "ROU", "BGR", "CUB" };

    private static CountryId Id(string value) => new(value);

    private static IEnumerable<(Country First, Country Second)> Pairs(WorldState state)
    {
        var all = state.Countries.All;
        for (var i = 0; i < all.Count; i++)
        {
            for (var j = i + 1; j < all.Count; j++)
            {
                yield return (all[i], all[j]);
            }
        }
    }

    [Fact]
    public void TheDefaultSeedKeepsTheHistoricalSetUp()
    {
        var state = DefaultScenario.Build(Historical).State;
        Assert.Equal(1200, state.Countries.Get(Id("USSR")).Supplies);
        Assert.Equal(1000, state.Countries.Get(Id("USSR")).Ammunition);
        Assert.Equal(900, state.Countries.Get(Id("PRC")).Supplies);
        Assert.Equal(-0.75, state.Relations.Get(Id("USSR"), Id("USA")), 6);
        Assert.Equal(0.1, state.Relations.Get(Id("USSR"), Id("PRC")), 6);
        Assert.Equal(-0.9, state.Relations.Get(Id("PRK"), Id("KOR")), 6);
        Assert.Equal(25, state.Units.Count(u => u.Owner == Id("USSR")));
        Assert.Equal(24, state.Units.Count(u => u.Owner == Id("USA")));
    }

    [Fact]
    public void TheDefaultSeedIsTheOneTheServerStartsWith()
    {
        Assert.Equal(Historical, new Simulation.API.Services.SimulationOptions().Seed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1000)]
    public void ASeedAlwaysBuildsTheSameWorld(int seed)
    {
        var first = DefaultScenario.Build(seed).State;
        var second = DefaultScenario.Build(seed).State;
        Assert.Equal(
            first.Countries.All.Select(c => (c.Id.Value, c.Supplies, c.Ammunition)),
            second.Countries.All.Select(c => (c.Id.Value, c.Supplies, c.Ammunition)));
        Assert.Equal(
            Pairs(first).Select(p => first.Relations.Get(p.First.Id, p.Second.Id)),
            Pairs(second).Select(p => second.Relations.Get(p.First.Id, p.Second.Id)));
        Assert.Equal(
            first.Units.Select(u => (u.Owner.Value, u.Kind, u.Position)),
            second.Units.Select(u => (u.Owner.Value, u.Kind, u.Position)));
    }

    [Fact]
    public void OtherSeedsChangeStockpilesRelationsForcesAndPositions()
    {
        var historical = DefaultScenario.Build(Historical).State;
        var varied = DefaultScenario.Build(1).State;
        var other = DefaultScenario.Build(2).State;
        Assert.NotEqual(
            historical.Countries.All.Select(c => c.Supplies),
            varied.Countries.All.Select(c => c.Supplies));
        Assert.NotEqual(
            Pairs(historical).Select(p => historical.Relations.Get(p.First.Id, p.Second.Id)),
            Pairs(varied).Select(p => varied.Relations.Get(p.First.Id, p.Second.Id)));
        Assert.NotEqual(
            historical.Units.Select(u => u.Position),
            varied.Units.Select(u => u.Position));
        Assert.NotEqual(
            varied.Units.Select(u => u.Position),
            other.Units.Select(u => u.Position));
        var counts = Enumerable.Range(1, 5)
            .Select(seed => DefaultScenario.Build(seed).State.Units.Count)
            .Distinct()
            .Count();
        Assert.True(counts > 1, "every seed fielded the same number of units");
    }

    [Fact]
    public void VariationStaysInsideColdWarLimits()
    {
        var historical = DefaultScenario.Build(Historical).State;
        for (var seed = 0; seed < 60; seed++)
        {
            var state = DefaultScenario.Build(seed).State;
            foreach (var country in state.Countries.All)
            {
                var original = historical.Countries.Get(country.Id);
                Assert.InRange(
                    country.Supplies,
                    original.Supplies * (1 - ScenarioVariation.StockSpread) - 1,
                    original.Supplies * (1 + ScenarioVariation.StockSpread) + 1);
                Assert.InRange(
                    country.Ammunition,
                    original.Ammunition * (1 - ScenarioVariation.StockSpread) - 1,
                    original.Ammunition * (1 + ScenarioVariation.StockSpread) + 1);
                var fielded = state.Units.Count(u => u.Owner == country.Id);
                var before = historical.Units.Count(u => u.Owner == country.Id);
                Assert.InRange(fielded, before - 1, before + 1);
                Assert.Contains(state.Units, u => u.Owner == country.Id && u.Kind == UnitKind.Infantry);
            }
            foreach (var (first, second) in Pairs(state))
            {
                var now = state.Relations.Get(first.Id, second.Id);
                var then = historical.Relations.Get(first.Id, second.Id);
                Assert.True(
                    Math.Abs(now - then) <= ScenarioVariation.RelationSpread + 1e-9,
                    $"seed {seed}: {first.Id.Value}-{second.Id.Value} moved from {then} to {now}");
                foreach (var line in ScenarioVariation.RelationLines)
                {
                    Assert.True(
                        Math.Sign(Math.Round(now - line, 9)) == Math.Sign(Math.Round(then - line, 9)),
                        $"seed {seed}: {first.Id.Value}-{second.Id.Value} crossed {line}, from {then} to {now}");
                }
            }
        }
    }

    [Fact]
    public void TiersOfPowerHoldOnEverySeed()
    {
        for (var seed = 0; seed < 60; seed++)
        {
            var state = DefaultScenario.Build(seed).State;
            double Power(string id) => NationalPower.Of(state, Id(id)).Total;
            Assert.True(Superpowers.Min(id => Power(id)) > RegionalPowers.Max(id => Power(id)), $"seed {seed}");
            Assert.True(RegionalPowers.Min(id => Power(id)) > SmallStates.Max(id => Power(id)), $"seed {seed}");
        }
    }

    [Fact]
    public void EverySeedStartsOnLegalGroundAndCloseToHome()
    {
        for (var seed = 0; seed < 60; seed++)
        {
            var state = DefaultScenario.Build(seed).State;
            var countries = state.Countries.All;
            foreach (var unit in state.Units)
            {
                Assert.True(
                    state.Territory!.Permits(unit.Kind.DomainOf(), unit.Owner, unit.Position),
                    $"seed {seed}: {unit.Owner.Value} {unit.Kind} starts at {unit.Position}");
                if (unit.Kind.DomainOf() == Domain.Sea)
                {
                    continue;
                }
                var home = state.Countries.Get(unit.Owner);
                var nearest = countries.Where(c => c.Id != home.Id).Min(c => Vector2.Distance(home.Capital, c.Capital));
                var distance = Vector2.Distance(unit.Position, home.Capital);
                Assert.True(distance <= nearest * 0.4f + 0.01f, $"seed {seed}: {unit.Owner.Value} {unit.Kind} strayed {distance}");
                Assert.True(distance <= 37.01f);
            }
        }
    }

    [Theory]
    [InlineData(42)]
    [InlineData(3)]
    [InlineData(11)]
    [InlineData(2024)]
    public void WindowAInvariantsHoldOverALongRun(int seed)
    {
        var simulation = DefaultScenario.Build(seed);
        var state = simulation.State;
        var doctrine = state.Doctrine!;
        var map = state.Territory!;
        for (var block = 0; block < 60; block++)
        {
            simulation.Run(25);
            var tick = state.Clock.Tick;
            foreach (var unit in state.Units)
            {
                var domain = unit.Kind.DomainOf();
                var legal = map.Permits(domain, unit.Owner, unit.Position)
                    || (domain == Domain.Land
                        && !map.IsSea(unit.Position)
                        && state.Occupations.HolderAt(unit.Position) == unit.Owner);
                Assert.True(legal, $"seed {seed} tick {tick}: {unit.Owner.Value} {unit.Kind} at {unit.Position}");
                Assert.InRange(unit.Strength, 0, UnitCatalog.Of(unit.Kind).Strength);
            }
            foreach (var country in state.Countries.All)
            {
                Assert.True(country.Supplies >= 0 && country.Ammunition >= 0, $"seed {seed} tick {tick}: {country.Name}");
            }
            foreach (var (first, second) in Pairs(state))
            {
                Assert.InRange(state.Relations.Get(first.Id, second.Id), -1.0, 1.0);
            }
            foreach (var front in state.Escalation!.Fronts)
            {
                Assert.InRange(front.Rung, Rung.Calm, front.Front.Ceiling);
            }
            foreach (var item in state.Interactions.All)
            {
                if (item.Kind is InteractionKind.Clash or InteractionKind.MissileStrike)
                {
                    Assert.True(doctrine.MayFight(item.Source, item.Target), $"seed {seed}: {item.Source.Value} fought {item.Target.Value}");
                }
                if (item.Kind == InteractionKind.SupplyShipment)
                {
                    Assert.NotNull(item.Route);
                    var source = state.Countries.Get(item.Source);
                    var target = state.Countries.Get(item.Target);
                    Assert.True(
                        state.Routes!.IsLegal(item.Route!, source, target, state.Countries.All, _ => false),
                        $"seed {seed}: shipment {item.Id} has an illegal route");
                }
            }
            foreach (var occupation in state.Occupations.All)
            {
                Assert.Equal(occupation.Loser, map.OwnerAt(occupation.Center));
                Assert.True(doctrine.MayFight(occupation.Holder, occupation.Loser));
            }
        }
    }
}
