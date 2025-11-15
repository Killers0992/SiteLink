namespace SiteLink.API.Events.Args;

public class ListenerRegisteredEvent : BaseEvent
{
    public ListenerRegisteredEvent(Listener listener)
    {
        Listener = listener;
    }

    public Listener Listener { get; }
}