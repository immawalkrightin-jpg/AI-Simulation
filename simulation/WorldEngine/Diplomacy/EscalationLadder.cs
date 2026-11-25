using WorldEngine.Countries;
using WorldEngine.World;

namespace WorldEngine.Diplomacy;

public sealed class FrontState
{
    public FrontState(ProxyFront front)
    {
        Front = front;
        Rung = front.Start;
    }

    public ProxyFront Front { get; }
    public Rung Rung { get; internal set; }
    public long LastChangeTick { get; internal set; }
}

public readonly record struct LadderMove(FrontState State, Rung From, Rung To);

// One rung per front: tension, incident, clash, missile. Rungs rise only while the pair is hostile and fall
// again after a quiet period or successful ceasefire talks, so relations settle instead of oscillating.
public sealed class EscalationLadder
{
    public const int CheckInterval = 5;
    public const int CooldownTicks = 60;
    public const double EscalationChance = 0.15;

    private readonly List<FrontState> states;

    public EscalationLadder(Doctrine doctrine)
    {
        states = doctrine.Fronts.Select(f => new FrontState(f)).ToList();
    }

    public IReadOnlyList<FrontState> Fronts => states;

    public FrontState? StateOf(CountryId a, CountryId b) =>
        states.FirstOrDefault(s =>
            (s.Front.First == a && s.Front.Second == b) || (s.Front.First == b && s.Front.Second == a));

    public Rung RungOf(CountryId a, CountryId b) => StateOf(a, b)?.Rung ?? Rung.Calm;

    public static double Hostility(double relation) => Math.Clamp((-relation - 0.2) / 0.8, 0.0, 1.0);

    public IReadOnlyList<LadderMove> Step(WorldState world)
    {
        var tick = world.Clock.Tick;
        var moves = new List<LadderMove>();
        foreach (var state in states)
        {
            var front = state.Front;
            var hostility = Hostility(world.Relations.Get(front.First, front.Second));
            if (state.Rung < front.Ceiling)
            {
                var roll = world.Rng.NextDouble();
                if (roll < EscalationChance * hostility)
                {
                    var from = state.Rung;
                    state.Rung = from + 1;
                    state.LastChangeTick = tick;
                    moves.Add(new LadderMove(state, from, state.Rung));
                    continue;
                }
            }
            if (state.Rung > Rung.Calm && tick - state.LastChangeTick >= CooldownTicks)
            {
                moves.Add(Lower(state, tick)!.Value);
            }
        }
        return moves;
    }

    public LadderMove? Cool(CountryId a, CountryId b, long tick)
    {
        var state = StateOf(a, b);
        return state is null || state.Rung == Rung.Calm ? null : Lower(state, tick);
    }

    private static LadderMove? Lower(FrontState state, long tick)
    {
        var from = state.Rung;
        state.Rung = from - 1;
        state.LastChangeTick = tick;
        return new LadderMove(state, from, state.Rung);
    }
}
