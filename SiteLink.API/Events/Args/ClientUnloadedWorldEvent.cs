namespace SiteLink.API.Events.Args;

public class SessionUnloadedWorldEvent : BaseEvent
{
    public SessionUnloadedWorldEvent(Session session, World world)
    {
        Session = session;
        World = world;
    }

    public Session Session { get; }
    public World World { get; }
}