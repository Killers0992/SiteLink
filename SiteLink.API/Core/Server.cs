using PlayerRoles;
using SiteLink.API.Events;
using SiteLink.API.Events.Args;
using SiteLink.API.Networking.Connections;

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
            using var enumerator = RegisteredServers.GetEnumerator();
            if (enumerator.MoveNext())
                return enumerator.Current.Value;
        }

        return null;
    }

    private readonly ConcurrentDictionary<Session, byte> _sessions = new();


    private int _index = -1;
    private ServerSettings _customSettings;

    public string Name { get; }

    public BridgeConnection BridgeConnection { get; set; }

    public int SessionsCount => _sessions.Count;

    public Session[] GetSessionsSnapshot() => _sessions.Keys.ToArray();

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

    public int MaxSessions => Settings.MaxClients;

    public Server(string name, ServerSettings customSettings = null, bool isSimulated = false)
    {
        Name = name;
        _customSettings = customSettings;

        IsSimulated = isSimulated;

        SiteLinkLogger.Info($"{Tag} Server registered under ip (f=green){IpAddress}:{Port}(f=white)");
    }

    public void SendToBridge(ushort messageId,
        Action<NetDataWriter> payload,
        DeliveryMethod method = DeliveryMethod.ReliableOrdered) => SiteLinkBridge.SendTo(this, messageId, payload, method);

    internal bool InternalSessionConnecting(Session session) => OnSessionConnecting(session);

    internal bool InternalSessionConnected(Session session)
    {
        if (_sessions.TryAdd(session, 0))
        {
            OnSessionConnected(session);
            return true;
        }

        return false;
    }

    internal bool InternalSessionDisconnected(Session session)
    {
        if (_sessions.TryRemove(session, out _))
        {
            OnSessionDisconnected(session);
            return true;
        }

        return false;
    }

    internal void InternalSessionAddPlayer(Session session) => OnSessionAddPlayer(session);

    internal void InternalSessionReady(Session session) => OnSessionReady(session);

    public virtual void OnSessionSpawned(Session session) { }

    public virtual bool OnSessionConnecting(Session session) => false;

    public virtual void OnSessionConnected(Session session) { }

    public virtual void OnSessionDisconnected(Session session) { }

    public virtual void OnSessionSSSReponse(Session session, int id) { }

    public virtual void OnSessionReady(Session session) { }

    public virtual void OnSessionAddPlayer(Session session) { }

    public virtual void OnUpdate() { }

    public void Destroy()
    {
        var sessions = GetSessionsSnapshot();

        foreach (var s in sessions)
        {
            try
            {
                s.Disconnect(
                    TranslationManager.Format(TranslationManager.Current.Connection.ServerRemoved)
                        .Add("server", DisplayName)
                        .Add("server_name", Name)
                        .Format());
            }
            catch { }
            try { s.Dispose(); } catch { }
        }

        SiteLinkLogger.Info($"{Tag} Server unregistered.");
    }

    public override string ToString() => $"{IpAddress}:{Port}";
}
