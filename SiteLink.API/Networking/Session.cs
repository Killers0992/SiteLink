using PlayerRoles;
using RelativePositioning;
using SiteLink.API.Metrics;
using SiteLink.API.Networking.Connections;
using SiteLink.API.Threading;
using SiteLink.Core;
using System.Buffers;
using RoundRestarting;
using UserSettings.ServerSpecific;

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

                if (value && Connection != null)
                    Connection.HasEverSpawned = true;
            }
        }

        public PlayerObject Player { get; set; }

        /// <summary>
        /// Gets this player's selected language, falling back to settings.yml.
        /// </summary>
        public string Language => TranslationManager.GetLanguage(this);

        /// <summary>
        /// Gets the proxy-owned persistent data record for this player.
        /// Plugins should normally use Plugin.Data.For(UserId) for isolated storage.
        /// </summary>
        public PlayerDataRecord Data => StorageManager.Core.For(UserId);

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

        /// <summary>
        /// Latest moment a client that left because of a game server restart may still claim
        /// this session back. <see cref="DateTime.MinValue"/> means the default detach grace
        /// applies.
        /// <para>
        /// A restart hands the client back to its own reconnect countdown, which is up to
        /// <c>full_restart_rejoin_time</c> (25 seconds by default) - far longer than the ten
        /// seconds a detached session normally survives. Without this the session expired
        /// while the player was still staring at the vanilla restart screen, and they came
        /// back as a brand new connection that got routed by priority instead of back to the
        /// server they were playing on.
        /// </para>
        /// </summary>
        public DateTime ReconnectDeadline { get; internal set; } = DateTime.MinValue;

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
        private float _recoveryInitialDelay;

        /// <summary>
        /// How often the recovery hint is re-sent while the game server is away.
        /// <para>
        /// Hints replace each other on the client and then fade, so a single hint sent once
        /// per reconnect attempt left the screen blank for most of the outage - which is
        /// what made players think the server had frozen and quit. Refreshing faster than
        /// the hint's own lifetime keeps one continuous message on screen.
        /// </para>
        /// </summary>
        private static readonly TimeSpan RecoveryHintInterval = TimeSpan.FromSeconds(1);

        /// <summary>Hint lifetime, deliberately longer than the refresh interval so it never blinks.</summary>
        private const float RecoveryHintDuration = 2.5f;

        private DateTime _nextRecoveryHint = DateTime.MaxValue;

        /// <summary>
        /// Slack added on top of the restart offset before a detached session gives up on the
        /// client coming back.
        /// <para>
        /// The client waits the offset, then loads a scene, then re-authenticates against the
        /// central servers - none of which is instant, and none of which the proxy can see.
        /// Expiring a heartbeat early costs the player their slot for no reason, so the window
        /// is generous; a client that really left still frees the session when the grace runs
        /// out.
        /// </para>
        /// </summary>
        private const double RestartReconnectGraceSeconds = 25.0;

        /// <summary>
        /// When the proxy drops the client itself to finish the restart it just announced.
        /// <see cref="DateTime.MaxValue"/> while no restart is pending.
        /// </summary>
        private DateTime _restartDropClientAt = DateTime.MaxValue;

        /// <summary>
        /// How long the restart message gets to reach the client before its socket is closed.
        /// Long enough for the batch it rides in to be flushed, short enough that the player
        /// does not stare at a frozen facility first.
        /// </summary>
        private const double RestartClientDropDelaySeconds = 1.0;

        /// <summary>
        /// Set once the bridge has told us the game server came back and is accepting
        /// connections, so the countdown can stop lying about waiting.
        /// </summary>
        private bool _recoveryServerBack;

        public uint NetworkId { get; private set; }
        public string Nickname { get; set; }
        public string UserId { get; private set; }

        public int MapSeed { get; private set; } = -1;

        public bool IsRestarting { get; private set; }

        /// <summary>
        /// Set on the replacement session that a shutdown/restart recovery creates, so that
        /// connecting resumes the player in place instead of running the server-switch path.
        /// </summary>
        internal bool IsRecoveryRetry { get; set; }

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
                connection,
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
            _serverToClient.Register(NetworkMessages.SSSEntriesPack, OnServerSpecificEntries);
        }

        /// <summary>
        /// True once the game server has sent its own server-specific settings for this
        /// session. Until then there is nothing to append to.
        /// </summary>
        public bool HasGameServerSettings { get; private set; }

        /// <summary>
        /// Appends the proxy's own server-specific settings to the pack the game server
        /// sends.
        /// <para>
        /// Sending a separate pack does not work: the client keeps one set of entries per
        /// server, so whichever pack arrives last wins and the other side's settings vanish.
        /// The entries the game server wrote are copied through verbatim - they never need to
        /// be deserialized, only counted.
        /// </para>
        /// </summary>
        private InterceptResult OnServerSpecificEntries(ushort id, NetworkReader reader, ArraySegment<byte> original, Session session)
        {
            session.HasGameServerSettings = true;

            ServerSpecificSettingBase[] extra = session.Server?.GetExtraServerSpecificEntries(session);

            if (extra == null || extra.Length == 0)
                return InterceptResult.Pass();

            if (reader.Remaining < sizeof(int) + sizeof(byte))
                return InterceptResult.Pass();

            int version = reader.ReadInt();
            int count = reader.ReadByte();

            if (count + extra.Length > byte.MaxValue)
            {
                SiteLinkLogger.Warn($"{Connection?.Tag} Cannot append {extra.Length} server-specific entries: the game server already sent {count} and the wire format caps the total at {byte.MaxValue}.");
                return InterceptResult.Pass();
            }

            ArraySegment<byte> gameServerEntries = reader.ReadBytesSegment(reader.Remaining);

            NetworkWriter writer = new NetworkWriter();

            writer.WriteUShort(NetworkMessages.SSSEntriesPack);
            writer.WriteInt(version);
            writer.WriteByte((byte)(count + extra.Length));

            if (gameServerEntries.Count > 0)
                writer.WriteBytes(gameServerEntries.Array, gameServerEntries.Offset, gameServerEntries.Count);

            foreach (ServerSpecificSettingBase setting in extra)
            {
                writer.WriteByte(ServerSpecificSettingsSync.GetCodeFromType(setting.GetType()));
                setting.SerializeEntry(writer);
            }

            return InterceptResult.Replace(writer.ToArraySegment());
        }

        private InterceptResult OnRestart(ushort id, NetworkReader reader, ArraySegment<byte> original, Session session)
        {
            RoundRestartType type = (RoundRestartType)reader.ReadByte();

            switch (type)
            {
                // A fast restart tells the client to come back immediately and gives it no
                // countdown to display, which on a proxy reads as the screen simply blinking:
                // the facility reloads and the player never learns the round restarted. The
                // client is told about a full restart instead, so it gets the same waiting
                // screen every other restart produces, and the proxy holds its session for
                // the trip.
                case RoundRestartType.FastRestart:
                    SiteLinkLogger.Info($"{Connection?.Tag} Server is performing a fast restart.");

                    IsRestarting = true;

                    // A fast restart announces no delay of its own; without a bridge to tell
                    // us when it finished, the transport's own connection delay is the best
                    // estimate we have.
                    float fastRestartDelay = Math.Max(3f, session.Server?.BridgeConnectionDelaySeconds ?? 0);

                    session.BeginRestartRecovery(fastRestartDelay, extendedReconnectionPeriod: true);
                    return InterceptResult.Replace(
                        MirrorMessagesEx.BuildFullRestart(fastRestartDelay, extendedReconnectionPeriod: true)
                    );

                case RoundRestartType.RedirectRestart:
                    return InterceptResult.Pass();

                case RoundRestartType.FullRestart:
                    bool reconnect = reader.ReadBool();
                    bool extendedReconnectionPeriod = reconnect && reader.ReadBool();
                    float restartDelay = reader.ReadFloat();

                    if (!reconnect)
                        return InterceptResult.Pass();

                    SiteLinkLogger.Info($"{Connection?.Tag} Server closed the connection, likely due to restart.");

                    IsRestarting = true;

                    // Forwarded untouched. The offset the game server picked is its own
                    // estimate of when it will be back - a measured average for `rr`,
                    // full_restart_rejoin_time for `sr` - and the client's restart screen and
                    // countdown are driven entirely by it.
                    session.BeginRestartRecovery(restartDelay, extendedReconnectionPeriod);
                    return InterceptResult.Pass();

                default:
                    return InterceptResult.Pass();
            }
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

            // The client is back; whatever extra grace a restart bought it has been spent.
            ReconnectDeadline = DateTime.MinValue;
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

            UpdateRestartClientDrop();
            UpdateShutdownRetry();
            UpdateRecoveryHint();

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

            IsRestarting = false;
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

            if (IsRecoveryRetry)
            {
                ResumeAfterRecovery();
                return;
            }

            Connection.Session?.Stats.RecordServerSwitch();

            SessionManager.Singleton.PromotePendingToActive(
                Connection.PreAuth.UserId,
                this
            );

            Connection.AsServer.Reconnect();
        }

        /// <summary>
        /// Hands the client over to this session after a shutdown/restart recovery reconnect,
        /// without telling the client anything.
        /// <para>
        /// Recovery does not reconnect the old session, it builds a new one and swaps it in,
        /// so the normal server-switch path used to run here. That path sends a
        /// <c>RoundRestartMessage</c> (and, via the replaced session, a disconnect RPC), which
        /// threw the player out of the proxy for the six seconds it took to re-authenticate,
        /// destroyed the recovery hint that was the whole point, and occasionally surfaced as
        /// a bare kick. From the client's point of view nothing happened here: the same
        /// connection is now relayed to a freshly restarted server, and the server's own
        /// <c>SceneMessage</c> reloads the facility for it.
        /// </para>
        /// </summary>
        private void ResumeAfterRecovery()
        {
            // Take ownership of the connection first. PromotePendingToActive disconnects the
            // session it replaces, and that disconnect only fires while the connection still
            // points at the old session.
            AttachToConnection(Connection);
            Connection.Session = this;

            SessionManager.Singleton.PromotePendingToActive(
                Connection.PreAuth.UserId,
                this
            );

            // Promotion marks the connection as switching servers so that the expected client
            // disconnect detaches instead of destroying the session. No disconnect is coming,
            // and leaving the flag set would make a genuine quit leak the session.
            Connection.IsSwitchingServers = false;

            IsRecoveryRetry = false;
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
                case DisconnectReason.RemoteConnectionClose when !IsRestarting:
                    BeginShutdownRecovery();
                    return;

                // Happens when client losts connection with the server, usually due to network issues.
                case DisconnectReason.Timeout:
                    BeginShutdownRecovery();
                    return;

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
                            Disconnect(TranslationManager.Format(
                                TranslationManager.For(this).Connection.ExpiredAuthentication,
                                TranslationContext.For(this)).Format());
                            break;

                        case RejectionReason.RateLimit:
                            RetryConnect(TimeSpan.FromSeconds(4));
                            return;

                        case RejectionReason.Delay:
                            if (!disconnectInfo.AdditionalData.TryGetByte(out byte offset))
                                break;

                            OnConnectionDelayed.Invoke(new ConnectionDelayedResponse(ConnectingToServer, offset));
                            RetryConnect(TimeSpan.FromSeconds(Math.Max(1, (int)offset)));
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

            if (_shutdownRetryServer == shutdownServer && !_shutdownRetryFinished)
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

        private void BeginRestartRecovery(float restartDelay, bool extendedReconnectionPeriod)
        {
            Server restartingServer = Server;

            if (restartingServer == null || Connection == null)
                return;

            ServerSettings settings = restartingServer.Settings;

            _shutdownRetryServer = restartingServer;
            _shutdownRetryAttempts = Math.Max(0, settings?.RestartRetryAttempts ?? 0);
            _shutdownRetryAttemptsMade = 0;
            _shutdownRetryInterval = TimeSpan.FromSeconds(Math.Max(0.1f, settings?.RestartRetryInterval ?? 3f));

            // With a bridge attached we do not have to guess how long the restart takes: the
            // bridge holds the retry back until the game server reports it is accepting
            // connections again, so the initial wait only exists to avoid a pointless first
            // attempt. Without one, fall back to the delay the game server announced, and to
            // the old fixed ten seconds when it announced nothing useful.
            _recoveryInitialDelay = restartingServer.HasFreshBridgeRoundState
                ? 1f
                : (restartDelay > 0.5f ? Math.Min(restartDelay, 30f) : 10f);

            _recoveryServerBack = false;

            _nextShutdownRetry = DateTime.UtcNow.AddSeconds(_recoveryInitialDelay);
            _shutdownWaitingMessage = TranslationManager.For(this).Recovery.RestartWaiting;
            _shutdownUnreachableMessage = TranslationManager.For(this).Recovery.RestartUnreachable;
            _shutdownRetryFinished = false;

            ShowShutdownRetryStatus();

            // The restart message is on its way to the client, and the client answers it by
            // leaving. Without this the proxy reads that as "player quit" and tears the whole
            // slot down, so the player comes back as a stranger and gets routed by priority
            // instead of back to the server they were playing on.
            Connection.IsSwitchingServers = true;
            ReconnectDeadline = DateTime.UtcNow.AddSeconds(
                Math.Max(0f, restartDelay) + RestartReconnectGraceSeconds
            );
            _restartDropClientAt = DateTime.UtcNow.AddSeconds(RestartClientDropDelaySeconds);

            SiteLinkLogger.Info(
                $"{Connection.Tag} Server (f=yellow){restartingServer.Name}(f=white) is restarting; " +
                $"first reconnect in (f=yellow){_recoveryInitialDelay:0.##}(f=white) second(s), then " +
                $"(f=yellow){_shutdownRetryAttempts}(f=white) attempt(s) every " +
                $"(f=yellow){_shutdownRetryInterval.TotalSeconds:0.##}(f=white) second(s)."
            );

            DestroyNet();
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

            SiteLinkLogger.Info(
                $"{Connection.Tag} Server (f=yellow){_shutdownRetryServer.Name}(f=white) did not recover; " +
                $"no fallback servers configured."
            );

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
            _shutdownWaitingMessage = TranslationManager.For(this).Recovery.ShutdownWaiting;
            _shutdownUnreachableMessage = TranslationManager.For(this).Recovery.ShutdownUnreachable;
            _shutdownRetryFinished = false;
            _recoveryInitialDelay = (float)_shutdownRetryInterval.TotalSeconds;
            _recoveryServerBack = false;
        }

        private void ShowShutdownRetryStatus()
        {
            if (_shutdownRetryServer == null || _shutdownRetryFinished)
                return;

            _nextRecoveryHint = DateTime.UtcNow.Add(RecoveryHintInterval);

            string message = FormatShutdownRetryMessage(_shutdownWaitingMessage);

            if (string.IsNullOrEmpty(message))
                return;

            Connection?.AsServer.Hint(message, RecoveryHintDuration);
        }

        /// <summary>
        /// Keeps the recovery message on screen for the whole outage.
        /// <para>
        /// Without this the player saw one hint, watched it fade, and then stared at a frozen
        /// facility for the rest of the restart with no indication that anything was still
        /// happening. Re-sending on a fixed cadence also means the countdown in the message
        /// actually counts down.
        /// </para>
        /// </summary>
        private void UpdateRecoveryHint()
        {
            if (_shutdownRetryServer == null || _shutdownRetryFinished)
            {
                _nextRecoveryHint = DateTime.MaxValue;
                return;
            }

            if (_nextRecoveryHint > DateTime.UtcNow)
                return;

            ShowShutdownRetryStatus();
        }

        /// <summary>
        /// Closes the client's connection shortly after a restart was announced to it.
        /// <para>
        /// On a vanilla server the restart message and the transport shutdown arrive together,
        /// and it is the shutdown that actually sends the client to its reconnect screen -
        /// the message on its own only tells it how long to wait. Behind a proxy nothing ever
        /// closes that socket, so the client kept the connection, ignored its own countdown,
        /// and sat on a facility that was no longer being simulated. Dropping it here is the
        /// proxy standing in for the game server's socket, which is what makes the restart
        /// look like a restart.
        /// </para>
        /// </summary>
        private void UpdateRestartClientDrop()
        {
            if (_restartDropClientAt > DateTime.UtcNow)
                return;

            _restartDropClientAt = DateTime.MaxValue;

            RemoteConnection connection = Connection;

            if (connection == null)
                return;

            // If recovery already handed the client to a newer session, the player is back in
            // the facility and dropping them now would be a kick with extra steps.
            if (!ReferenceEquals(connection.Session, this))
                return;

            // Transport-level only. A disconnect reason would be rendered as an error over
            // the restart screen the client is about to show, and the client is expected
            // back: IsSwitchingServers keeps the session alive for it.
            connection.Disconnect();
        }

        private void UpdateShutdownRetry()
        {
            if (_shutdownRetryServer == null || _shutdownRetryFinished || Status != SessionStatus.Connected)
                return;

            // No client to reconnect on behalf of. This is the normal state for most of a
            // restart: the player is away watching the game's own restart screen, and the
            // preauth the retry needs went with them. Hold the recovery until they are back
            // rather than burning attempts - or dereferencing a connection that is gone.
            if (Connection == null)
                return;

            // The bridge is the only source that knows whether the game server is actually
            // down or merely swapping scenes, so let it drive the schedule when it is there.
            ApplyBridgeRecoverySignal();

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

            // The player never asked to change servers; they are being put back where they
            // already were. Marking the retry keeps FinalizeConnection from kicking them.
            retrySession.IsRecoveryRetry = true;

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

            // Seconds left until the next reconnect attempt. This is what makes the message
            // read as progress rather than as a stuck screen: a static "waiting..." is
            // indistinguishable from a crash from the player's side.
            double countdown = Math.Max(0d, (_nextShutdownRetry - DateTime.UtcNow).TotalSeconds);

            return TranslationManager.Format(
                    message,
                    TranslationContext.For(this, _shutdownRetryServer))
                .Add("server", _shutdownRetryServer?.DisplayName)
                .Add("server_name", _shutdownRetryServer?.Name)
                .Add("attempts", _shutdownRetryAttempts)
                .Add("attempt", Math.Min(_shutdownRetryAttemptsMade + 1, Math.Max(1, _shutdownRetryAttempts)))
                .Add("interval", _shutdownRetryInterval.TotalSeconds, "0.##")
                .Add("restart_delay", _recoveryInitialDelay, "0.##")
                .Add("countdown", countdown, "0")
                .Format();
        }

        /// <summary>
        /// Lets the bridge, rather than a fixed timer, decide when the game server is worth
        /// reconnecting to.
        /// <para>
        /// Without a bridge the proxy can only guess: it waits ten seconds, then retries on a
        /// fixed interval and burns its attempt budget against a server that is still loading
        /// a scene. The bridge reports both the round state and whether the transport is
        /// still delaying incoming connections, which is exactly the question the retry loop
        /// is trying to answer.
        /// </para>
        /// </summary>
        private void ApplyBridgeRecoverySignal()
        {
            Server server = _shutdownRetryServer;

            if (server == null || !server.HasFreshBridgeRoundState)
                return;

            if (server.IsBridgeBusyRestarting)
            {
                _recoveryServerBack = false;

                // Do not spend an attempt on a server that has told us it is not ready. Keep
                // the next check just past the point where the bridge would have to have
                // reported again for us to still trust it.
                DateTime hold = DateTime.UtcNow.Add(_shutdownRetryInterval);

                if (_nextShutdownRetry < hold)
                    _nextShutdownRetry = hold;

                return;
            }

            if (!server.BridgeAcceptingConnections)
            {
                _recoveryServerBack = false;

                // The transport is up but still delaying preauth. Connecting now earns a
                // rejection and a wasted attempt; wait out the delay the server declared.
                DateTime hold = DateTime.UtcNow.AddSeconds(Math.Max(1, server.BridgeConnectionDelaySeconds));

                if (_nextShutdownRetry < hold)
                    _nextShutdownRetry = hold;

                return;
            }

            if (_recoveryServerBack)
                return;

            // First tick where the bridge says the server is live and accepting. Stop waiting.
            _recoveryServerBack = true;
            _nextShutdownRetry = DateTime.UtcNow;

            SiteLinkLogger.Info(
                $"{Connection?.Tag} Bridge reports (f=yellow){server.Name}(f=white) is accepting connections again; reconnecting now."
            );
        }


        internal void ShowConnectionDelayedStatus(Server server, byte delay)
        {
            string message = TranslationManager.For(this).Connection.ConnectionDelayed;
            if (string.IsNullOrEmpty(message))
                return;

            Connection?.AsServer.Hint(
                TranslationManager.Format(message, TranslationContext.For(this, server))
                    .Add("server", server?.DisplayName)
                    .Add("server_name", server?.Name)
                    .Add("delay", delay)
                    .Format(),
                Math.Max(3f, delay + 0.5f)
            );
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
