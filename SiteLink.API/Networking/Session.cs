using PlayerRoles;
using RelativePositioning;
using SiteLink.API.Metrics;
using SiteLink.API.Networking.Connections;
using SiteLink.API.Threading;
using SiteLink.Core;
using System.Buffers;

namespace SiteLink.API.Networking
{
    [ThreadAffined("SessionService")]
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

        private readonly int _ownerThreadId;

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

        private World _world;
        private bool _isSpawned;

        public bool IsSpawned
        {
            get => _isSpawned;
            set
            {
                if (!_isSpawned)
                    Server?.OnSessionSpawned(this);

                _isSpawned = value;
            }
        }

        public PlayerObject Player { get; set; }

        public SessionStats Stats { get; } = new SessionStats();

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
                        Player = null;

                    _world.Unload(this, value);
                }
                else
                {
                    Player = null;
                }

                _world = value;

                if (value != null)
                    value.Load(this);
            }
        }

        public bool IsSilent { get; }

        // Position System
        /// <summary>
        /// Current horizontal rotation.
        /// </summary>
        public float HorizontalRotation { get; internal set; }

        /// <summary>
        /// Current vertical rotation.
        /// </summary>
        public float VerticalRotation { get; internal set; }

        public PlayerMovementState MovementState { get; internal set; }
        internal bool HasFpcPosition;
        internal bool HasFpcMouseLook;
        internal bool HasFpcCustomData;
        internal ushort HorizontalRotationRaw { get; set; }
        internal ushort VerticalRotationRaw { get; set; }

        /// <summary>
        /// Gets the current relative position of the client.
        /// </summary>
        public RelativePosition RelativePosition { get; internal set; }

        /// <summary>
        /// Gets the absolute position of the client in the world.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                if (World == null)
                    return Vector3.zero;

                if (World.Waypoints.TryGetValue(RelativePosition.WaypointId, out WaypointToyObject obj))
                    return obj.Position + RelativePosition.Relative;

                return Vector3.zero;
            }
        }

        private WeakReference<RemoteConnection> _connectionReference;
        private readonly BatchInterceptor _serverToClient = new(PacketDirection.ServerToClient);

        public RemoteConnection Connection
        {
            get
            {
                if (_connectionReference == null || !_connectionReference.TryGetTarget(out RemoteConnection connection))
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

                _connectionReference = new WeakReference<RemoteConnection>(value);

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

        private Server _shutdownRetryServer;
        private int _shutdownRetryAttempts;
        private int _shutdownRetryAttemptsMade;
        private TimeSpan _shutdownRetryInterval;
        private DateTime _nextShutdownRetry = DateTime.MaxValue;
        private string _shutdownWaitingMessage;
        private string _shutdownUnreachableMessage;
        private bool _shutdownRetryFinished;

        public uint NetworkId { get; private set; }
        public string Nickname { get; set; }
        public string UserId { get; private set; }

        public int MapSeed { get; private set; } = -1;

        public bool IsReady { get; internal set; }
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

        /// <summary>
        /// Gets the thread ID that owns this session.
        /// </summary>
        public int OwnerThreadId => _ownerThreadId;

        public MirrorSender AsClient { get; } // sends to server

        public Session(RemoteConnection connection, Server[] servers, int ownerThreadId, bool isSilent)
        {
            IsSilent = isSilent;
            Connection = connection;

            _ownerThreadId = ownerThreadId;
            ThreadOwner.Register(this, "SessionService", _ownerThreadId);

            ConnectToServers = new Queue<Server>(servers);

            Challenge = new ChallengeHandler(this);

            if (!IsSilent)
                SiteLinkLogger.Info(servers.Length > 1
                    ? $"{Connection.Tag} Connecting to one of (f=yellow){servers.Length}(f=white) servers..."
                    : $"{Connection.Tag} Connecting to server (f=yellow){servers[0].Name}(f=white)...");

            AsClient = new MirrorSender(
                SiteLinkAPI.ThresholdBytes,
                () => SessionTime.TotalSeconds,
                (bytes, offset, length, method) =>
                {
                    // proxy -> server
                    SendToServer(bytes, offset, length, method);
                });

            _serverToClient.Register(NetworkMessages.SeedMessage, OnReceiveSeed);
            _serverToClient.Register(NetworkMessages.NetworkPingMessage, OnPing);
            _serverToClient.Register(NetworkMessages.SpawnMessage, OnSpawn);
            _serverToClient.Register(NetworkMessages.RoundRestartMessage, OnRestart);
        }

        private InterceptResult OnRestart(ushort id, NetworkReader reader, ArraySegment<byte> original, Session session)
        {
            session.Connection.IsSwitchingServers = true;

            return InterceptResult.Drop();
        }

        private InterceptResult OnReceiveSeed(ushort id, NetworkReader reader, ArraySegment<byte> original, Session session)
        {
            int seed = reader.ReadInt();

            session.MapSeed = seed;

            if (SessionManager.Singleton.Slots.TryGetValue(session.UserId, out SessionSlot slot) && slot.Pending == null)
                return InterceptResult.Pass();

            return InterceptResult.Drop();
        }

        private InterceptResult OnSpawn(ushort id, NetworkReader reader, ArraySegment<byte> original, Session session)
        {
            uint networkId = reader.ReadUInt();
            bool isLocalPlayer = reader.ReadBool();
            bool isOwner = reader.ReadBool();

            ulong sceneId = reader.ReadULong();
            uint assetId = reader.ReadUInt();

            switch (assetId)
            {
                case PlayerObject.ObjectAssetId when isLocalPlayer && isOwner:
                    session.NetworkId = networkId;

                    session.Player = new PlayerObject(null, session, networkId);
                    break;
            }

            return InterceptResult.Pass();
        }

        private InterceptResult OnPing(ushort id, NetworkReader r, ArraySegment<byte> original, Session session)
        {
            if (IsDetached)
            {
                AsClient.Send(w =>
                {
                    w.WriteUShort(NetworkMessages.NetworkPongMessage);
                    w.WriteDouble(r.ReadDouble());
                });
            }

            return InterceptResult.Pass();
        }

        public void SpawnPlayer(Vector3 pos)
        {
            if (Player != null)
            {
                SiteLinkLogger.Error($"Player object already exists for {UserId}", "Session");
                return;
            }

            Player = new PlayerObject(World);
            Player.AssignOwner(this);
            Player.ReferenceHub.PlayerId = new RecyclablePlayerId(false);

            Player.Position = pos;

            NetworkId = Player.NetworkId;
        }

        public void AttachToConnection(RemoteConnection connection)
        {
            Connection = connection;
            IsDetached = false;
        }

        /// <summary>
        /// Marshals an action to execute on this session's owning thread (SessionService thread).
        /// </summary>
        /// <param name="action">The action to execute.</param>
        public void Execute(Action action)
        {
            if (Thread.CurrentThread.ManagedThreadId == _ownerThreadId)
            {
                action();
            }
            else
            {
                Scheduler.Execute(this, action);
            }
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

                    SessionManager.Singleton.FailPending(
                        Connection?.PreAuth.UserId,
                        this,
                        "Simulated server rejected connection"
                    );

                    ConnectingToServer = null;
                    Status = SessionStatus.None;
                    return;
                }

                FinalizeConnection(ConnectingToServer, isSimulated: true);

                Connection?.AsServer.Scene("Facility");
                return;
            }

            EnsureNet();

            _netManager.Connect(ConnectingToServer.IpAddress, ConnectingToServer.Port, Connection.PreAuth.Create(ConnectingToServer.ForwardIpAddress, challengeId, challengeResponse));
        }

        public void RetryConnect(TimeSpan delay)
        {
            Stats.RecordReconnection();

            Status = SessionStatus.Retrying;

            NextRetry = DateTime.Now.Add(delay);

            if (!IsSilent)
                SiteLinkLogger.Info($"{Connection.Tag} Retrying connection to {ConnectingToServer.Tag} in {delay.TotalSeconds} seconds...");
        }

        public void SendToServer(byte[] data, int offset, int length, DeliveryMethod method)
        {
            if (_netManager?.FirstPeer == null)
                return;

            Stats.RecordBytesToServer(length);
            _netManager.FirstPeer.Send(data, offset, length, method);
        }

        /// <summary>
        /// Disconnects the session with an optional reason message.
        /// </summary>
        /// <param name="reason">The reason for disconnection</param>
        public void Disconnect(string reason = null)
        {
            if (Connection?.Session == this)
                Connection?.Disconnect(reason);
        }

        public void Update()
        {
            _netManager?.PollEvents();
            AsClient?.Update();

            UpdateShutdownRetry();

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
            Stats.RecordConnected();

            Status = SessionStatus.Connected;
            Server = server;

            IsConnectedToSimulated = isSimulated;

            SiteLinkLogger.Info(
                isSimulated
                    ? $"{Connection.Tag} Connected to simulated server (f=yellow){Server.Name}(f=white)!"
                    : $"{Connection.Tag} Connected to server (f=yellow){Server.Name}(f=white)!"
            );

            if (Connection.Session == null)
            {
                SessionManager.Singleton.PromotePendingToActive(
                    Connection.PreAuth.UserId,
                    this
                );

                AttachToConnection(Connection);

                Connection.AcceptRequest();
                Connection.Session = this;
                return;
            }

            Connection.Session?.Stats.RecordServerSwitch();

            SessionManager.Singleton.PromotePendingToActive(
                Connection.PreAuth.UserId,
                this
            );

            Connection.AsServer.Reconnect();
        }

        private void OnConnected(NetPeer peer) => FinalizeConnection(ConnectingToServer, isSimulated: false);

        private void OnDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            switch (disconnectInfo.Reason)
            {
                default:
                    SiteLinkLogger.Info($"{Connection?.Tag} Disconnect undefined {disconnectInfo.Reason}");
                    break;

                // Happens during server shutdown.
                case DisconnectReason.RemoteConnectionClose:
                    BeginShutdownRecovery();
                    return;

                case DisconnectReason.Timeout:
                    SiteLinkLogger.Info($"{Connection?.Tag} Server timeout! " + disconnectInfo.Reason + " " + disconnectInfo.SocketErrorCode);
                    Status = SessionStatus.Timeout;
                    Disconnect();
                    break;

                case DisconnectReason.ConnectionFailed when disconnectInfo.AdditionalData.RawData == null:
                    OnServerOffline.Invoke(new ServerOfflineResponse(ConnectingToServer, ConnectToServers.Count == 0));

                    ConnectingToServer = null;
                    return;

                case DisconnectReason.ConnectionRejected when disconnectInfo.AdditionalData.RawData != null:
                    NetDataWriter rejectedData = NetDataWriter.FromBytes(disconnectInfo.AdditionalData.RawData, disconnectInfo.AdditionalData.UserDataOffset, disconnectInfo.AdditionalData.UserDataSize);

                    if (!disconnectInfo.AdditionalData.TryGetByte(out byte lastRejectionReason))
                        break;

                    RejectionReason reason = (RejectionReason)lastRejectionReason;

                    switch (reason)
                    {
                        case RejectionReason.ExpiredAuth:
                            Disconnect("Expired auth");
                            break;

                        case RejectionReason.RateLimit:
                            RetryConnect(TimeSpan.FromSeconds(4));
                            return;

                        case RejectionReason.Delay:
                            if (!disconnectInfo.AdditionalData.TryGetByte(out byte offset))
                                break;

                            OnConnectionDelayed.Invoke(new ConnectionDelayedResponse(ConnectingToServer, offset));
                            return;

                        case RejectionReason.ServerFull:
                            OnServerFull?.Invoke(new ServerFullResponse(ConnectingToServer, ConnectToServers.Count == 0));

                            ConnectingToServer = null;
                            return;

                        case RejectionReason.Banned:
                            long expireTime = disconnectInfo.AdditionalData.GetLong();
                            string banReason = disconnectInfo.AdditionalData.GetString();
                            DateTime date = new DateTime(expireTime, DateTimeKind.Utc).ToLocalTime();

                            OnBanned?.Invoke(new BannedResponse(ConnectingToServer, banReason, date));
                            return;

                        case RejectionReason.Challenge:
                            Challenge.ProcessChallenge(disconnectInfo.AdditionalData);
                            return;

                        default:
                            SiteLinkLogger.Info($"{Connection.Tag} Disconnected: {reason}");
                            break;
                    }

                    break;
            }

            Disconnect();
        }

        private void BeginShutdownRecovery()
        {
            Server shutdownServer = Server;

            if (shutdownServer == null || Connection == null)
                return;

            ConfigureShutdownRetry(shutdownServer);
            ShowShutdownRetryStatus();

            SiteLinkLogger.Info(
                $"{Connection.Tag} Server (f=yellow){shutdownServer.Name}(f=white) shut down; " +
                $"retrying (f=yellow){_shutdownRetryAttempts}(f=white) time(s) every " +
                $"(f=yellow){_shutdownRetryInterval.TotalSeconds:0.##}(f=white) second(s) before trying fallbacks."
            );

            if (_shutdownRetryAttempts == 0)
            {
                _nextShutdownRetry = DateTime.UtcNow;
            }
        }

        private void TryFallbackServersAfterShutdown()
        {
            _shutdownRetryFinished = true;
            string unreachableMessage = FormatShutdownRetryMessage(_shutdownUnreachableMessage);

            Server[] fallbackServers = (_shutdownRetryServer.Settings?.FallbackServers ?? Array.Empty<string>())
                .Select(name => SiteLink.API.Core.Server.Get<Server>(name: name))
                .Where(server => server != null && server != _shutdownRetryServer)
                .Distinct()
                .ToArray();

            Connection?.AsServer.Hint(unreachableMessage, 8f);

            if (fallbackServers.Length == 0 || Connection == null)
            {
                Disconnect(unreachableMessage);
                return;
            }

            Session fallbackSession = SessionManager.Singleton.CreateOrSwitchSession(
                Connection,
                fallbackServers,
                silent: true
            );

            if (fallbackSession == null)
            {
                Disconnect(unreachableMessage);
                return;
            }

            bool disconnected = false;

            void DisconnectAfterFinalFallbackFailure()
            {
                if (disconnected)
                    return;

                disconnected = true;
                Disconnect(unreachableMessage);
            }

            fallbackSession.OnServerOffline += response =>
            {
                if (response.IsFinalResponse)
                    DisconnectAfterFinalFallbackFailure();
            };

            fallbackSession.OnServerFull += response =>
            {
                if (response.IsFinalResponse)
                    DisconnectAfterFinalFallbackFailure();
            };

            fallbackSession.OnBanned += _ => DisconnectAfterFinalFallbackFailure();

            SiteLinkLogger.Info(
                $"{Connection.Tag} Server (f=yellow){_shutdownRetryServer.Name}(f=white) did not recover; trying fallback servers: " +
                $"(f=yellow){string.Join("(f=white) -> (f=yellow)", fallbackServers.Select(server => server.Name))}(f=white)"
            );
        }

        private void ConfigureShutdownRetry(Server shutdownServer)
        {
            ServerSettings settings = shutdownServer.Settings;

            _shutdownRetryServer = shutdownServer;
            _shutdownRetryAttempts = Math.Max(0, settings?.ShutdownRetryAttempts ?? 0);
            _shutdownRetryAttemptsMade = 0;
            _shutdownRetryInterval = TimeSpan.FromSeconds(Math.Max(0.1f, settings?.ShutdownRetryInterval ?? 10f));
            _nextShutdownRetry = DateTime.UtcNow.Add(_shutdownRetryInterval);
            _shutdownWaitingMessage = settings?.ShutdownWaitingMessage;
            _shutdownUnreachableMessage = settings?.ShutdownUnreachableMessage;
            _shutdownRetryFinished = _shutdownRetryAttempts == 0;
        }

        private void ShowShutdownRetryStatus()
        {
            if (_shutdownRetryServer == null || _shutdownRetryFinished)
                return;

            Connection?.AsServer.Hint(
                FormatShutdownRetryMessage(_shutdownWaitingMessage),
                Math.Max(3f, (float)_shutdownRetryInterval.TotalSeconds + 0.5f)
            );
        }

        private void UpdateShutdownRetry()
        {
            if (_shutdownRetryServer == null || _shutdownRetryFinished || Status != SessionStatus.Connected)
                return;

            if (_nextShutdownRetry > DateTime.UtcNow)
                return;

            if (SessionManager.Singleton.Slots.TryGetValue(UserId, out SessionSlot slot) && slot.Pending != null)
                return;

            if (_shutdownRetryAttemptsMade >= _shutdownRetryAttempts)
            {
                TryFallbackServersAfterShutdown();
                return;
            }

            Session retrySession = SessionManager.Singleton.CreateOrSwitchSession(
                Connection,
                new[] { _shutdownRetryServer },
                silent: true
            );

            if (retrySession == null)
            {
                _nextShutdownRetry = DateTime.UtcNow.Add(_shutdownRetryInterval);
                return;
            }

            _shutdownRetryAttemptsMade++;
            _nextShutdownRetry = DateTime.UtcNow.Add(_shutdownRetryInterval);

            ShowShutdownRetryStatus();

            void FinishImmediatelyAfterLastFailure()
            {
                if (_shutdownRetryAttemptsMade >= _shutdownRetryAttempts)
                    _nextShutdownRetry = DateTime.UtcNow;
            }

            retrySession.OnServerOffline += response =>
            {
                if (response.IsFinalResponse)
                    FinishImmediatelyAfterLastFailure();
            };

            retrySession.OnServerFull += response =>
            {
                if (response.IsFinalResponse)
                    FinishImmediatelyAfterLastFailure();
            };

            retrySession.OnBanned += _ => FinishImmediatelyAfterLastFailure();
        }

        private string FormatShutdownRetryMessage(string message)
        {
            message ??= string.Empty;

            return message
                .Replace("{server}", _shutdownRetryServer?.DisplayName ?? string.Empty)
                .Replace("{server_name}", _shutdownRetryServer?.Name ?? string.Empty)
                .Replace("{attempts}", _shutdownRetryAttempts.ToString())
                .Replace("{interval}", _shutdownRetryInterval.TotalSeconds.ToString("0.##"));
        }

        private void OnReceiveDataFromServer(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            byte[] bytes = reader.RawData;
            int position = reader.Position;
            int length = reader.AvailableBytes;

            Stats.RecordBytesFromServer(length);

            if (!_serverToClient.TryRewrite(this, bytes, position, length, out var outBytes, out var outPos, out var outLen, out bool pooled))
            {
                if (Connection?.Session != this)
                    return;

                Connection.SendToConnection(bytes, position, length, deliveryMethod);
                return;
            }

            if (Connection?.Session == this)
                Connection.SendToConnection(outBytes, outPos, outLen, deliveryMethod);

            // Return pooled array to pool
            if (pooled && !ReferenceEquals(outBytes, bytes))
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
                UpdateTime = NetSettings.UpdateTime,
                ChannelsCount = NetSettings.ChannelsCount,
                DisconnectTimeout = NetSettings.SessionDisconnectTimeout,
                ReconnectDelay = NetSettings.SessionReconnectDelay,
                MaxConnectAttempts = NetSettings.SessionMaxConnectAttempts,
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
            World = null;

            _connectionReference = null;

            Challenge = null;

            DestroyNet();

            ConnectToServers = null;
            ConnectingToServer = null;

            Server = null;
        }
    }
}
