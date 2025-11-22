using SiteLink.API.Events;
using SiteLink.API.Events.Args;
using System;
using YamlDotNet.Core.Tokens;

namespace SiteLink.Core;

/// <summary>
/// Represents a network connection for a client, handling communication and state.
/// </summary>
public class Connection : IDisposable
{
    private const string Separator =  "-> ";

    private NetManager _netManager;
    private EventBasedNetListener _listener;

    /// <summary>
    /// Gets a value indicating whether this is the main connection.
    /// </summary>
    public bool IsMain { get; set; } = true;

    public bool IsSilent { get; set; } = false;

    /// <summary>
    /// Gets a value indicating whether the connection is currently established.
    /// </summary>
    public bool IsConnected => IsValid && _netManager.FirstPeer != null || IsConnectedToSimulated;

    /// <summary>
    /// Gets a value indicating whether the NetManager instance is valid (not null).
    /// </summary>
    public bool IsValid => _netManager != null;

    public bool IsConnectedToSimulated { get; set; }

    public bool IsConnecting;

    /// <summary>
    /// Gets or sets the client associated with this connection.
    /// </summary>
    public Client Client { get; private set; }

    /// <summary>
    /// Gets the server this connection is associated with.
    /// </summary>
    public Server Server { get; private set; }

    public ChallengeHandler Challenge { get; set; }

    /// <summary>
    /// Sets up the connection for the specified client.
    /// </summary>
    public void Setup(Client client)
    {
        Challenge = new ChallengeHandler(this);
        Client = client;

        if (_listener == null)
        {
            _listener = new EventBasedNetListener();

            _listener.PeerConnectedEvent += OnConnected;
            _listener.NetworkReceiveEvent += OnReceiveData;
            _listener.PeerDisconnectedEvent += OnDisconnected;
        }

        if (_netManager == null)
        {
            _netManager = new NetManager(_listener)
            {
                UpdateTime = 5,
                ChannelsCount = (byte)6,
                DisconnectTimeout = 1000,
                ReconnectDelay = 300,
                MaxConnectAttempts = 3,
            };

            _netManager.Start();
        }
    }

