using SiteLink.API.Events.Args;
using static SiteLink.API.Events.EventManager;

namespace SiteLink.API.Events;

public class EventManager
{
    public delegate void CustomEventHandler<in TEvent>(TEvent ev)
        where TEvent : BaseEvent;

    public static ListenerEvents Listener { get; } = new ListenerEvents();

    public static ServerEvents Server { get; } = new ServerEvents();

    public static ClientEvents Client { get; } = new ClientEvents();
}

public class ListenerEvents
{
    public event CustomEventHandler<ListenerRegisteredEvent> ListenerRegistered;
    public void InvokeListenerRegistered(ListenerRegisteredEvent ev) => ListenerRegistered?.InvokeWithExceptionHandler(ev);

    public event CustomEventHandler<ListenerUnregisteredEvent> ListenerUnregisteredEvent;
    public void InvokeListenerUnregistered(ListenerUnregisteredEvent ev) => ListenerUnregisteredEvent?.InvokeWithExceptionHandler(ev);
}

public class ServerEvents
{
    public event CustomEventHandler<ServerRegisteredEvent> ServerRegistered;
    public void InvokeServerRegistered(ServerRegisteredEvent ev) => ServerRegistered?.InvokeWithExceptionHandler(ev);

    public event CustomEventHandler<ServerUnregisteredEvent> ServerUnregisteredEvent;
    public void InvokeServerUnregistered(ServerUnregisteredEvent ev) => ServerUnregisteredEvent?.InvokeWithExceptionHandler(ev);
}

public class ClientEvents
{
    public event CustomEventHandler<ClientConnectingToListenerEvent> ConnectingToListener;
    public void InvokeConnectingToListener(ClientConnectingToListenerEvent ev) => ConnectingToListener?.InvokeWithExceptionHandler(ev);

    public event CustomEventHandler<ClientJoinedServerEvent> JoinedServer;
    public void InvokeJoinedServer(ClientJoinedServerEvent ev) => JoinedServer?.InvokeWithExceptionHandler(ev);

    public event CustomEventHandler<ClientLoadedWorldEvent> LoadedWorld;
    public void InvokeLoadedWorld(ClientLoadedWorldEvent ev) => LoadedWorld?.InvokeWithExceptionHandler(ev);

    public event CustomEventHandler<ClientUnloadedWorldEvent> UnloadedWorld;
    public void InvokeUnloadedWorld(ClientUnloadedWorldEvent ev) => UnloadedWorld?.InvokeWithExceptionHandler(ev);
}