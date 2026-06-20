using SiteLink.API.Events;
using SiteLink.API.Events.Args;
using SiteLink.API.Metrics;
using SiteLink.API.Networking.Connections;
using SiteLink.API.Threading;
using System.Buffers;

namespace SiteLink.API.Networking;

public class Listener : IDisposable
{
    /// <summary>
    /// The inverse accuracy constant for position calculations.
    /// </summary>
    public const float InverseAccuracy = 0.00390625f;

    public const int PoolingDelayMs = 10;

    public static CancellationToken Token;

    public static List<Listener> List
    {
        get
        {
            int currentVersion = ListenersByName.Count;
            if (_cachedListenerList == null || _listenerListVersion != currentVersion)
            {
                _cachedListenerList = ListenersByName.Values.ToList();
                _listenerListVersion = currentVersion;
            }
            return _cachedListenerList;
        }
    }
    public static ConcurrentDictionary<string, Listener> ListenersByName { get; } = new ConcurrentDictionary<string, Listener>();

    public static bool TryGet(string name, out Listener listener) => ListenersByName.TryGetValue(name, out listener);

    public readonly BatchInterceptor ClientToServer = new(PacketDirection.ClientToServer);
    public readonly ArrayPool<byte> ByteArrayPool = ArrayPool<byte>.Shared;

    private string _version;
    private Version _parsedGameVersion;

    private SiteLinkApiClient _apiClient;
    private NetManager _manager;
    private EventBasedNetListener _listener;
    private int _index = -1;

    private readonly ConcurrentQueue<Connection> _connectionsToRemove = new();
    private readonly Timer _connectionCleanupTimer;

    private int _threadId;
    private readonly ConcurrentQueue<Action> _workQueue = new();

    private static List<Listener> _cachedListenerList;
    private static int _listenerListVersion = 0;

    public string Name { get; }

    public ListenerStats Stats { get; } = new ListenerStats();

    public ListenerSettings Settings
    {
        get
        {
            if (_index == -1)
            {
                _index = SiteLinkSettings.Singleton.Listeners.FindIndex(x => x.Name == Name);
            }

            return SiteLinkSettings.Singleton.Listeners[_index];
        }
    }

    public string ListenAddress => Settings.ListenAddress;

    public int ListenPort => Settings.ListenPort;

    public string PublicAddress { get; private set; }

    public string[] Priorities => Settings.Priorities;

    public Version GameVersion
    {
        get
        {
            if (_version != Settings.GameVersion.ParseVersion())
            {
                _parsedGameVersion = Version.Parse(Settings.GameVersion.ParseVersion());
                _version = Settings.GameVersion.ParseVersion();
            }

            return _parsedGameVersion;
        }
    }

    public List<Connection> PendingConnections { get; } = new List<Connection>();

    public ConcurrentDictionary<int, Connection> Connections { get; } = new ConcurrentDictionary<int, Connection>();

    public string Tag => $"[(f=cyan){Name}(f=white)]";

    /// <summary>
    /// Gets the thread ID that owns this listener.
    /// </summary>
    public int OwnerThreadId => _threadId;

    public bool ForceServerListUpdate;

    public bool ServerListUpdate;

    public int ServerListCycle;

    readonly NetDataWriter RequestWriter = new NetDataWriter();