    /// <summary>
    /// Updates the connection state.
    /// </summary>
    public void Update()
    {
        if (_netManager == null)
            return;

        if (!_netManager.IsRunning)
            return;

        try
        {
            _netManager.PollEvents();
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    /// <summary>
    /// Attempts to establish a connection to the specified server using the provided connection data.
    /// Handles both simulated and real server connections, and manages connection state.
    /// </summary>
    /// <param name="server">The server to connect to.</param>
    /// <param name="connectionData">The data required for the connection.</param>
    /// <param name="reconnect">Indicates whether this is a reconnection attempt.</param>
    public bool TryMakeConnection(Server server, NetDataWriter connectionData, bool reconnect = false)
    {
        if (!IsValid)
            return false;

        if (IsConnecting)
        {
            if (Client.Object != null)
                Client.SendHint("Already connecting to a server...", 3);
            return false;
        }

        IsConnectedToSimulated = false;

        Server = server;

        if (!IsSilent)
            SiteLinkLogger.Info($"{Client.Tag} {(Client.Server == null ? string.Empty : Separator)}{server.Tag} {(reconnect ? "Reconnecting..." : "Connecting...")}");

        if (server.IsSimulated)
        {
            bool canJoin = server.InternalClientConnecting(Client);

            if (canJoin)
            {
                AcceptConnection();
                IsConnectedToSimulated = true;

                server.InternalClientConnected(Client);
                return true;
            }

            return false;
        }

        _netManager.Connect(Server.IpAddress, Server.Port, connectionData);

        IsConnecting = true;
        return true;
    }

    /// <summary>
    /// Attempts to reconnect to the current server using the provided connection data.
    /// </summary>
    /// <param name="connectionData">The data required for the connection.</param>
    public void Reconnect(NetDataWriter connectionData)
    {
        if (!IsValid)
            return;

        TryMakeConnection(Server, connectionData, true);
    }

    /// <summary>
    /// Sends data over the connection.
    /// </summary>
    public void Send(byte[] bytes, int position, int length, DeliveryMethod method)
    {
        if (!IsConnected)
            return;

        if (IsConnectedToSimulated)
            return;

        _netManager.FirstPeer.Send(bytes, position, length, method);
    }

    void OnDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        IsConnecting = false;

        switch (disconnectInfo.Reason)
        {
            default:
                SiteLinkLogger.Info($"{Client.Tag} Disconnect undefined {disconnectInfo.Reason}");
                break;

            case DisconnectReason.ConnectionFailed when disconnectInfo.AdditionalData.RawData == null:

                if (!IsSilent)
                    SiteLinkLogger.Info($"{Client.Tag} {(Client.Server == null ? string.Empty : Separator)}{Server.Tag} Server is (f=red)offline(f=white)!");

                if (!IsMain)
                {
                    Client.InvokeConnectionResponse(Server, IsSilent, new ServerIsOfflineResponse());
                    return;
                }

                Client.TakeServerAndTryConnect();
                return;

            case DisconnectReason.ConnectionRejected when disconnectInfo.AdditionalData.RawData != null:
                NetDataWriter rejectedData = NetDataWriter.FromBytes(disconnectInfo.AdditionalData.RawData, disconnectInfo.AdditionalData.UserDataOffset, disconnectInfo.AdditionalData.UserDataSize);

                if (!disconnectInfo.AdditionalData.TryGetByte(out byte lastRejectionReason))
                    break;

                RejectionReason reason = (RejectionReason)lastRejectionReason;

                bool cancel = false;

                switch (reason)
                {
                    case RejectionReason.RateLimit:
                        Client.AddToReconnectAttempt(TimeSpan.FromSeconds(4));
                        break;

                    case RejectionReason.Delay:
                        if (!disconnectInfo.AdditionalData.TryGetByte(out byte offset))
                            break;

                        if (!IsMain)
                        {
                            Client.InvokeConnectionResponse(Server, IsSilent, new DelayConnectionResponse(offset));
                            return;
                        }

                        SiteLinkLogger.Info($"{Client.Tag} Delay connecting to (f=yellow){Server.IpAddress}:{Server.Port}(f=white) by {offset} seconds!");
                        break;

                    case RejectionReason.ServerFull:

                        if (!IsSilent)
                            SiteLinkLogger.Info($"{Client.Tag} Server (f=yellow){Server.IpAddress}:{Server.Port}(f=white) is full!");

                        if (!IsMain)
                        {
                            Client.InvokeConnectionResponse(Server, IsSilent, new ServerIsFullResponse());
                            return;
                        }

                        Client.OnDisconnectedFromServerInternal(Server, new ConnectionFailedInfo($"Server {Server.IpAddress}:{Server.Port} is full!", DisconnectType.ServerIsFull));
                        break;

                    case RejectionReason.Banned:
                        long expireTime = disconnectInfo.AdditionalData.GetLong();
                        string banReason = disconnectInfo.AdditionalData.GetString();

                        var date = new DateTime(expireTime, DateTimeKind.Utc).ToLocalTime();

                        if (!IsMain)
                        {
                            Client.InvokeConnectionResponse(Server, IsSilent, new BannedResponse(banReason, date));
                            return;
                        }

                        SiteLinkLogger.Info($"{Client.Tag} Banned from (f=yellow){Server.IpAddress}:{Server.Port}(f=white) with reason (f=yellow){banReason}(f=white)!");
                        break;

                    case RejectionReason.Challenge:
                        Challenge.ProcessChallenge(Server.ForwardIpAddress, disconnectInfo.AdditionalData);
                        break;

                    default:

                        break;
                }

                if (cancel)
                    return;

                //Client.Disconnect(writer: rejectedData);
                return;

            case DisconnectReason.Timeout:
            case DisconnectReason.PeerNotFound:
                SiteLinkLogger.Info($"{Client.Tag} Server timeout!");

                Client.AddToActions(
                    new TimedAction(
                        "ReconnectSequence",
                        TimeSpan.Zero,
                        p => p.SendHint("Server timeout\n\nConnecting to fallback server...", 6),
                        null,
                        new TimedAction("ConnectToServer", TimeSpan.FromSeconds(3), p =>
                        {
                            if (Server.Settings.FallbackServers.Length == 0)
                            {
                                p.Disconnect($"Server timeout!\n\nTheres no fallback servers set for '{Server.Name}'");
                                return;
                            }

                            p.Connect(Server.Settings.FallbackServers);
                        }
                    )
                ));
                return;

            case DisconnectReason.RemoteConnectionClose:
                SiteLinkLogger.Info("Remote connection closed, check LastResponse");

                switch (Client.LastResponse)
                {
                    case RoundRestartResponse roundRestart:
                        SiteLinkLogger.Info($"{Client.Tag} Server (f=yellow){Server.Name}(f=white) is restarting, reconnect in (f=yellow){roundRestart.TimeOffset}(f=white) seconds");

                        Client.AddToActions(
                            new TimedAction(
                                "ReconnectSequence",
                                TimeSpan.Zero,
                                p => p.SendHint($"Server '<color=orange>{Server.Name}</color> is restarting round...", 6),
                                null,
                                new TimedAction("ConnectToServer", TimeSpan.FromSeconds(roundRestart.TimeOffset), p =>
                                {
                                    if (Server.Settings.FallbackServers.Length == 0)
                                    {
                                        p.Disconnect($"Server timeout!\n\nTheres no fallback servers set for '{Server.Name}'");
                                        return;
                                    }

                                    p.Connect(Server.Settings.FallbackServers);
                                }
                            )
                        ));
                        return;
                    default:
                        SiteLinkLogger.Info($"{Client.Tag} Remote server closed connection! Connect to -> Lobby");
                        Client.Connect("lobby");
                        return;
                }
        }
    }

    void OnReceiveData(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        byte[] bytes = reader.RawData;
        int pos = reader.Position;
        int length = reader.AvailableBytes;

        if (!Client.ProcessMirrorDataFromServer(ref bytes, ref pos, ref length))
        {
            reader.Recycle();
            return;
        }

        Client.SendData(bytes, pos, length, deliveryMethod);

        reader.Recycle();
    }

    void AcceptConnection()
    {
        Client.AcceptConnection();

        EventManager.Client.InvokeJoinedServer(new ClientJoinedServerEvent(Client, Server));

        Client.AddToActions(new TimedAction("Reset0", TimeSpan.Zero, p =>
        {
            if (Client.Object != null) Client.SetRole(PlayerRoles.RoleTypeId.Destroyed);
        }, 
        nextAction: new TimedAction("Reset1", TimeSpan.FromSeconds(1), p =>
        {
            if (Client.Object != null)  Client.SendSeed(-1);

            Client.NotReady();

            if (Client.Object != null) Client.FastRoundrestart();

            Client.SendToScene("Facility");

            Client.Connection = this;
            Client.LastResponse = null;
        })));
    }

    void OnConnected(NetPeer peer)
    {
        AcceptConnection();
        Server.InternalClientConnected(Client);

        IsConnecting = false;
    }

    /// <summary>
    /// Disconnects the connection.
    /// </summary>
    public void Disconnect()
    {
        if (_netManager == null)
            return;

        Server?.InternalClientDisconnected(Client);

        if (!IsConnected)
            return;

        if (!IsConnectedToSimulated)
            _netManager.FirstPeer.Disconnect();

        _netManager?.Stop();
        _netManager = null;

        switch (Client.LastResponse)
        {
            case RoundRestartResponse roundRestart:
                Client.Reconnect(Server.Name, roundRestart.TimeOffset, "is restarting");
                return;
        }
    }

    /// <summary>
    /// Disposes the connection and releases all resources.
    /// </summary>
    public void Dispose()
    {
        IsConnecting = false;

        Disconnect();

        if (_listener != null)
        {
            _listener.PeerConnectedEvent -= OnConnected;
            _listener.NetworkReceiveEvent -= OnReceiveData;
            _listener.PeerDisconnectedEvent -= OnDisconnected;

            _listener = null;
        }
    }
}
