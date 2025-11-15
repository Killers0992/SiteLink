namespace SiteLink.API.Events.Args;

public class ClientConnectingToListenerEvent : BaseCancellableEvent
{
    public ClientConnectingToListenerEvent(Listener listener, ConnectionRequest request, PreAuth preAuth)
    {
        Listener = listener;
        ConnectionRequest = request;
        PreAuth = preAuth;
    }

    public Listener Listener { get; }

    public ConnectionRequest ConnectionRequest { get; }

    public PreAuth PreAuth { get; }
}