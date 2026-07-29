using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Concurrent;
using UnityEngine;

namespace SiteLink.API;

public delegate void SiteLinkMessageHandler(
    NetPacketReader reader
#if NET10_0
    , Server server
#endif
);

/// <summary>
/// Round state as reported by the game server itself.
/// <para>
/// The proxy previously had to infer this from the <c>RoundRestartMessage</c> of whichever
/// session happened to be attached, which only works while somebody is connected. An empty
/// server restarting was invisible.
/// </para>
/// </summary>
public enum BridgeRoundState : byte
{
    Unknown = 0,
    WaitingForPlayers = 1,
    InProgress = 2,
    Ended = 3,
    Restarting = 4,
    Shutdown = 5,
}

/// <summary>
/// How the game server is restarting. Mirrors <c>RoundRestarting.RoundRestartType</c> plus an
/// explicit "not restarting" value, which the game's own enum does not have.
/// </summary>
public enum BridgeRestartType : byte
{
    None = 0,
    Full = 1,

    /// <summary>Fast restart, which is what <c>sr</c> / fast restart produce.</summary>
    Fast = 2,

    Redirect = 3,
}

/// <summary>
/// A SiteLink proxy the game server talks to. More than one is normal: a game server sitting
/// behind two proxies has to report to both of them.
/// </summary>
public sealed class BridgeEndpoint
{
    public BridgeEndpoint(string ip, int port, string secretKey)
    {
        Ip = ip;
        Port = port;
        SecretKey = secretKey ?? string.Empty;
    }

    public string Ip { get; }

    public int Port { get; }

    public string SecretKey { get; }

    public override string ToString() => $"{Ip}:{Port}";
}

public static class SiteLinkBridge
{

#if NET48
    private class BridgeRunner : MonoBehaviour
    {
        public void Update() => SiteLinkBridge.Update();
    }
#endif

    private static readonly ConcurrentDictionary<ushort, List<SiteLinkMessageHandler>> _handlers = new();

    public const ushort MsgTargetServersList = 17150;

    /// <summary>
    /// Sent by the game server to report how many real players it is currently hosting.
    /// Payload: int playerCount, int maxPlayers.
    /// </summary>
    public const ushort MsgPlayerCount = 17151;

    /// <summary>
    /// Sent by the game server whenever its round state changes.
    /// Payload: byte <see cref="BridgeRoundState"/>, byte <see cref="BridgeRestartType"/>, bool idle.
    /// </summary>
    public const ushort MsgRoundState = 17152;

#if NET48

    private sealed class ProxyState
    {
        public ProxyState(BridgeEndpoint endpoint)
        {
            Endpoint = endpoint;
        }

        public readonly BridgeEndpoint Endpoint;

        public NetPeer Peer;
        public bool Connecting;
        public DateTime NextRetry = DateTime.MinValue;

        /// <summary>Target servers advertised by this proxy.</summary>
        public List<string> TargetServers = new List<string>();

        public bool IsConnected => Peer != null && Peer.ConnectionState == ConnectionState.Connected;
    }

    private static NetManager _manager;
    private static EventBasedNetListener _listener;

    private static readonly List<ProxyState> _proxies = new List<ProxyState>();

    /// <summary>Every proxy this game server is configured to talk to.</summary>
    public static List<BridgeEndpoint> Endpoints
    {
        get
        {
            List<BridgeEndpoint> result = new List<BridgeEndpoint>();

            lock (_proxies)
            {
                foreach (ProxyState state in _proxies)
                    result.Add(state.Endpoint);
            }

            return result;
        }
    }

    /// <summary>How many proxies are currently connected.</summary>
    public static int ConnectedCount
    {
        get
        {
            int count = 0;

            lock (_proxies)
            {
                foreach (ProxyState state in _proxies)
                {
                    if (state.IsConnected)
                        count++;
                }
            }

            return count;
        }
    }

    /// <summary>True when at least one proxy is connected.</summary>
    public static bool IsConnected => ConnectedCount > 0;

    /// <summary>
    /// Target servers advertised by the proxies, de-duplicated. Several proxies in front of
    /// the same network normally advertise the same list.
    /// </summary>
    public static List<string> TargetServers
    {
        get
        {
            List<string> result = new List<string>();

            lock (_proxies)
            {
                foreach (ProxyState state in _proxies)
                {
                    foreach (string server in state.TargetServers)
                    {
                        if (!result.Contains(server))
                            result.Add(server);
                    }
                }
            }

            return result;
        }
    }

