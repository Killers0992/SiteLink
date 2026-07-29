namespace SiteLink.API.Networking.Connections;

public class RemoteConnection : Connection
{
    internal enum DisconnectDelivery
    {
        Transport,
        BootstrapSession,
        SessionRpc
    }

    public static Dictionary<string, RemoteConnection> ConnectionByUserId = new Dictionary<string, RemoteConnection>();

    public static bool TryGet(string userId, out RemoteConnection connection) => ConnectionByUserId.TryGetValue(userId, out connection);

    /// <summary>
    /// Gets or sets the current session associated with the user.
    /// </summary>
    public Session Session { get; set; }

    /// <summary>
    /// Gets the server instance of the MirrorSender used for sending mirror data to connected clients.
    /// </summary>
    public MirrorSender AsServer { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the connection is currently switching servers.
    /// </summary>
    public bool IsSwitchingServers { get; set; }

    /// <summary>
    /// Whether this client has ever had a spawned player object on any session.
    /// <para>
    /// Hints are gated on this rather than on <see cref="Networking.Session.IsSpawned"/>
    /// because recovery replaces the session: a player who was mid-round when the game
    /// server restarted is attached to a brand new, never-spawned session while the proxy
    /// waits, which is exactly when the "reconnecting..." hint has to be visible. The
    /// client-side HUD outlives the session, so the connection is the right owner of the flag.
    /// </para>
    /// </summary>
    public bool HasEverSpawned { get; internal set; }

    public RemoteConnection(Listener listener, ConnectionRequest request, PreAuth preAuth) : base(listener, request, preAuth)
    {
        AsServer = new MirrorSender(this,
            SiteLinkAPI.ThresholdBytes,
            NowSeconds,
            (bytes, offset, length, method) =>
            {
                SendToConnection(bytes, offset, length, method);
            });

        ConnectionByUserId.Add(PreAuth.UserId, this);
    }

    /// <summary>
    /// Connects to a server by name.
    /// </summary>
    /// <param name="name">The server name.</param>
    public bool Connect(string name, bool silent = false)
    {
        //ThreadOwner.Verify(this);
        return ConnectInternal(name, silent);
    }

    private bool ConnectInternal(string name, bool silent)
    {
        Server server = Server.Get<Server>(name: name);

        if (server == null)
        {
            Disconnect(
                TranslationManager.Format(
                        TranslationManager.Current.Connection.ServerNotFound,
                        TranslationContext.For(Session))
                    .Add("server", name)
                    .Add("server_name", name)
                    .Format());
            return false;
        }

        return ConnectInternal(server, silent);
    }

    /// <summary>
    /// Connects to a list of servers in order.
    /// </summary>
    /// <param name="servers">The server names.</param>
    public bool Connect(string[] servers, bool silent = false)
    {
        //ThreadOwner.Verify(this);
        return ConnectInternal(servers, silent);
    }

    public bool Connect(Server server, bool silent = false)
    {
        return ConnectInternal(server, silent);
    }

    private bool ConnectInternal(string[] servers, bool silent)
    {
        SiteLinkLogger.Info($"{Tag} Connect to (f=yellow){string.Join("(f=white) -> (f=yellow)", servers)}(f=white)");

        var serverNames = new HashSet<string>(servers.Select(s => s.ToLower()), StringComparer.OrdinalIgnoreCase);
        var serverObjs = new List<Server>(servers.Length);

        foreach (var server in Server.List)
        {
            if (serverNames.Contains(server.Name.ToLower()))
            {
                serverObjs.Add(server);
            }
        }

        return SessionManager.Singleton.CreateOrSwitchSession(this, serverObjs.ToArray(), silent) != null;
    }

    private bool ConnectInternal(Server server, bool silent)
    {
        return SessionManager.Singleton.CreateOrSwitchSession(this, new[] { server }, silent) != null;
    }

    private double NowSeconds() => Session == null ? 0 : Session.SessionTime.TotalSeconds;

    public override void Update()
    {
        AsServer?.Update();
        Session?.Update();
    }

    public override void ReceiveDataFromListener(int length, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        if (Session == null)
            return;

        byte[] bytes = reader.RawData;
        int position = reader.Position;

        if (!Listener.ClientToServer.TryRewrite(Session, bytes, position, length, out var outBytes, out var outPos, out var outLen, out bool pooled))
        {
            Session?.SendToServer(bytes, position, length, deliveryMethod);
            return;
        }

        Session?.SendToServer(outBytes, outPos, outLen, deliveryMethod);

        if (pooled && !ReferenceEquals(outBytes, bytes))
            Listener.ByteArrayPool.Return(outBytes);
    }

    public override void Disconnect(string message = null)
    {
        switch (SelectDisconnectDelivery(message, Request != null, Session != null))
        {
            case DisconnectDelivery.BootstrapSession:
                if (SessionManager.Singleton?.BeginDisconnect(this, message) == true)
                    return;
                break;
            case DisconnectDelivery.SessionRpc:
                Execute(() =>
                {
                    if (!IsDisposed)
                        SendDisconnectError(message);
                });
                return;
        }

        base.Disconnect(message);
    }

    internal static DisconnectDelivery SelectDisconnectDelivery(string message, bool hasRequest, bool hasSession)
    {
        if (message != null && hasRequest)
            return DisconnectDelivery.BootstrapSession;

        if (message != null && hasSession)
            return DisconnectDelivery.SessionRpc;

        return DisconnectDelivery.Transport;
    }

    internal void SendDisconnectError(string message)
    {
        Session session = Session;
        if (session == null)
            return;

        AsServer.Send(writer => WriteDisconnectError(writer, session.NetworkId, message));
    }

    internal static void WriteDisconnectError(NetworkWriter writer, uint networkId, string message)
    {
        writer.WriteUShort(NetworkMessages.RpcMessage);
        writer.WriteUInt(networkId);
        writer.WriteByte(1);
        writer.WriteUShort(unchecked((ushort)-2106075371));

        NetworkWriter messageWriter = new NetworkWriter();
        messageWriter.WriteString(message);

        writer.WriteArraySegmentAndSize(messageWriter.ToArraySegment());
    }

    public override void Disconnected()
    {
        if (IsSwitchingServers)
            SessionManager.Singleton.DetachClient(PreAuth.UserId, "switching servers");
        else
            SessionManager.Singleton.DestroyAllForUser(PreAuth.UserId, "Client disconnected from proxy");

        ConnectionByUserId.Remove(PreAuth.UserId);
    }
}
