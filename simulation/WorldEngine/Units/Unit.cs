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

    // Takes cover where it stands. Only a unit that is standing idle can lie in wait.
    public void Conceal()
    {
        if (Status == UnitStatus.Idle)
        {
            Status = UnitStatus.Ambushing;
        }
    }

    public void Reveal()
    {
        if (Status == UnitStatus.Ambushing)
        {
            Status = UnitStatus.Idle;
        }
    }

    // Stops a moving unit where it is.
    public void Halt()
    {
        if (Status == UnitStatus.Moving)
        {
            Destination = null;
            Status = UnitStatus.Idle;
        }
    }

    // Puts a unit at a point at once. Used when ground is handed back and its garrison pulls out.
    public void Relocate(Vector2 point)
    {
        if (Status == UnitStatus.Destroyed)
        {
            return;
        }
        Position = point;
        Destination = null;
        Status = UnitStatus.Idle;
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
