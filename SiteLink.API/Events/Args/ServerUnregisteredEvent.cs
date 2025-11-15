namespace SiteLink.API.Events.Args;

public class ServerUnregisteredEvent : BaseEvent
{
    public ServerUnregisteredEvent(Server server)
    {
        Server = server;
    }

    public Server Server { get; }
}