using SiteLink.API.Events;
using SiteLink.API.Events.Args;

namespace SiteLink.API.Networking;

public class Listener
{
    public const int PoolingDelayMs = 10;

    public static Dictionary<string, Client> ClientByUserId = new Dictionary<string, Client>();
    //public static Dictionary<string, Client> ClientByName = new Dictionary<string, Client>();

    public static CancellationToken Token;

    public static List<Listener> List => ListenersByName.Values.ToList();
    public static ConcurrentDictionary<string, Listener> ListenersByName { get; } = new ConcurrentDictionary<string, Listener>();

    public static bool TryGet(string name, out Listener listener) => ListenersByName.TryGetValue(name, out listener);

    public static void Register(Listener listener)
    {
        ListenersByName.TryAdd(listener.Name, listener);

        EventManager.Listener.InvokeListenerRegistered(new ListenerRegisteredEvent(listener));
    }

    public static void Unregister(string name)
    {
        if (!ListenersByName.TryGetValue(name, out Listener listener))
            return;

        listener.Destroy();

        EventManager.Listener.InvokeListenerUnregistered(new ListenerUnregisteredEvent(listener));

        ListenersByName.TryRemove(name, out _);
    }

    public static void RegisterClientInLookup(Client client)
    {
        if (ClientByUserId.ContainsKey(client.PreAuth.UserId))
            ClientByUserId[client.PreAuth.UserId] = client;
        else
            ClientByUserId.Add(client.PreAuth.UserId, client);
    }

    public static void UnregisterClientInLookup(Client client)
    {
        if (ClientByUserId.ContainsKey(client.PreAuth.UserId))
            ClientByUserId.Remove(client.PreAuth.UserId);
    }

    private string _version;
    private Version _parsedGameVersion;

    private HttpClient _httpClient;
    private NetManager _manager;
    private EventBasedNetListener _listener;
    private Queue<Client> _clientsToRemove = new Queue<Client>();
    private int _index = -1;

    public string Name { get; }

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

    public List<Client> NotConnectedClients = new List<Client>();
    public Dictionary<int, Client> ConnectedClients = new Dictionary<int, Client>();

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

        if (!_manager.StartInManualMode(IPAddress.Parse(ListenAddress), IPAddress.IPv6Any, ListenPort))
        {
            SiteLinkLogger.Info($"{Tag} Failed to start listener!", "Listener");
            return;
        }

        Task.Run(() => RunEventPolling(Token), Token);

        SiteLinkLogger.Info($"{Tag} Listening for clients on (f=green){ListenAddress}:{ListenPort}(f=white), allow clients with game version (f=green){Settings.GameVersion.ParseVersion()}(f=white)");
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
        while (!token.IsCancellationRequested)
        {
            try
            {
                _manager.PollEvents();
                _manager.ManualUpdate(PoolingDelayMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            foreach (Client client in NotConnectedClients)
            {
                if (client.Connection.IsConnected || client.IsDisposing)
                {
                    //ProxyLogger.Info("Dispose not connected client " + client.PreAuth.UserId);
                    _clientsToRemove.Enqueue(client);
                    continue;
                }

                try
                {
                    client.PollEvents();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            while(_clientsToRemove.Count > 0)
            {
                NotConnectedClients.Remove(_clientsToRemove.Dequeue());
            }

            foreach (Client client in ConnectedClients.Values)
            {
                try
                {
                    client.PollEvents();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            await Task.Delay(PoolingDelayMs, token);
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

        OnClientConnected(new Client(this, request, preAuth));
    }

    void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        if (!ConnectedClients.TryGetValue(peer.Id, out Client client))
            return;

        byte[] bytes = reader.RawData;
        int pos = reader.Position;
        int length = reader.AvailableBytes;

        if (!client.ProcessMirrorDataFromListener(ref bytes, ref pos, ref length))
            return;

        client.Connection.Send(bytes, pos, length, deliveryMethod);
    }

    void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (!ConnectedClients.TryGetValue(peer.Id, out Client client))
            return;

        OnClientDisconneted(client, disconnectInfo.Reason);

        client.World = null;

        switch (disconnectInfo.Reason)
        {
            case DisconnectReason.RemoteConnectionClose:
                SiteLinkLogger.Info($"{client.Tag} Client closed the connection!");
                break;
            default:
                SiteLinkLogger.Info($"{client.Tag} Disconnected");
                break;
        }

        client.Dispose();
    }

    public virtual void OnClientConnected(Client client)
    {
        SiteLinkLogger.Info($"{client.Tag} Connected to listener ( Ip Address (f=cyan){client.PreAuth.IpAddress}(f=white), Game Version (f=cyan){client.PreAuth.ClientVersion.ToString(3)}(f=white) )");

        client.Connect(Priorities);
    }

    public virtual void OnClientDisconneted(Client client, DisconnectReason reason)
    {

    }

    public void Destroy()
    {
        foreach(Client client in ConnectedClients.Values)
        {
            client.Disconnect("Listener shutdown");
        }

        SiteLinkLogger.Info($"{Tag} Listener unregistered.");
    }
}