    /// <summary>Target servers advertised by one specific proxy.</summary>
    public static List<string> GetTargetServers(BridgeEndpoint endpoint)
    {
        lock (_proxies)
        {
            foreach (ProxyState state in _proxies)
            {
                if (state.Endpoint == endpoint)
                    return new List<string>(state.TargetServers);
            }
        }

        return new List<string>();
    }

    /// <summary>Whether one specific proxy is currently connected.</summary>
    public static bool IsConnectedTo(BridgeEndpoint endpoint)
    {
        lock (_proxies)
        {
            foreach (ProxyState state in _proxies)
            {
                if (state.Endpoint == endpoint)
                    return state.IsConnected;
            }
        }

        return false;
    }

#endif

#if NET10_0

    public delegate void BridgeConnectedHandler(Server server);
    public delegate void BridgeDisconnectedHandler(Server server, DisconnectInfo info);

    private static readonly List<BridgeConnectedHandler> _connectedHandlers = new();
    private static readonly List<BridgeDisconnectedHandler> _disconnectedHandlers = new();

    private static readonly ConcurrentDictionary<Server, LiteNetPeer> _serverPeers = new();

#else

    public delegate void BridgeConnectedHandler();
    public delegate void BridgeDisconnectedHandler(DisconnectInfo info);

    /// <summary>Raised for the specific proxy that connected.</summary>
    public delegate void BridgeEndpointConnectedHandler(BridgeEndpoint endpoint);

    /// <summary>Raised for the specific proxy that dropped.</summary>
    public delegate void BridgeEndpointDisconnectedHandler(BridgeEndpoint endpoint, DisconnectInfo info);

    private static readonly List<BridgeConnectedHandler> _connectedHandlers = new();
    private static readonly List<BridgeDisconnectedHandler> _disconnectedHandlers = new();
    private static readonly List<BridgeEndpointConnectedHandler> _endpointConnectedHandlers = new();
    private static readonly List<BridgeEndpointDisconnectedHandler> _endpointDisconnectedHandlers = new();
#endif

#if NET10_0
    public static void RegisterConnectedHandler(BridgeConnectedHandler handler)
    {
        lock (_connectedHandlers) _connectedHandlers.Add(handler);
    }

    public static void UnregisterConnectedHandler(BridgeConnectedHandler handler)
    {
        lock (_connectedHandlers) _connectedHandlers.Remove(handler);
    }

    public static void RegisterDisconnectedHandler(BridgeDisconnectedHandler handler)
    {
        lock (_disconnectedHandlers) _disconnectedHandlers.Add(handler);
    }

    public static void UnregisterDisconnectedHandler(BridgeDisconnectedHandler handler)
    {
        lock (_disconnectedHandlers) _disconnectedHandlers.Remove(handler);
    }
#else
    public static void RegisterConnectedHandler(BridgeConnectedHandler handler)
    {
        lock (_connectedHandlers) _connectedHandlers.Add(handler);
    }

    public static void UnregisterConnectedHandler(BridgeConnectedHandler handler)
    {
        lock (_connectedHandlers) _connectedHandlers.Remove(handler);
    }

    public static void RegisterDisconnectedHandler(BridgeDisconnectedHandler handler)
    {
        lock (_disconnectedHandlers) _disconnectedHandlers.Add(handler);
    }

    public static void UnregisterDisconnectedHandler(BridgeDisconnectedHandler handler)
    {
        lock (_disconnectedHandlers) _disconnectedHandlers.Remove(handler);
    }

    public static void RegisterConnectedHandler(BridgeEndpointConnectedHandler handler)
    {
        lock (_endpointConnectedHandlers) _endpointConnectedHandlers.Add(handler);
    }

    public static void UnregisterConnectedHandler(BridgeEndpointConnectedHandler handler)
    {
        lock (_endpointConnectedHandlers) _endpointConnectedHandlers.Remove(handler);
    }

    public static void RegisterDisconnectedHandler(BridgeEndpointDisconnectedHandler handler)
    {
        lock (_endpointDisconnectedHandlers) _endpointDisconnectedHandlers.Add(handler);
    }

