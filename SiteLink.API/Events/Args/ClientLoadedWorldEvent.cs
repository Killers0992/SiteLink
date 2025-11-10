namespace SiteLink.API.Events.Args;

public class ClientLoadedWorldEvent : BaseEvent
{
    public ClientLoadedWorldEvent(Client client, World world)
    {
        Client = client;
        World = world;
    }

    public Client Client { get; }
    public World World { get; }
}
