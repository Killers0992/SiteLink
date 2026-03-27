namespace SiteLink.API.Events.Args;

public class SessionLoadedWorldEvent : BaseEvent
{
    public SessionLoadedWorldEvent(Session session, World world)
    {
        Session = session;
        World = world;
    }

    public Session Session { get; }
    public World World { get; }
}