    public static void UnregisterDisconnectedHandler(BridgeEndpointDisconnectedHandler handler)
    {
        lock (_endpointDisconnectedHandlers) _endpointDisconnectedHandlers.Remove(handler);
    }
#endif

#if NET48
    /// <summary>
    /// Connects this game server to a single proxy. Kept for plugins that only ever talk to
    /// one; the overload taking a list is the general form.
    /// </summary>
    public static void Initialize(string ip, int port, string secret)
        => Initialize(new[] { new BridgeEndpoint(ip, port, secret) });

    /// <summary>
    /// Connects this game server to every given proxy. Calling this again later adds the
    /// endpoints that are not registered yet instead of throwing them away.
    /// </summary>
    public static void Initialize(System.Collections.Generic.IEnumerable<BridgeEndpoint> endpoints)
    {
        if (endpoints == null)
            throw new ArgumentNullException(nameof(endpoints));

        if (_manager == null)
        {
            _listener = new EventBasedNetListener();

            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;

            _manager = new NetManager(_listener);
            _manager.Start();

            // The ticker has to survive the scene reload a round restart performs. Hanging it
            // off an existing singleton meant polling silently stopped after the first round.
            GameObject runner = new GameObject("SiteLinkBridge");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<BridgeRunner>();
        }

        lock (_proxies)
        {
            foreach (BridgeEndpoint endpoint in endpoints)
            {
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Ip))
                    continue;

                bool exists = false;

                foreach (ProxyState state in _proxies)
                {
                    if (state.Endpoint.Ip == endpoint.Ip && state.Endpoint.Port == endpoint.Port)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    _proxies.Add(new ProxyState(endpoint));
            }
        }
    }

    private static ProxyState FindByPeer(NetPeer peer)
    {
        if (peer == null)
            return null;

        lock (_proxies)
        {
            foreach (ProxyState state in _proxies)
            {
                if (ReferenceEquals(state.Peer, peer))
                    return state;
            }
        }

        return null;
    }

    private static void OnPeerConnected(NetPeer peer)
    {
        ProxyState state = FindByPeer(peer);

        if (state == null)
            return;

        state.Connecting = false;

        BridgeConnectedHandler[] copy;
        lock (_connectedHandlers) copy = _connectedHandlers.ToArray();
        foreach (var h in copy)
        {
            try { h(); } catch { }
        }

        BridgeEndpointConnectedHandler[] endpointCopy;
        lock (_endpointConnectedHandlers) endpointCopy = _endpointConnectedHandlers.ToArray();
        foreach (var h in endpointCopy)
        {
            try { h(state.Endpoint); } catch { }
        }
    }

    private static void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        ProxyState state = FindByPeer(peer);

        if (state == null)
            return;

        state.Connecting = false;
        state.Peer = null;
        state.NextRetry = DateTime.Now.AddSeconds(5);
        state.TargetServers = new List<string>();

        BridgeDisconnectedHandler[] copy;
        lock (_disconnectedHandlers) copy = _disconnectedHandlers.ToArray();
        foreach (var h in copy)
        {
            try { h(info); } catch { }
        }

        BridgeEndpointDisconnectedHandler[] endpointCopy;
        lock (_endpointDisconnectedHandlers) endpointCopy = _endpointDisconnectedHandlers.ToArray();
        foreach (var h in endpointCopy)
        {
            try { h(state.Endpoint, info); } catch { }
        }
    }

    private static void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
    {
        if (reader.AvailableBytes < 2)
            return;

        ushort messageId = reader.GetUShort();

        // 17150 carries per-proxy state, so it is read here rather than through the shared
        // handler table, which has no idea which proxy a message came from.
        if (messageId == MsgTargetServersList)
        {
            ReadTargetServersList(FindByPeer(peer), reader);
            return;
        }

        Dispatch(messageId, reader);
    }

    private static void ReadTargetServersList(ProxyState state, NetPacketReader reader)
    {
        int count = reader.GetInt();
        var list = new List<string>();

        for (int i = 0; i < count; i++)
        {
            string name = reader.GetString();
            string ip = reader.GetString();
            int port = reader.GetInt();
            list.Add($"{name} ({ip}:{port})");
        }

        if (state != null)
            state.TargetServers = list;
    }
#endif

