namespace SiteLink.API.Events.Args;

public class ClientUnloadedWorldEvent : BaseEvent
{
    public ClientUnloadedWorldEvent(Client client, World world)
    {
        Client = client;
        World = world;
    }

    public Client Client { get; }
    public World World { get; }
}