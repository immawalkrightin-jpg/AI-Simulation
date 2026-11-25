using System.Numerics;
using Simulation.API.Scenarios;
using Simulation.API.Services;
using WorldEngine.Interactions;
using WorldEngine.Units;
using Xunit;

namespace Simulation.Tests;

public class CarrierAndSpacingTests
{
    [Theory]
    [InlineData(InteractionKind.SupplyShipment, TransportMode.Ground, "Truck")]
    [InlineData(InteractionKind.SupplyShipment, TransportMode.Naval, "CargoShip")]
    [InlineData(InteractionKind.SupplyShipment, TransportMode.Air, "Transport")]
    [InlineData(InteractionKind.SupplyShipment, TransportMode.None, null)]
    [InlineData(InteractionKind.Negotiation, TransportMode.Air, null)]
    [InlineData(InteractionKind.Clash, TransportMode.Ground, null)]
    [InlineData(InteractionKind.MissileStrike, TransportMode.Air, null)]
    public void CarrierFollowsTheModeOfASupplyShipmentOnly(InteractionKind kind, TransportMode mode, string? expected)
    {
        Assert.Equal(expected, GroundTruthMapper.CarrierOf(kind, mode));
    }

    [Fact]
    public void SnapshotShipmentsCarryTheCarrierOfTheirCurrentLeg()
    {
        var simulation = DefaultScenario.Build(42);
        var seen = new HashSet<string>();
        for (var step = 0; step < 40; step++)
        {
            simulation.Run(50);
            foreach (var item in GroundTruthMapper.Map(simulation).Interactions)
            {
                if (item.Kind == "SupplyShipment")
                {
                    if (item.Route is null)
                    {
                        var expected = item.Mode switch
                        {
                            "Ground" => "Truck",
                            "Naval" => "CargoShip",
                            "Air" => "Transport",
                            _ => null
                        };
                        Assert.Equal(expected, item.Carrier);
                    }
                    else
                    {
                        Assert.Contains(item.Route.Legs, l => l.Carrier == item.Carrier);
                    }
                    seen.Add(item.Mode);
                }
                else
                {
                    Assert.Null(item.Carrier);
                }
            }
        }
        Assert.NotEmpty(seen);
    }

    [Fact]
    public void UnitScatterStaysWithinFortyPercentOfTheNearestOtherCapital()
    {
        var state = DefaultScenario.Build(42).State;
        var countries = state.Countries.All;
        foreach (var unit in state.Units.Where(u => u.Kind.DomainOf() != Domain.Sea))
        {
            var home = countries.Single(c => c.Id == unit.Owner);
            var nearest = countries.Where(c => c.Id != home.Id).Min(c => Vector2.Distance(home.Capital, c.Capital));
            var distance = Vector2.Distance(unit.Position, home.Capital);
            Assert.True(distance <= nearest * 0.4f + 0.01f, $"{unit.Owner.Value} {unit.Kind} is {distance} from its capital, limit {nearest * 0.4f}");
            Assert.True(distance <= 37.01f);
        }
    }

    [Fact]
    public void UnitsSitCloserToTheirOwnCapitalThanToAnyOther()
    {
        var state = DefaultScenario.Build(42).State;
        var countries = state.Countries.All;
        foreach (var unit in state.Units.Where(u => u.Kind.DomainOf() != Domain.Sea))
        {
            var own = countries.Single(c => c.Id == unit.Owner);
            var ownDistance = Vector2.Distance(unit.Position, own.Capital);
            foreach (var other in countries.Where(c => c.Id != own.Id))
            {
                Assert.True(ownDistance < Vector2.Distance(unit.Position, other.Capital));
            }
        }
    }

    [Fact]
    public void UnitPlacementIsDeterministicForASeed()
    {
        var first = DefaultScenario.Build(7).State.Units.Select(u => (u.Owner.Value, u.Kind, u.Position)).ToList();
        var second = DefaultScenario.Build(7).State.Units.Select(u => (u.Owner.Value, u.Kind, u.Position)).ToList();
        Assert.Equal(first, second);
    }
}
