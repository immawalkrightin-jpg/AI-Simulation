using System.Numerics;
using WorldEngine.Countries;

namespace WorldEngine.Units;

public sealed class Unit
{
    public int Id { get; }
    public CountryId Owner { get; }
    public UnitKind Kind { get; }
    public Vector2 Position { get; private set; }
    public Vector2? Destination { get; private set; }
    public double Speed { get; }
    public double Strength { get; private set; }
    public UnitStatus Status { get; private set; }

    public Unit(int id, CountryId owner, UnitKind kind, Vector2 position, double speed, double strength)
    {
        Id = id;
        Owner = owner;
        Kind = kind;
        Position = position;
        Speed = speed;
        Strength = strength;
        Status = UnitStatus.Idle;
    }

    public void OrderMove(Vector2 destination)
    {
        if (Status == UnitStatus.Destroyed)
        {
            return;
        }
        Destination = destination;
        Status = UnitStatus.Moving;
    }

    public void ApplyDamage(double amount)
    {
        if (Status == UnitStatus.Destroyed)
        {
            return;
        }
        Strength = Math.Max(0, Strength - amount);
        if (Strength == 0)
        {
            Status = UnitStatus.Destroyed;
            Destination = null;
        }
    }

    public void Step(double seconds)
    {
        if (Status != UnitStatus.Moving || Destination is null)
        {
            return;
        }
        var target = Destination.Value;
        var offset = target - Position;
        var distance = offset.Length();
        var travel = (float)(Speed * seconds);
        if (distance <= travel)
        {
            Position = target;
            Destination = null;
            Status = UnitStatus.Idle;
            return;
        }
        Position += offset / distance * travel;
    }
}
