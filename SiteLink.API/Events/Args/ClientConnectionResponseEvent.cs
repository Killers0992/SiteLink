using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Events.Args;

public class ClientConnectionResponseEvent : BaseCancellableEvent
{
    public ClientConnectionResponseEvent(RemoteConnection connection, Server server, IDisconnectResponse response)
    {
        Connection = connection;
        Server = server;
        Response = response;
    }

    public RemoteConnection Connection { get; }

    public Server Server { get; }

    public IDisconnectResponse Response { get; }
}