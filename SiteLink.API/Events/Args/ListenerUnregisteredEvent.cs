namespace SiteLink.API.Events.Args;

public class ListenerUnregisteredEvent : BaseEvent
{
    public ListenerUnregisteredEvent(Listener listener)
    {
        Listener = listener;
    }

    public Listener Listener { get; }
}