namespace SiteLink.API.Core;

public class Server
{
    public static Dictionary<string, Server> RegisteredServers = new Dictionary<string, Server>();
    public static List<Server> List { get; set; } = new List<Server>();

    public static void Register<TServer>(TServer server) where TServer : Server
    {
        string ipAddress = $"{server.IpAddress}:{server.Port}";

        if (RegisteredServers.ContainsKey(ipAddress))
            return;

        RegisteredServers.Add(ipAddress, server);
        RegisteredServers.Add(server.Name.ToLower(), server);
        List.Add(server);
    }

    public static bool TryGetByName(string name, out Server server) => RegisteredServers.TryGetValue($"{name.ToLower()}", out server);

    public static Server Get<TServer>(string name = null, string ip = null, int port = -1) where TServer : Server
    {
        if (!string.IsNullOrEmpty(ip))
        {
            if (RegisteredServers.TryGetValue($"{ip}:{port}", out Server server))
                return server;
        }
        else if (!string.IsNullOrEmpty(name))
        {
            if (RegisteredServers.TryGetValue($"{name.ToLower()}", out Server server))
                return server;
        }
        else
        {
            var pair = RegisteredServers.FirstOrDefault();

            if (pair.Value != null)
                return pair.Value;
        }

        return null;
    }

    public string Name => Settings.Name;
    public string DisplayName => Settings.DisplayName;
    public string IpAddress => Settings.Address;
    public int Port => Settings.Port;
    public bool ForwardIpAddress => Settings.ForwardIpAddress;

    public bool IsSimulated { get; private set; }

    public string Tag => $"[(f=yellow){Name.ToLower()}(f=white)]";

    public List<Client> Clients { get; } = new List<Client>();
    public int ClientsCount => Clients.Count;
    public int MaxClientsCount => Settings.MaxClients;

    public ServerSettings Settings { get; }

    public Server(ServerSettings settings = null, string name = null, string ip = null, int? port = null, bool? isSimulated = null, bool? forwardIpAddress = null)
    {
        if (settings == null)
        {
            settings = new ServerSettings()
            {
                Name = name ?? "Unknown",
                Address = ip ?? "127.0.0.1",
                Port = port ?? 7777,
                ForwardIpAddress = forwardIpAddress ?? false,
            };

            IsSimulated = isSimulated ?? false;
        }

        Settings = settings;

        SiteLinkLogger.Info($"{Tag} Server registered (f=green){IpAddress}:{Port}(f=white)");
    }

    public bool InternalClientConnecting(Client client)
    {
        bool canJoin = OnClientConnecting(client);

        return canJoin;
    }

    public void InternalClientConnected(Client client)
    {
        Clients.Add(client);
        OnClientConnected(client);
    }


    public void InternalClientDisconnected(Client client)
    {
        Clients.Remove(client);
        OnClientDisconnected(client);
    }

    public virtual bool OnClientConnecting(Client client) => false;
    
    public virtual void OnClientConnected(Client client) { }
    public virtual void OnClientDisconnected(Client client) { }

    public virtual void OnClientReady(Client client) { }
    public virtual void OnClientSpawnPlayer(Client client) { }

    public virtual void OnClientSpawned(Client client) { }

    public virtual void OnClientSSSReponse(Client client, int id) { }

    public virtual void OnUpdate() { }

    public override string ToString() => $"{IpAddress}:{Port}";
}