    public Listener(string name)
    {
        Name = name;

        _listener = new EventBasedNetListener();
        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;

        _listener.NetworkErrorEvent += (endPoint, socketError) =>
        {
            SiteLinkLogger.Error($"{Tag} Network error from {endPoint}: {socketError}");
        };

        _manager = new NetManager(_listener)
        {
            // Server only.
            BroadcastReceiveEnabled = true,

            UpdateTime = NetSettings.UpdateTime,
            ChannelsCount = NetSettings.ChannelsCount,
            DisconnectTimeout = NetSettings.SessionDisconnectTimeout,
            ReconnectDelay = NetSettings.SessionReconnectDelay,
            MaxConnectAttempts = NetSettings.SessionMaxConnectAttempts,
        };

        ListenersByName.TryAdd(Name, this);

        if (!_manager.Start(IPAddress.Parse(ListenAddress), IPAddress.IPv6Any, ListenPort))
        {
            SiteLinkLogger.Info($"{Tag} Failed to start listener!", "Listener");
            return;
        }

        // Initialize periodic connection cleanup timer (every 30 seconds)
        _connectionCleanupTimer = new Timer(CleanupStaleConnections, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Task.Run(() => RunEventPolling(Token), Token);

        SiteLinkLogger.Info($"{Tag} Listening for clients on (f=green){ListenAddress}:{ListenPort}(f=white), allow clients with game version (f=green){Settings.GameVersion.ParseVersion()}(f=white)");

        EventManager.Listener.InvokeListenerRegistered(new ListenerRegisteredEvent(this));

        ClientToServer.Register(NetworkMessages.AttachmentsSetupPreference, OnAddPlayer);
        ClientToServer.Register(NetworkMessages.ReadyMessage, OnReady);
        ClientToServer.Register(NetworkMessages.SSSClientResponse, OnSSSClientResponse);
        ClientToServer.Register(NetworkMessages.FpcFromClientMessage, OnPosition);
        ClientToServer.Register(NetworkMessages.CommandMessage, OnCommand);
    }

    private InterceptResult OnCommand(ushort id, NetworkReader reader, ArraySegment<byte> original, Session session)
    {
        CommandMessage commandMessage = new CommandMessage
        {
            netId = reader.ReadUInt(),
            componentIndex = reader.ReadByte(),
            functionHash = reader.ReadUShort(),
            payload = reader.ReadArraySegmentAndSize()
        };

        if (session.NetworkId == commandMessage.netId)
        {
            switch (commandMessage.functionHash)
            {
                case NetworkMessages.CharacterClassManager.Commands.ConfirmDisconnect:
                    session.Disconnect();
                    break;
            }
        }

        return InterceptResult.Pass();
    }

    static InterceptResult OnAddPlayer(ushort id, NetworkReader r, ArraySegment<byte> original, Session session)
    {
        session.Server?.InternalSessionAddPlayer(session);

        return InterceptResult.Pass();
    }

    static InterceptResult OnReady(ushort id, NetworkReader r, ArraySegment<byte> original, Session session)
    {
        session.IsReady = true;

        session.Server?.InternalSessionReady(session);

        return InterceptResult.Pass();
    }

    static InterceptResult OnSSSClientResponse(ushort id, NetworkReader r, ArraySegment<byte> original, Session session)
    {
        //SSSClientResponse response = new SSSClientResponse(r);

        //session.Server?.OnSessionSSSReponse(session, response.Id);

        return InterceptResult.Pass();
    }

    static InterceptResult OnPosition(ushort id, NetworkReader r, ArraySegment<byte> original, Session session)
    {
        session.IsSpawned = true;

        if (!session.Server?.IsSimulated ?? true)
            return InterceptResult.Pass();

        byte code = r.ReadByte();

        bool _bitMouseLook = false;
        bool _bitPosition = false;
        bool _bitCustom = false;

        ushort _rotH, _rotV;

        code.ByteToBools(out bool b1, out bool b2, out bool b3, out bool b4, out bool b5, out _bitMouseLook, out _bitPosition, out _bitCustom);

        if (_bitPosition)
        {
            byte waypointId = r.ReadByte();

            short PositionX, PositionY, PositionZ;
            if (waypointId > 0)
            {
                PositionX = r.ReadShort();
                PositionY = r.ReadShort();
                PositionZ = r.ReadShort();

                session.WaypointId = waypointId;
                session.RelativePosition = new Vector3(PositionX * InverseAccuracy, PositionY * InverseAccuracy, PositionZ * InverseAccuracy);
            }
            else
            {
                PositionX = 0;
                PositionY = 0;
                PositionZ = 0;
            }
        }

        if (_bitMouseLook)
        {
            _rotH = r.ReadUShort();
            _rotV = r.ReadUShort();
        }
        else
        {
            _rotH = 0;
            _rotV = 0;
        }

        session.HorizontalRotation = Mathf.Lerp(0, 360, _rotH / (float)ushort.MaxValue);
        session.VerticalRotation = Mathf.Lerp(-88f, 88f, _rotV / (float)ushort.MaxValue);

        return InterceptResult.Pass();
    }

    public async Task Initialize()
    {
        if (Settings.ServerList.PublicAddress != "auto")
        {
            PublicAddress = Settings.ServerList.PublicAddress;
            return;
        }

        PublicAddress = await GetPublicIp();
        SiteLinkLogger.Info($"{Tag} Obtained public ip (f=green){PublicAddress}(f=white).");
    }

    async Task<string> GetPublicIp()
    {
        try
        {
            using var client = new SiteLinkApiClient("SCP SL", GameVersion.ToString(3));
            string str = await client.GetPublicIpAddressAsync();

            str = (str.EndsWith(".") ? str.Remove(str.Length - 1) : str);

            return str;
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error(ex, "ListService");
            return null;
        }
    }

    async Task RunEventPolling(CancellationToken token)
    {
        _threadId = Thread.CurrentThread.ManagedThreadId;

        Scheduler.RegisterThread(_threadId, Name, EnqueueWork);

        while (!token.IsCancellationRequested)
        {
            UpdateNetwork();

            UpdateConnections(Connections.Values);
            UpdateConnections(PendingConnections);

            RemoveConnections();

            ProcessWorkQueue();

            await Task.Delay(PoolingDelayMs, token);
        }
    }

    private void EnqueueWork(Action callback)
    {
        _workQueue.Enqueue(callback);
    }

    private void ProcessWorkQueue()
    {
        while (_workQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Error($"Error processing work queue for {Name}: {ex}", Name);
            }
        }
    }