#if NET10_0
    static SiteLinkBridge()
    {
        // Player count reporting is built into the API so that every proxy gets accurate
        // numbers without requiring an extra plugin. CSG 5.6 is not optional.
        RegisterHandler(MsgPlayerCount, OnBridgePlayerCount);
        RegisterHandler(MsgRoundState, OnBridgeRoundState);
    }

    private static void OnBridgePlayerCount(NetPacketReader reader, Server server)
    {
        if (server == null)
            return;

        if (reader.AvailableBytes < sizeof(int) * 2)
        {
            SiteLinkLogger.Warn($"{server.Tag} Bridge sent a malformed player count packet.");
            return;
        }

        int players = reader.GetInt();
        int maxPlayers = reader.GetInt();

        if (players < 0 || maxPlayers < 0)
        {
            SiteLinkLogger.Warn($"{server.Tag} Bridge reported a negative player count ({players}/{maxPlayers}), ignoring.");
            return;
        }

        server.SetBridgePlayerCount(players, maxPlayers);
    }

    private static void OnBridgeRoundState(NetPacketReader reader, Server server)
    {
        if (server == null)
            return;

        if (reader.AvailableBytes < 3)
        {
            SiteLinkLogger.Warn($"{server.Tag} Bridge sent a malformed round state packet.");
            return;
        }

        BridgeRoundState state = (BridgeRoundState)reader.GetByte();
        BridgeRestartType restartType = (BridgeRestartType)reader.GetByte();
        bool idle = reader.GetBool();

        // Older bridges stop here. Treat their silence as "accepting", which is what the
        // proxy assumed before the field existed, so a stale plugin degrades to the old
        // timer-driven behaviour instead of never reconnecting anyone.
        bool acceptingConnections = true;
        int connectionDelaySeconds = 0;

        if (reader.AvailableBytes >= 2)
        {
            acceptingConnections = reader.GetBool();
            connectionDelaySeconds = reader.GetByte();
        }

        server.SetBridgeRoundState(state, restartType, idle, acceptingConnections, connectionDelaySeconds);
    }

    public static void AttachServerPeer(Server server, LiteNetPeer peer)
    {
        _serverPeers[server] = peer;

        SendTargetServersList(server);

        // Fire connected event
        BridgeConnectedHandler[] copy;
        lock (_connectedHandlers) copy = _connectedHandlers.ToArray();
        foreach (var h in copy)
        {
            try { h(server); } catch { }
        }
    }

    /// <summary>
    /// Returns the servers that should be exposed to the game server, in
    /// <c>servers_in_selector</c> order. Falls back to every registered server when the
    /// selector list is empty or unset.
    /// </summary>
    internal static List<Server> GetSelectorServers()
    {
        string[] selector = SiteLinkSettings.Singleton?.ServersInSelector;

        if (selector == null || selector.Length == 0)
            return Server.List;

        List<Server> result = new List<Server>(selector.Length);

        foreach (string name in selector)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            Server match = Server.Get<Server>(name: name.Trim());

            if (match != null && !result.Contains(match))
                result.Add(match);
        }

        return result;
    }

    public static void SendTargetServersList(Server server)
    {
        SendTo(server, MsgTargetServersList, writer =>
        {
            var servers = GetSelectorServers();
            writer.Put(servers.Count);
            foreach (var s in servers)
            {
                writer.Put(s.Name ?? string.Empty);
                writer.Put(s.IpAddress ?? string.Empty);
                writer.Put(s.Port);
            }
        });
    }

    public static bool DetachServerPeer(Server server, DisconnectInfo info)
    {
        if (server == null)
            return false;

        var removed = _serverPeers.TryRemove(server, out _);

        server.ResetBridgePlayerCount();
        server.ResetBridgeRoundState();

        // Fire disconnected event
        BridgeDisconnectedHandler[] copy;
        lock (_disconnectedHandlers) copy = _disconnectedHandlers.ToArray();
        foreach (var h in copy)
        {
            try { h(server, info); } catch { }
        }

        return removed;
    }

    public static bool TryGetPeer(Server server, out LiteNetPeer peer)
        => _serverPeers.TryGetValue(server, out peer);
#endif

