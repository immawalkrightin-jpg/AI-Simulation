using Simulation.API.Contracts.GroundTruth;
using WorldEngine.Sensors;
using WorldEngine.World;

namespace Simulation.API.Services;

public static class GroundTruthMapper
{
    public static GroundTruthSnapshot Map(WorldSimulation simulation)
    {
        var state = simulation.State;
        var countries = state.Countries.All;
        var relations = new List<RelationDto>();
        for (var i = 0; i < countries.Count; i++)
        {
            for (var j = i + 1; j < countries.Count; j++)
            {
                var first = countries[i].Id;
                var second = countries[j].Id;
                relations.Add(new RelationDto(
                    first.Value,
                    second.Value,
                    state.Relations.Get(first, second),
                    state.Relations.StateOf(first, second).ToString()));
            }
        }
        var sensors = simulation.Sensors?.Sensors ?? Array.Empty<Sensor>();
        return new GroundTruthSnapshot(
            state.Clock.Tick,
            state.Clock.ElapsedSeconds,
            state.Width,
            state.Height,
            countries
                .Select(c => new CountryDto(c.Id.Value, c.Name, c.Capital.X, c.Capital.Y, c.Supplies, c.Ammunition))
                .ToList(),
            state.Units
                .Select(u => new UnitDto(
                    u.Id,
                    u.Owner.Value,
                    u.Kind.ToString(),
                    u.Position.X,
                    u.Position.Y,
                    u.Destination?.X,
                    u.Destination?.Y,
                    u.Speed,
                    u.Strength,
                    u.Status.ToString()))
                .ToList(),
            relations,
            sensors
                .Select(s => new SensorDto(s.Id, s.Kind.ToString(), s.Owner.Value, s.Position.X, s.Position.Y, s.Range))
                .ToList(),
            state.Log.Records
                .TakeLast(50)
                .Select(r => new EventDto(r.Tick, r.Kind, r.Description))
                .ToList());
    }
}
