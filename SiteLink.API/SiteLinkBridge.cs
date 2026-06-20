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

public static class SiteLinkBridge
{

#if NET48
    private class BridgeRunner : MonoBehaviour
    {
        public void Update() => SiteLinkBridge.Update();
    }
#endif

    private static readonly ConcurrentDictionary<ushort, List<SiteLinkMessageHandler>> _handlers = new();

#if NET10_0

    public delegate void BridgeConnectedHandler(Server server);
    public delegate void BridgeDisconnectedHandler(Server server, DisconnectInfo info);

    private static readonly List<BridgeConnectedHandler> _connectedHandlers = new();
    private static readonly List<BridgeDisconnectedHandler> _disconnectedHandlers = new();

    private static readonly ConcurrentDictionary<Server, LiteNetPeer> _serverPeers = new();

#else

    public delegate void BridgeConnectedHandler();
    public delegate void BridgeDisconnectedHandler(DisconnectInfo info);

    private static readonly List<BridgeConnectedHandler> _connectedHandlers = new();
    private static readonly List<BridgeDisconnectedHandler> _disconnectedHandlers = new();

    private static NetManager _manager;
    private static EventBasedNetListener _listener;

    private static NetPeer _peer => _manager?.FirstPeer;

    private static bool _isConnecting;
    private static DateTime _nextRetry;

    private static string _ip;
    private static int _port;
    private static string _secret;

    public static bool IsConnected => _peer != null && _peer.ConnectionState == ConnectionState.Connected;
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
#endif

#if NET48
    public static void Initialize(string ip, int port, string secret)
    {
        if (_manager != null)
            return;

        _ip = ip;
        _port = port;
        _secret = secret;

        _listener = new EventBasedNetListener();

        _listener.PeerConnectedEvent += peer =>
        {
            _isConnecting = false;

            BridgeConnectedHandler[] copy;
            lock (_connectedHandlers) copy = _connectedHandlers.ToArray();
            foreach (var h in copy)
            {
                try { h(); } catch { }
            }
        };

        _listener.PeerDisconnectedEvent += (peer, info) =>
        {
            _isConnecting = false;
            _nextRetry = DateTime.Now.AddSeconds(5);

            BridgeDisconnectedHandler[] copy;
            lock (_disconnectedHandlers) copy = _disconnectedHandlers.ToArray();
            foreach (var h in copy)
            {
                try { h(info); } catch { }
            }
        };

        _listener.NetworkReceiveEvent += (peer, reader, channel, delivery) =>
        {
            if (reader.AvailableBytes < 2)
                return;

            ushort messageId = reader.GetUShort();
            Dispatch(messageId, reader);
        };

        _manager = new NetManager(_listener);
        _manager.Start();

        if (GameCore.Console.Singleton.gameObject.GetComponent<BridgeRunner>() == null)
            GameCore.Console.Singleton.gameObject.AddComponent<BridgeRunner>();
    }
#endif

#if NET10_0
    public static void AttachServerPeer(Server server, LiteNetPeer peer)
    {
        _serverPeers[server] = peer;

        // Fire connected event
        BridgeConnectedHandler[] copy;
        lock (_connectedHandlers) copy = _connectedHandlers.ToArray();
        foreach (var h in copy)
        {
            try { h(server); } catch { }
        }
    }

    public static bool DetachServerPeer(Server server, DisconnectInfo info)
    {
        var removed = _serverPeers.TryRemove(server, out _);

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
    public static void Update()
    {
        if (_manager == null)
            return;

        if (_manager.IsRunning)
            _manager.PollEvents();

        if (!IsConnected && !_isConnecting && _nextRetry < DateTime.Now)
        {
            var writer = new NetDataWriter();

            // client type bridge = 2
            writer.Put((byte)2);
            writer.Put(_secret);

            _manager.Connect(_ip, _port, writer);
            _isConnecting = true;
        }
    }
#endif

#if NET48
    public static void Send(
        ushort messageId,
        Action<NetDataWriter> payload,
        DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        if (!IsConnected)
            return;

        var writer = new NetDataWriter();
        writer.Put(messageId);
        payload?.Invoke(writer);

        _peer.Send(writer, method);
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