    void UpdateNetwork()
    {
        try
        {
            _manager.PollEvents();
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error("Failed to poll network events:\n" + ex);
        }
    }

    void UpdateConnections(ICollection<Connection> connections)
    {
        foreach (Connection connection in connections)
        {
            if (connection.IsDisposed)
            {
                _connectionsToRemove.Enqueue(connection);
                continue;
            }

            try
            {
                connection.Update();
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Error($"Failed to update connection {connection}:\n{ex}");
            }
        }
    }

    void RemoveConnections()
    {
        while (_connectionsToRemove.TryDequeue(out Connection connection))
        {
            PendingConnections.Remove(connection);
        }
    }

    void CleanupStaleConnections(object state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var connectionsToRemove = new List<Connection>();

            foreach (var kvp in Connections)
            {
                if (kvp.Value.IsDisposed)
                {
                    connectionsToRemove.Add(kvp.Value);
                }
            }

            foreach (var connection in connectionsToRemove)
            {
                Connections.TryRemove(connection.Peer?.Id ?? -1, out _);
            }

            if (connectionsToRemove.Count > 0)
            {
                SiteLinkLogger.Debug($"{Tag} Cleaned up {connectionsToRemove.Count} disposed connections.");
            }
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error($"Error during connection cleanup: {ex}", Name);
        }
    }


    void OnConnectionRequest(ConnectionRequest request)
    {
        string connectionIpAddress = $"{request.RemoteEndPoint.Address}";

        DisconnectType response = DisconnectType.Valid;
        bool rejectForce = false;
        PreAuth preAuth = default;

        if (!PreAuth.TryRead(this, connectionIpAddress, request.Data, ref response, ref rejectForce, ref preAuth))
        {
            switch (response)
            {
                case DisconnectType.InvalidClientType:
                case DisconnectType.ClientTypeOutOfRange:
                    request.RejectWithReason(RequestWriter, RejectionReason.InvalidToken);
                    break;

                case DisconnectType.ForbiddenClientType:
                    request.RejectWithReason(RequestWriter, RejectionReason.VerificationRejected);
                    break;

                case DisconnectType.InvalidMajorVersion:
                case DisconnectType.InvalidMinorVersion:
                case DisconnectType.InvalidRevisionVersion:
                case DisconnectType.InvalidBackwardCompatibility:
                case DisconnectType.InvalidBackwardRevision:
                case DisconnectType.VersionNotCompatible:
                    request.RejectWithReason(RequestWriter, RejectionReason.VersionMismatch);
                    break;

                case DisconnectType.InvalidChallengeId:
                    request.RejectWithReason(RequestWriter, RejectionReason.InvalidChallenge);
                    break;

                case DisconnectType.InvalidChallengeResponse:
                    request.RejectWithReason(RequestWriter, RejectionReason.InvalidChallengeKey);
                    break;

                case DisconnectType.PreAuthExpired:
                    request.RejectWithReason(RequestWriter, RejectionReason.ExpiredAuth);
                    break;

                default:
                    request.RejectWithReason(RequestWriter, RejectionReason.Error);
                    break;
            }
            return;
        }

        switch (preAuth.ClientType)
        {
            case ClientType.Bridge:
                BridgeConnection bridgeConnection = new BridgeConnection(this, request, preAuth);

                LiteNetPeer peer = bridgeConnection.AcceptRequest();

                bridgeConnection.TargetServer = preAuth.TargetServer;
                SiteLinkBridge.AttachServerPeer(preAuth.TargetServer, peer);

                SiteLinkLogger.Info($"{bridgeConnection.Tag} {preAuth.TargetServer.Tag} Bridge connected!");
                break;

            case ClientType.GameClient:

                if (RemoteConnection.ConnectionByUserId.ContainsKey(preAuth.UserId))
                {
                    SiteLinkLogger.Info($"{Tag} Rejected connection from (f=cyan){preAuth.UserId}(f=white) - already connected.");

                    request.RejectWithReason(RequestWriter, RejectionReason.Error);
                    return;
                }

                ClientConnectingToListenerEvent ev = new ClientConnectingToListenerEvent(this, request, preAuth);
                EventManager.Client.InvokeConnectingToListener(ev);

                if (ev.IsCancelled)
                    return;

                RemoteConnection connection = new RemoteConnection(this, request, preAuth);

                Stats.RecordConnectionAccepted();

                SiteLinkLogger.Info($"{connection.Tag} Connected to listener ( Ip Address (f=cyan){connection.PreAuth.IpAddress}(f=white), Game Version (f=cyan){connection.PreAuth.ClientVersion.ToString(3)}(f=white) )");

                if (SessionManager.Singleton.TryReattachConnection(connection))
                    return;

                connection.Connect(Priorities, true);
                break;
        }
    }

    void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        if (!Connections.TryGetValue(peer.Id, out Connection connection))
            return;

        int length = reader.AvailableBytes;

        connection.Stats.RecordBytesReceived(length);

        Stats?.RecordBytesReceived(length);
        Stats?.RecordPacketsReceived(1);

        connection.ReceiveDataFromListener(length, reader, channelNumber, deliveryMethod);
    }

    void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (!Connections.TryRemove(peer.Id, out Connection connection))
            return;

        Stats.RecordConnectionError();

        string tag = connection.Tag;

        connection.Dispose();

        switch (disconnectInfo.Reason)
        {
            case DisconnectReason.RemoteConnectionClose:
                SiteLinkLogger.Info($"{tag} Client closed the connection!");
                break;
            default:
                SiteLinkLogger.Info($"{tag} Disconnected");
                break;
        }

    }

    public void Dispose()
    {
        EventManager.Listener.InvokeListenerUnregistered(new ListenerUnregisteredEvent(this));

        if (_listener != null)
        {
            _listener.ConnectionRequestEvent -= OnConnectionRequest;
            _listener.NetworkReceiveEvent -= OnNetworkReceive;
            _listener.PeerDisconnectedEvent -= OnPeerDisconnected;
        }

        _connectionCleanupTimer?.Dispose();

        _apiClient?.Dispose();

        _manager?.Stop();
        _manager = null;

        _listener = null;

        ListenersByName.TryRemove(Name, out _);

        _cachedListenerList = null;
        _listenerListVersion = 0;
    }
}