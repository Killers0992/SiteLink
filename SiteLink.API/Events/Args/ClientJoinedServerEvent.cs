namespace SiteLink.API.Events.Args;

public class ClientJoinedServerEvent : BaseEvent
{
    public ClientJoinedServerEvent(Client client, Server server)
    {
        Client = client;
        Server = server;
    }

    public Client Client { get; }
    public Server Server { get; }
}
