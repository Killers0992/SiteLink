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

    public event CustomEventHandler<ClientConnectionResponseEvent> ConnectionResponse;
    public void InvokeConnectionResponse(ClientConnectionResponseEvent ev) => ConnectionResponse?.InvokeWithExceptionHandler(ev);

    public event CustomEventHandler<SessionJoinedServerEvent> JoinedServer;
    public void InvokeJoinedServer(SessionJoinedServerEvent ev) => JoinedServer?.InvokeWithExceptionHandler(ev);

    public event CustomEventHandler<SessionLoadedWorldEvent> LoadedWorld;
    public void InvokeLoadedWorld(SessionLoadedWorldEvent ev) => LoadedWorld?.InvokeWithExceptionHandler(ev);

    public event CustomEventHandler<SessionUnloadedWorldEvent> UnloadedWorld;
    public void InvokeUnloadedWorld(SessionUnloadedWorldEvent ev) => UnloadedWorld?.InvokeWithExceptionHandler(ev);
}