using Mirror;
using Org.BouncyCastle.Utilities;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using SiteLink.API.Core;
using SiteLink.Core;
using System.Buffers;
using UserSettings.ServerSpecific;

namespace SiteLink.API.Networking
{
    public enum SessionStatus
    {
        None,
        Connecting,
        Challenge,
        PreAuthentication,
        Connected,
        Retrying,
    }

    public class Session : IDisposable
    {
        public class ServerFullResponse
        {
            public ServerFullResponse(Server server, bool isFinalResponse)
            {
                Server = server;
                IsFinalResponse = isFinalResponse;
            }

            public Server Server { get; }
            public bool IsFinalResponse { get; }
        }

        public class ConnectionDelayedResponse
        {
            public ConnectionDelayedResponse(Server server, byte offset)
            {
                Server = server;
                Offset = offset;
            }

            public Server Server { get; }
            public byte Offset { get; }
        }

        public class ServerOfflineResponse
        {
            public ServerOfflineResponse(Server server, bool isFinalResponse)
            {
                Server = server;
                IsFinalResponse = isFinalResponse;
            }

            public Server Server { get; }
            public bool IsFinalResponse { get; }
        }

        public class BannedResponse
        {
            public BannedResponse(Server server, string reason, DateTime expires)
            {
                Server = server;
                Reason = reason;
                Expires = expires;
            }

            public Server Server { get; }
            public string Reason { get; }
            public DateTime Expires { get; }
        }

        /// <summary>
        /// The inverse accuracy constant for position calculations.
        /// </summary>
        public const float InverseAccuracy = 0.00390625f;

        private World _world;

        public PlayerObject PlayerObject { get; set; }

        /// <summary>
        /// Gets or sets the world this client is currently in.
        /// Setting this property will load or unload the client from the world as appropriate.
        /// </summary>
        public World World
        {
            get => _world;
            set
            {
                if (_world != null)
                {
                    if (value == null)
                        PlayerObject = null;

                    _world.Unload(this, value);
                }
                else
                {
                    PlayerObject = null;
                }

                _world = value;

                if (value != null)
                    value.Load(this);
            }
        }

        // Position System

        /// <summary>
        /// Gets the waypoint ID associated with this client.
        /// </summary>
        public byte WaypointId { get; private set; }

        /// <summary>
        /// Current horizontal rotation.
        /// </summary>
        public float HorizontalRotation { get; private set; }

        /// <summary>
        /// Current vertical rotation.
        /// </summary>
        public float VerticalRotation { get; private set; }

        /// <summary>
        /// Gets the current relative position of the client.
        /// </summary>
        public Vector3 RelativePosition { get; private set; }

        /// <summary>
        /// Gets the absolute position of the client in the world.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                if (World == null)
                    return Vector3.zero;

                if (World.Waypoints.TryGetValue(WaypointId, out WaypointToyObject obj))
                    return obj.Position + RelativePosition;

