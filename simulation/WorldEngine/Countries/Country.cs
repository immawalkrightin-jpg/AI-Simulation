using System.Numerics;

namespace WorldEngine.Countries;

public sealed class Country
{
    public CountryId Id { get; }
    public string Name { get; }
    public Vector2 Capital { get; }
    public double Supplies { get; private set; }
    public double Ammunition { get; private set; }

    public Country(CountryId id, string name, Vector2 capital, double supplies, double ammunition)
    {
        Id = id;
        Name = name;
        Capital = capital;
        Supplies = supplies;
        Ammunition = ammunition;
    }

    public void AddSupplies(double amount) => Supplies = Math.Max(0, Supplies + amount);

    public void AddAmmunition(double amount) => Ammunition = Math.Max(0, Ammunition + amount);
}
