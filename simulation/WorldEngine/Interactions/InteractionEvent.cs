using WorldEngine.Events;
using WorldEngine.World;

namespace WorldEngine.Interactions;

public sealed class InteractionEvent : WorldEvent
{
    private readonly string kind;
    private readonly string text;

    public InteractionEvent(string kind, string text)
    {
        this.kind = kind;
        this.text = text;
    }

    public override string Kind => kind;

    public override void Apply(WorldState world)
    {
    }

    public override string Describe() => text;
}