                return Vector3.zero;
            }
        }

        private WeakReference<Connection> _connectionReference;
        private readonly BatchInterceptor _serverToClient = new();

        public Connection Connection
        {
            get
            {
                if (_connectionReference == null || !_connectionReference.TryGetTarget(out Connection connection))
                    return null;

                return connection;
            }
            set
            {
                if (value == null)
                {
                    _connectionReference = null;
                    return;
                }

                _connectionReference = new WeakReference<Connection>(value);

                Nickname = $"Unknown";
                UserId = value.PreAuth.UserId;
            }
        }

        private Server _server;

        public ChallengeHandler Challenge { get; private set; }

        private NetManager _netManager;
        private EventBasedNetListener _listener;

        private Queue<Server> ConnectToServers;

        public Server ConnectingToServer;

        public DateTime AliveUntil { get; set; } = DateTime.MinValue;

        public DateTime? DetachedAtUtc { get; set; }

        public bool WasDetached { get; set; }
        public int LastExpiryLogSecond { get; set; } = -1; // prevents spam
        public bool IsDetached { get; private set; } = true;

        public Server Server
        {
            get => _server;
            private set
            {
                if (value != null)
                    value.InternalSessionConnected(this);
                else if (_server != null)
                    _server.InternalSessionDisconnected(this);

                _server = value;
            }
        }

        public SessionStatus Status { get; set; } = SessionStatus.None;

        public DateTime NextRetry { get; set; } = DateTime.MinValue;

        public uint NetworkId { get; private set; }
        public string Nickname { get; private set; }
        public string UserId { get; private set; }

        public int MapSeed { get; private set; } = -1;

        public bool IsConnectionConnected => Connection != null;
        public bool IsConnectedToSimulated { get; private set; }

        public Action<ServerOfflineResponse> OnServerOffline;

        public Action<ServerFullResponse> OnServerFull;

        public Action<ConnectionDelayedResponse> OnConnectionDelayed;

        public Action<BannedResponse> OnBanned;

        /// <summary>
        /// Gets the time the session created.
        /// </summary>
        public DateTime CreatedOn { get; } = DateTime.Now;

        /// <summary>
        /// Gets the duration of the current session.
        /// </summary>
        public TimeSpan SessionTime => DateTime.Now - CreatedOn;

        public Session(Connection connection, Server[] servers)
        {
            Connection = connection;

            ConnectToServers = new Queue<Server>(servers);

            Challenge = new ChallengeHandler(this);

            SiteLinkLogger.Info(servers.Length > 1
                ? $"{Connection.Tag} Connecting to one of {servers.Length} servers..."
                : $"{Connection.Tag} Connecting to server {servers[0].Tag}...");

            _serverToClient.Register(NetworkMessages.SeedMessage, static (id, r, original, session) =>
            {
                int seed = r.ReadInt();

                session.MapSeed = seed;

                SiteLinkLogger.Info(session.Connection.Tag + $" Received map seed (f=green){seed}(f=white) from server.");

                return InterceptResult.Pass();
            });
        }

        public void AttachToConnection(Connection connection)
        {
            Connection = connection;
            IsDetached = false;
        }

        public void DetachFromConnection()
        {
            Connection = null;
            IsDetached = true;
        }

        public void Connect(int challengeId = 0, byte[] challengeResponse = null)
        {
            Status = challengeId == 0 ? SessionStatus.Connecting : SessionStatus.PreAuthentication;

            IsConnectedToSimulated = false;

            if (ConnectingToServer.IsSimulated)
            {
                DestroyNet();

                bool canJoin = ConnectingToServer.InternalSessionConnecting(this);

                if (!canJoin)
                {
                    OnServerFull?.Invoke(
                        new ServerFullResponse(ConnectingToServer, ConnectToServers.Count == 0)
                    );

                    Connection?.Listener.SessionManager.FailPending(
                        Connection?.PreAuth.UserId,
                        this,
                        "Simulated server rejected connection"
                    );

                    ConnectingToServer = null;
                    Status = SessionStatus.None;
                    return;
                }

                FinalizeConnection(ConnectingToServer, isSimulated: true);

                ConnectingToServer.InternalSessionConnected(this);
                return;
            }

            EnsureNet();

            _netManager.Connect(ConnectingToServer.IpAddress, ConnectingToServer.Port, Connection.PreAuth.Create(ConnectingToServer.ForwardIpAddress, challengeId, challengeResponse));
        }

        public void RetryConnect(TimeSpan delay)
        {
            Status = SessionStatus.Retrying;

            NextRetry = DateTime.Now.Add(delay);
            SiteLinkLogger.Info($"{Connection.Tag} Retrying connection to {ConnectingToServer.Tag} in {delay.TotalSeconds} seconds...");
        }

        public void SendToServer(byte[] data, int offset, int length, DeliveryMethod method)
        {
            if (_netManager == null)
                return;

            _netManager.FirstPeer.Send(data, offset, length, method);
        }

        /// <summary>
        /// Disconnects the session with an optional reason message.
        /// </summary>
        /// <param name="reason">The reason for disconnection</param>
        public void Disconnect(string reason = null)
        {
            if (_netManager?.FirstPeer != null)
            {
                if (!string.IsNullOrEmpty(reason))
                {
                    //SiteLinkLogger.Info($"Disconnecting session: {reason}");
                }

                _netManager.FirstPeer.Disconnect();
            }
        }

        public void Update()
        {
            if (_netManager != null)
                _netManager.PollEvents();

            if (ConnectingToServer == null && ConnectToServers != null && ConnectToServers.Count > 0)
            {
                ConnectingToServer = ConnectToServers.Dequeue();

                Connect();
            }

            switch (Status)
            {
                case SessionStatus.Retrying when NextRetry < DateTime.Now:
                    Connect();
                    break;
            }
        }

        private void FinalizeConnection(Server server, bool isSimulated)
        {
            Status = SessionStatus.Connected;
            Server = server;

            IsConnectedToSimulated = isSimulated;

            SiteLinkLogger.Info(
                isSimulated
                    ? $"{Connection.Tag} Connected to simulated server!"
                    : $"{Connection.Tag} Connected to server!"
            );

            // If client already has an active session, this is a pending switch attempt
            if (Connection.Session != null && Connection.Session != this)
            {
                Connection.Listener.SessionManager.PromotePendingToActive(
                    Connection.PreAuth.UserId,
                    this
                );

                // Tell proxy client to reconnect (switch servers)
                Connection.AsServer.Reconnect();
                return;
            }

            // First / initial session
            Connection.Session = this;
        }

        private void OnConnected(NetPeer peer) => FinalizeConnection(ConnectingToServer, isSimulated: false);

        private void OnDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            switch (disconnectInfo.Reason)
            {
                default:
                    SiteLinkLogger.Info($"{Connection.Tag} Disconnect undefined {disconnectInfo.Reason}");
                    break;

                case DisconnectReason.ConnectionFailed when disconnectInfo.AdditionalData.RawData == null:
                    OnServerOffline.Invoke(new ServerOfflineResponse(ConnectingToServer, ConnectToServers.Count == 0));

                    ConnectingToServer = null;

                    Connection?.Listener.SessionManager.FailPending(Connection?.PreAuth.UserId, this, "server offline");
                    break;

                case DisconnectReason.ConnectionRejected when disconnectInfo.AdditionalData.RawData != null:
                    NetDataWriter rejectedData = NetDataWriter.FromBytes(disconnectInfo.AdditionalData.RawData, disconnectInfo.AdditionalData.UserDataOffset, disconnectInfo.AdditionalData.UserDataSize);

                    if (!disconnectInfo.AdditionalData.TryGetByte(out byte lastRejectionReason))
                        break;

                    RejectionReason reason = (RejectionReason)lastRejectionReason;

                    switch (reason)
                    {
                        case RejectionReason.RateLimit:

                            RetryConnect(TimeSpan.FromSeconds(4));
                            break;

                        case RejectionReason.Delay:
                            if (!disconnectInfo.AdditionalData.TryGetByte(out byte offset))
                                break;

                            OnConnectionDelayed.Invoke(new ConnectionDelayedResponse(ConnectingToServer, offset));

                            Connection?.Listener.SessionManager.FailPending(Connection?.PreAuth.UserId, this, "connection delayed");
                            break;

                        case RejectionReason.ServerFull:
                            OnServerFull?.Invoke(new ServerFullResponse(ConnectingToServer, ConnectToServers.Count == 0));

                            ConnectingToServer = null;

                            Connection?.Listener.SessionManager.FailPending(Connection?.PreAuth.UserId, this, "full");
                            break;

                        case RejectionReason.Banned:
                            long expireTime = disconnectInfo.AdditionalData.GetLong();
                            string banReason = disconnectInfo.AdditionalData.GetString();
                            DateTime date = new DateTime(expireTime, DateTimeKind.Utc).ToLocalTime();

                            OnBanned?.Invoke(new BannedResponse(ConnectingToServer, banReason, date));

                            Connection?.Listener.SessionManager.FailPending(Connection?.PreAuth.UserId, this, "banned");
                            break;

                        case RejectionReason.Challenge:
                            Challenge.ProcessChallenge(disconnectInfo.AdditionalData);
                            break;

                        default:
                            SiteLinkLogger.Info($"{Connection.Tag} Disconnected: {reason}");
                            break;
                    }

                    break;
            }
        }

        private void OnReceiveDataFromServer(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            byte[] bytes = reader.RawData;
            int position = reader.Position;
            int length = reader.AvailableBytes;

            if (!_serverToClient.TryRewrite(this, bytes, position, length, out var outBytes, out var outPos, out var outLen, out bool pooled))
            {
                if (Connection?.Session != this)
                    return;

                Connection.SendToClient(bytes, position, length, deliveryMethod);
                return;
            }

            if (Connection?.Session == this)
                Connection.SendToClient(outBytes, outPos, outLen, deliveryMethod);

            if (!ReferenceEquals(outBytes, bytes))
                ArrayPool<byte>.Shared.Return(outBytes);
        }
        
        private void EnsureNet()
        {
            if (_netManager != null && _listener != null)
                return;

            _listener = new EventBasedNetListener();
            _listener.PeerConnectedEvent += OnConnected;
            _listener.NetworkReceiveEvent += OnReceiveDataFromServer;
            _listener.PeerDisconnectedEvent += OnDisconnected;

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

        private void DestroyNet()
        {
            if (_listener != null)
            {
                _listener.PeerConnectedEvent -= OnConnected;
                _listener.NetworkReceiveEvent -= OnReceiveDataFromServer;
                _listener.PeerDisconnectedEvent -= OnDisconnected;
            }

            _netManager?.Stop();
            _netManager = null;
            _listener = null;
        }

        public void Dispose()
        {
            _connectionReference = null;

            Challenge = null;

            DestroyNet();

            ConnectToServers = null;
            ConnectingToServer = null;

            Server = null;
        }
    }
}