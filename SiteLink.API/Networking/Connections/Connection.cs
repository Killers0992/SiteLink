using SiteLink.API.Metrics;
using SiteLink.API.Threading;

namespace SiteLink.API.Networking.Connections;

[ThreadAffined("Listener")]
public class Connection : IDisposable
{
    private readonly int _ownerThreadId;

    public ConnectionStats Stats { get; } = new ConnectionStats();

    /// <summary>
    /// Gets the tag used for logging and identification.
    /// </summary>
    public string Tag
    {
        get
        {
            string listenerTag = Listener != null ? Listener.Tag : "[(f=cyan)unknown-listener(f=white)]";

            string user = PreAuth.UserId;

            if (string.IsNullOrEmpty(user))
                return $"{listenerTag} [(f=green){Peer.Id}(f=white)]";

            return $"{listenerTag} [(f=green){user}(f=white)]";
        }
    }

    /// <summary>
    /// Gets the listener associated with this instance, which is responsible for handling events.
    /// </summary>
    public Listener Listener { get; }

    /// <summary>
    /// Gets the pre-authentication information for this connection.
    /// </summary>
    public PreAuth PreAuth { get; private set; }

    /// <summary>
    /// Gets the connection request associated with this instance.
    /// </summary>
    public ConnectionRequest Request { get; private set; }

    /// <summary>
    /// Gets the network peer associated with this connection instance.
    /// </summary>
    public LiteNetPeer Peer { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the object has been disposed.
    /// </summary>
    /// <remarks>This property is set to <see langword="true"/> after the object has been disposed,
    /// indicating that it can no longer be used. Attempting to use the object after it has been disposed may result
    /// in exceptions.</remarks>
    public bool IsDisposed { get; private set; }

    public Connection(Listener listener, ConnectionRequest request, PreAuth preAuth)
    {
        Listener = listener;
        Request = request;

        PreAuth = preAuth;

        _ownerThreadId = listener.OwnerThreadId;
        ThreadOwner.Register(this, listener.Name + ":" + preAuth.UserId, _ownerThreadId);
    }

    /// <summary>
    /// Accepts the pending connection request.
    /// </summary>
    public LiteNetPeer AcceptRequest()
    {
        if (Request == null)
            return null;

        Peer = Request.Accept();

        Listener.Connections.TryAdd(Peer.Id, this);

        Request = null;

        return Peer;
    }

    public void SendToConnection(byte[] bytes, int position, int length, DeliveryMethod method)
    {
        if (Peer == null)
            return;

        Stats.RecordBytesSent(length);
        Peer.Send(bytes, position, length, method);
    }

    /// <summary>
    /// Marshals an action to execute on this connection's owning thread (Listener polling thread).
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Execute(Action action)
    {
        if (Thread.CurrentThread.ManagedThreadId == _ownerThreadId)
        {
            action();
        }
        else
        {
            Scheduler.Execute(this, action);
        }
    }

    public virtual void Update()
    {

    }

    public virtual void ReceiveDataFromListener(int length, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {

    }

    public virtual void Disconnect(string message = null)
    {
        if (Request != null)
        {
            Request.RejectWithMessage(message);

            Dispose();

            SiteLinkLogger.Info($"{Tag} Disconnected{(string.IsNullOrEmpty(message) ? string.Empty : $" with reason '(f=yellow){message}(f=white)'")}");
            return;
        }

        Peer.Disconnect();
    }

    public virtual void Disconnected() { }

    public void Dispose()
    {
        Disconnected();

        IsDisposed = true;
    }
}
