namespace SiteLink.API.Events.Args;

public class ClientConnectionResponseEvent : BaseCancellableEvent
{
    public ClientConnectionResponseEvent(Client client, Server server, IDisconnectResponse response)
    {
        Client = client;
        Server = server;
        Response = response;
    }

    public Client Client { get; }

    public Server Server { get; }

    public IDisconnectResponse Response { get; }
}