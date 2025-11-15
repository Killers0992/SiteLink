namespace SiteLink.API.Events.Args;

public class ServerRegisteredEvent : BaseEvent
{
    public ServerRegisteredEvent(Server server)
    {
        Server = server;
    }

    public Server Server { get; }
}