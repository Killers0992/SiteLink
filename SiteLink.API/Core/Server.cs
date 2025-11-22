using SiteLink.API.Events;
using SiteLink.API.Events.Args;

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

        EventManager.Server.InvokeServerRegistered(new ServerRegisteredEvent(server));
    }

    public static void Unregister(string name)
    {
        if (!RegisteredServers.TryGetValue(name, out Server server))
        {
            return;
        }

        EventManager.Server.InvokeServerUnregistered(new ServerUnregisteredEvent(server));

        server.Destroy();

        RegisteredServers.Remove(server.IpAddress);
        RegisteredServers.Remove(server.Name.ToLower());
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

    private int _index = -1;
    private ServerSettings _customSettings;

    public string Name { get; }

    public ServerSettings Settings
    {
        get
        {
            if (_customSettings != null)
                return _customSettings;

            switch (_index)
            {
                case -1:
                    _index = SiteLinkSettings.Singleton.Servers.FindIndex(x => x.Name.ToLower() == Name.ToLower());

                    // Index not found return null always.
                    if (_index == -1)
                    {
                        _index = -2;
                        return null;
                    }
                    break;
                case -2:
                    return null;
            }

            return SiteLinkSettings.Singleton.Servers[_index];
        }
    }

    public string DisplayName => Settings.DisplayName;
    public string IpAddress => Settings.Address;
    public int Port => Settings.Port;
    public bool ForwardIpAddress => Settings.ForwardIpAddress;

    public bool IsSimulated { get; private set; }

    public string Tag => $"[(f=yellow){Name.ToLower()}(f=white)]";

    public List<Client> Clients { get; } = new List<Client>();
    public int ClientsCount => Clients.Count;
    public int MaxClientsCount => Settings.MaxClients;

    public Server(string name, ServerSettings customSettings = null, bool isSimulated = false)
    {
        Name = name;
        _customSettings = customSettings;
        IsSimulated = isSimulated;

        SiteLinkLogger.Info($"{Tag} Server registered under ip (f=green){IpAddress}:{Port}(f=white)");
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

    public void Destroy()
    {
        foreach (Client client in Clients)
        {
            client.Disconnect("Server removed from config.");
        }

        SiteLinkLogger.Info($"{Tag} Server unregistered.");
    }

    public override string ToString() => $"{IpAddress}:{Port}";
}
