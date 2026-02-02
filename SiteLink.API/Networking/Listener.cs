using Mirror;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using SiteLink.API.Events;
using SiteLink.API.Events.Args;
using SiteLink.API.Threading;
using SiteLink.API.Metrics;
using System.Buffers;
using UserSettings.ServerSpecific;

namespace SiteLink.API.Networking;

public class Listener : IDisposable
{        
    /// <summary>
    /// The inverse accuracy constant for position calculations.
    /// </summary>
    public const float InverseAccuracy = 0.00390625f;

    public const int PoolingDelayMs = 10;

    public static CancellationToken Token;

    public static List<Listener> List => ListenersByName.Values.ToList();
    public static ConcurrentDictionary<string, Listener> ListenersByName { get; } = new ConcurrentDictionary<string, Listener>();

    public static bool TryGet(string name, out Listener listener) => ListenersByName.TryGetValue(name, out listener);

    private readonly BatchInterceptor _clientToServer = new(PacketDirection.ClientToServer);

    private string _version;
    private Version _parsedGameVersion;

    private HttpClient _httpClient;
    private NetManager _manager;
    private EventBasedNetListener _listener;
    private int _index = -1;

    private Queue<Connection> _connectionsToRemove = new Queue<Connection>();

    private int _threadId;
    private readonly ConcurrentQueue<Action> _workQueue = new();

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

    public HttpClient Http
    {
        get
        {
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "SCP SL");
                _httpClient.DefaultRequestHeaders.Add("Game-Version", GameVersion.ToString(3));
            }

            return _httpClient;
        }
    }


    public bool ForceServerListUpdate;

    public bool ServerListUpdate;

    public int ServerListCycle;

    public Listener(string name)
    {
        Name = name;

        _listener = new EventBasedNetListener();
        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;

        _manager = new NetManager(_listener)
        {
            UpdateTime = 5,
            BroadcastReceiveEnabled = true,
            ChannelsCount = (byte)6,
            DisconnectTimeout = 6000,
            ReconnectDelay = 400,
            MaxConnectAttempts = 2,
        };

        ListenersByName.TryAdd(Name, this);

        if (!_manager.StartInManualMode(IPAddress.Parse(ListenAddress), IPAddress.IPv6Any, ListenPort))
        {
            SiteLinkLogger.Info($"{Tag} Failed to start listener!", "Listener");
            return;
        }

        Task.Run(() => RunEventPolling(Token), Token);

        SiteLinkLogger.Info($"{Tag} Listening for clients on (f=green){ListenAddress}:{ListenPort}(f=white), allow clients with game version (f=green){Settings.GameVersion.ParseVersion()}(f=white)");

        EventManager.Listener.InvokeListenerRegistered(new ListenerRegisteredEvent(this));

        _clientToServer.Register(NetworkMessages.ReadyMessage, static (id, r, original, session) =>
        {
            session.IsReady = true;

            SiteLinkLogger.Info(session.Connection.Tag + $" Received ready message.");

            return InterceptResult.Pass();
        });

        _clientToServer.Register(NetworkMessages.SSSClientResponse, static (id, r, original, session) =>
        {
            SSSClientResponse response = new SSSClientResponse(r);

            session.Server?.OnSessionSSSReponse(session, response.Id);

            return InterceptResult.Pass();
        });

        _clientToServer.Register(NetworkMessages.FpcFromClientMessage, static (id, r, original, session) =>
        {
            if (session.Server?.IsSimulated ?? false)
                return InterceptResult.Pass();

            byte code = r.ReadByte();

            bool _bitMouseLook = false;
            bool _bitPosition = false;
            bool _bitCustom = false;

            ushort _rotH, _rotV;

            global::Misc.ByteToBools(code, out bool b1, out bool b2, out bool b3, out bool b4, out bool b5, out _bitMouseLook, out _bitPosition, out _bitCustom);

            PlayerMovementState _state = (PlayerMovementState)global::Misc.BoolsToByte(b1, b2, b3, b4, b5);

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
        });
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
            using (var response = await Http.GetAsync("https://api.scpslgame.com/ip.php"))
            {
                string str = await response.Content.ReadAsStringAsync();

                str = (str.EndsWith(".") ? str.Remove(str.Length - 1) : str);

                return str;
            }
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
            _manager.ManualUpdate(PoolingDelayMs);
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
        while (_connectionsToRemove.Count > 0)
        {
            PendingConnections.Remove(_connectionsToRemove.Dequeue());
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
                // Client connecting to this listener does have wrong game version, reject with VersionMismatch.
                case DisconnectType.VersionNotCompatible:
                    NetDataWriter writer = new NetDataWriter();
                    writer.Put((byte)RejectionReason.VersionMismatch);
                    request.RejectForce(writer);
                    break;

                default:
                    request.RejectForce();
                    break;
            }
            return;
        }

        ClientConnectingToListenerEvent ev = new ClientConnectingToListenerEvent(this, request, preAuth);
        EventManager.Client.InvokeConnectingToListener(ev);

        if (ev.IsCancelled)
            return;

        Connection connection = new Connection(this, request, preAuth);

        Stats.RecordConnectionAccepted();

        SiteLinkLogger.Info($"{connection.Tag} Connected to listener ( Ip Address (f=cyan){connection.PreAuth.IpAddress}(f=white), Game Version (f=cyan){connection.PreAuth.ClientVersion.ToString(3)}(f=white) )");

        if (SessionManager.Singleton.TryReattachConnection(connection))
            return;

        connection.Connect(Priorities);
    }

    void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        if (!Connections.TryGetValue(peer.Id, out Connection connection))
            return;

        int length = reader.AvailableBytes;
        connection.Stats.RecordBytesReceived(length);
        Stats?.RecordBytesReceived(length);
        Stats?.RecordPacketsReceived(1);

        if (connection.Session == null)
            return;

        byte[] bytes = reader.RawData;
        int position = reader.Position;

        if (!_clientToServer.TryRewrite(connection.Session, bytes, position, length, out var outBytes, out var outPos, out var outLen, out bool pooled))
        {
            connection.Session.SendToServer(bytes, position, length, deliveryMethod);
            return;
        }

        connection.Session.SendToServer(outBytes, outPos, outLen, deliveryMethod);

        if (!ReferenceEquals(outBytes, bytes))
            ArrayPool<byte>.Shared.Return(outBytes);
    }

    void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (!Connections.TryRemove(peer.Id, out Connection connection))
            return;

        Stats.RecordConnectionError();

        switch (disconnectInfo.Reason)
        {
            case DisconnectReason.RemoteConnectionClose:
                SiteLinkLogger.Info($"{connection.Tag} Client closed the connection!");
                break;
            default:
                SiteLinkLogger.Info($"{connection.Tag} Disconnected");
                break;
        }

        connection.Dispose();
    }

    public void Dispose()
    {
        EventManager.Listener.InvokeListenerUnregistered(new ListenerUnregisteredEvent(this));

        _manager.Stop();
        _manager = null;

        _listener = null;

        ListenersByName.TryRemove(Name, out _);

        Console.WriteLine("Connections after dispose: " + Connections.Count);
    }
}