#if NET48
    private static bool _gshRegistered = false;
    private static void RegisterGshCommandSafe()
    {
        if (_gshRegistered) return;
        if (RemoteAdmin.QueryProcessor.DotCommandHandler == null) return;

        try
        {
            RemoteAdmin.QueryProcessor.DotCommandHandler.RegisterCommand(new GshCommand());
            _gshRegistered = true;
            ServerConsole.AddLog("SiteLink: Registered .gsh console command.");
        }
        catch (Exception ex)
        {
            ServerConsole.AddLog("SiteLink: Failed to register .gsh command: " + ex);
        }
    }

    public static void Update()
    {
        RegisterGshCommandSafe();

        if (_manager == null)
            return;

        if (_manager.IsRunning)
            _manager.PollEvents();

        DateTime now = DateTime.Now;

        lock (_proxies)
        {
            foreach (ProxyState state in _proxies)
            {
                if (state.Connecting || state.IsConnected || state.NextRetry > now)
                    continue;

                var writer = new NetDataWriter();

                // client type bridge = 2
                writer.Put((byte)2);
                writer.Put(state.Endpoint.SecretKey);

                state.Peer = _manager.Connect(state.Endpoint.Ip, state.Endpoint.Port, writer);
                state.Connecting = state.Peer != null;

                // A Connect that never produced a peer (unresolvable host, for instance) must
                // not turn into a busy loop hammering DNS every frame.
                if (state.Peer == null)
                    state.NextRetry = now.AddSeconds(5);
            }
        }
    }
#endif

#if NET48
    /// <summary>
    /// Sends a message to every connected proxy and returns how many of them received it.
    /// </summary>
    public static int Send(
        ushort messageId,
        Action<NetDataWriter> payload,
        DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        NetDataWriter writer = null;
        int sent = 0;

        lock (_proxies)
        {
            foreach (ProxyState state in _proxies)
            {
                if (!state.IsConnected)
                    continue;

                if (writer == null)
                {
                    writer = new NetDataWriter();
                    writer.Put(messageId);
                    payload?.Invoke(writer);
                }

                state.Peer.Send(writer, method);
                sent++;
            }
        }

        return sent;
    }

    /// <summary>Sends a message to one specific proxy.</summary>
    public static bool SendTo(
        BridgeEndpoint endpoint,
        ushort messageId,
        Action<NetDataWriter> payload,
        DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        lock (_proxies)
        {
            foreach (ProxyState state in _proxies)
            {
                if (state.Endpoint != endpoint || !state.IsConnected)
                    continue;

                var writer = new NetDataWriter();
                writer.Put(messageId);
                payload?.Invoke(writer);

                state.Peer.Send(writer, method);
                return true;
            }
        }

        return false;
    }
#endif

#if NET10_0
    public static bool SendTo(
        Server server,
        ushort messageId,
        Action<NetDataWriter> payload,
        DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        if (!_serverPeers.TryGetValue(server, out var peer))
            return false;

        if (peer == null || peer.ConnectionState != ConnectionState.Connected)
            return false;

        var writer = new NetDataWriter();
        writer.Put(messageId);
        payload?.Invoke(writer);

        peer.Send(writer, method);
        return true;
    }
#endif

    public static void RegisterHandler(ushort messageId, SiteLinkMessageHandler handler)
    {
        var list = _handlers.GetOrAdd(messageId, _ => new List<SiteLinkMessageHandler>());
        lock (list)
            list.Add(handler);
    }

    public static void UnregisterHandler(ushort messageId, SiteLinkMessageHandler handler)
    {
        if (_handlers.TryGetValue(messageId, out var list))
        {
            lock (list)
                list.Remove(handler);
        }
    }

    public static void Dispatch(
        ushort messageId,
        NetPacketReader reader
#if NET10_0
        , Server server
#endif
    )
    {
        if (!_handlers.TryGetValue(messageId, out var list))
            return;

        SiteLinkMessageHandler[] copy;
        lock (list)
            copy = list.ToArray();

        foreach (var handler in copy)
        {
            try
            {
                handler(
                    reader
#if NET10_0
                    , server
#endif
                );
            }
            catch (Exception ex)
            {
#if NET48
                ServerConsole.AddLog(ex.ToString());
#else
                SiteLinkLogger.Error(ex);
#endif
            }
        }
    }
}

#if NET48
public class GshCommand : CommandSystem.ICommand
{
    public string Command => "gsh";
    public string[] Aliases => new[] { "ghs" };
    public string Description => "Displays the target servers of the Hub server.";

    public bool Execute(System.ArraySegment<string> arguments, CommandSystem.ICommandSender sender, out string response)
    {
        var servers = SiteLinkBridge.TargetServers;
        if (servers == null || servers.Count == 0)
        {
            response = "No target servers registered on the Hub server.";
            return false;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Hub Server targets:");
        foreach (var server in servers)
        {
            sb.AppendLine($"- {server}");
        }
        response = sb.ToString();
        return true;
    }
}
#endif
