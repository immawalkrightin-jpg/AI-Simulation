namespace WorldEngine.Sensors;

public interface IObservationSource
{
    IReadOnlyList<Observation> Collect(long tick);
}
