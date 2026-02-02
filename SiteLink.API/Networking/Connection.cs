using SiteLink.API.Metrics;
using SiteLink.API.Threading;

namespace SiteLink.API.Networking
{
    [ThreadAffined("Listener")]
    public class Connection : IDisposable
    {
        public static Dictionary<string, Connection> ConnectionByUserId = new Dictionary<string, Connection>();

        public static bool TryGet(string userId, out Connection connection) => ConnectionByUserId.TryGetValue(userId, out connection);

        private static int ThresholdBytes => 65535 * (NetConstants.MaxPacketSize - 6);

        private readonly int _ownerThreadId;

        public ConnectionStats Stats { get; } = new ConnectionStats();

        private Session _session;

        public bool IsDisposed { get; private set; }
        
        public bool IsSwitchingServers { get; set; }

        /// <summary>
        /// Gets the tag used for logging and identification.
        /// </summary>
        public string Tag
        {
            get
            {
                string listenerTag = Listener != null ? Listener.Tag : "[(f=cyan)unknown-listener(f=white)]";

                string user = PreAuth.UserId;

                string serverTag = (Session?.Server != null) ? $" {Session.Server.Tag}" : string.Empty;

                return $"{listenerTag} [(f=green){user}(f=white)]{serverTag}";
            }
        }

        public Listener Listener { get; }

        public Session Session
        {
            get => _session;
            set
            {
                if (value != null && _session == null)
                    AcceptRequest();

                _session = value;
            }
        }

        /// <summary>
        /// Gets the pre-authentication information for this connection.
        /// </summary>
        public PreAuth PreAuth { get; private set; }

        public ConnectionRequest Request { get; private set; }

        /// <summary>
        /// Gets or sets the network peer for this connection.
        /// </summary>
        public NetPeer Peer { get; private set; }

        public MirrorSender AsServer { get; } // sends to client
        public MirrorSender AsClient { get; } // sends to server

        private double NowSeconds() => Session.SessionTime.TotalSeconds;

        public Connection(Listener listener, ConnectionRequest request, PreAuth preAuth)
        {
            Listener = listener;
            Request = request;

            PreAuth = preAuth;

            _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            ThreadOwner.Register(this, listener.Name, _ownerThreadId);

            ConnectionByUserId.Add(PreAuth.UserId, this);

            AsServer = new MirrorSender(
                ThresholdBytes,
                NowSeconds,
                (bytes, offset, length, method) =>
                {
                    // proxy -> client
                    SendToClient(bytes, offset, length, method);
                });

            AsClient = new MirrorSender(
                ThresholdBytes,
                NowSeconds,
                (bytes, offset, length, method) =>
                {
                    // proxy -> server
                    Session?.SendToServer(bytes, offset, length, method);
                });
        }

        /// <summary>
        /// Accepts the pending connection request for this client.
        /// </summary>
        public void AcceptRequest()
        {
            if (Request == null)
                return;

            Peer = Request.Accept();

            Listener.Connections.TryAdd(Peer.Id, this);

            Request = null;
        }

        public void SendToClient(byte[] bytes, int position, int length, DeliveryMethod method)
        {
            if (Peer == null)
                return;

            Stats.RecordBytesSent(length);
            Peer.Send(bytes, position, length, method);
        }

        /// <summary>
        /// Connects to a server by name.
        /// </summary>
        /// <param name="name">The server name.</param>
        public void Connect(string name)
        {
            ThreadOwner.Verify(this);
            ConnectInternal(name);
        }

        private void ConnectInternal(string name)
        {
            Server server = Server.Get<Server>(name: name);

            if (server == null)
            {
                Disconnect($"Server {name} not found.");
                return;
            }

            ConnectInternal(server);
        }

        /// <summary>
        /// Connects to a list of servers in order.
        /// </summary>
        /// <param name="servers">The server names.</param>
        public void Connect(string[] servers)
        {
            ThreadOwner.Verify(this);
            ConnectInternal(servers);
        }

        private void ConnectInternal(string[] servers)
        {
            SiteLinkLogger.Info($"{Tag} Connect to (f=yellow){string.Join("(f=white) -> (f=yellow)", servers)}(f=white)");

            Server[] serverObjs = Server.List.Where(x => servers.Contains(x.Name.ToLower())).ToArray();

            SessionManager.Singleton.CreateOrSwitchSession(this, serverObjs);
        }

        public void Connect(Server server)
        {
            ThreadOwner.Verify(this);
            ConnectInternal(server);
        }

        private void ConnectInternal(Server server)
        {
            SessionManager.Singleton.CreateOrSwitchSession(this, new[] { server });
        }

        /// <summary>
        /// Marshals an action to execute on this connection's owning thread (Listener polling thread).
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

        public void Update()
        {
            Session?.Update();

            AsServer?.Update();
            AsClient?.Update();
        }

        public void Disconnect(string message = null)
        {
            if (Request != null)
            {
                Request.RejectWithMessage(message);

                Dispose();

                SiteLinkLogger.Info($"{Tag} Disconnected{(string.IsNullOrEmpty(message) ? string.Empty : $" with reason '(f=yellow){message}(f=white)'")}");
                return;
            }

            if (message != null && Session != null)
            {
                AsServer.Send(wr =>
                {
                    int id = -2106075371;

                    wr.WriteUShort(NetworkMessages.RpcMessage);
                    wr.WriteUInt(Session.NetworkId);
                    wr.WriteByte(1);
                    wr.WriteUShort((ushort)id);

                    NetworkWriter wr2 = new NetworkWriter();
                    wr2.WriteString(message);

                    wr.WriteArraySegmentAndSize(wr2.ToArraySegment());
                });

                return;
            }

            Peer.Disconnect();
        }

        public void Dispose()
        {
            ThreadOwner.Verify(this);
            DisposeInternal();
        }

        private void DisposeInternal()
        {
            if (IsSwitchingServers)
                SessionManager.Singleton.DetachClient(PreAuth.UserId, "switching servers");
            else
                SessionManager.Singleton.DestroyAllForUser(PreAuth.UserId, "Client disconnected from proxy");

            ConnectionByUserId.Remove(PreAuth.UserId);

            // Clean up resources here
            IsDisposed = true;
        }
    }
}
