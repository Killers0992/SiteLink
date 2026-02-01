namespace SiteLink.API.Events.Args;

public class ClientConnectionResponseEvent : BaseCancellableEvent
{
    public ClientConnectionResponseEvent(Connection connection, Server server, IDisconnectResponse response)
    {
        Connection = connection;
        Server = server;
        Response = response;
    }

    public Connection Connection { get; }

    public Server Server { get; }

    public IDisconnectResponse Response { get; }
}