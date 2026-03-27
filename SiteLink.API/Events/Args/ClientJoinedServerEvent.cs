namespace SiteLink.API.Events.Args;

public class SessionJoinedServerEvent : BaseEvent
{
    public SessionJoinedServerEvent(Session session, Server server)
    {
        Session = session;
        Server = server;
    }

    public Session Session { get; }
    public Server Server { get; }
}
