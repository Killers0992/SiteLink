using SiteLink.API.Metrics;
using SiteLink.API.Threading;
using System.Collections.Generic;
using System.Linq;

namespace SiteLink.API.Networking
{
    [ThreadAffined("Listener")]
    public class Connection : IDisposable
    {
        public static Dictionary<string, Connection> ConnectionByUserId = new Dictionary<string, Connection>();

        public static bool TryGet(string userId, out Connection connection) => ConnectionByUserId.TryGetValue(userId, out connection);

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

                return $"{listenerTag} [(f=green){user}(f=white)]";
            }
        }

        public Listener Listener { get; }

        public Session Session
        {
            get => _session;
            set
            {
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

        private double NowSeconds() => Session == null ? 0 : Session.SessionTime.TotalSeconds;

        public Connection(Listener listener, ConnectionRequest request, PreAuth preAuth)
        {
            Listener = listener;
            Request = request;

            PreAuth = preAuth;

            // Use the Listener's thread ID to ensure all operations happen on the listener's thread
            _ownerThreadId = listener.OwnerThreadId;

            ThreadOwner.Register(this, listener.Name + ":" + preAuth.UserId, _ownerThreadId);

            ConnectionByUserId.Add(PreAuth.UserId, this);

            AsServer = new MirrorSender(
                SiteLinkAPI.ThresholdBytes,
                NowSeconds,
                (bytes, offset, length, method) =>
                {
                    // proxy -> client
                    SendToClient(bytes, offset, length, method);
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
        public bool Connect(string name, bool silent = false)
        {
            //ThreadOwner.Verify(this);
            return ConnectInternal(name, silent);
        }

        private bool ConnectInternal(string name, bool silent)
        {
            Server server = Server.Get<Server>(name: name);

            if (server == null)
            {
                Disconnect($"Server {name} not found.");
                return false;
            }

            return ConnectInternal(server, silent);
        }

        /// <summary>
        /// Connects to a list of servers in order.
        /// </summary>
        /// <param name="servers">The server names.</param>
        public bool Connect(string[] servers, bool silent = false)
        {
            //ThreadOwner.Verify(this);
            return ConnectInternal(servers, silent);
        }

        private bool ConnectInternal(string[] servers, bool silent)
        {
            SiteLinkLogger.Info($"{Tag} Connect to (f=yellow){string.Join("(f=white) -> (f=yellow)", servers)}(f=white)");

            var serverNames = new HashSet<string>(servers.Select(s => s.ToLower()), StringComparer.OrdinalIgnoreCase);
            var serverObjs = new List<Server>(servers.Length);

            foreach (var server in Server.List)
            {
                if (serverNames.Contains(server.Name.ToLower()))
                {
                    serverObjs.Add(server);
                }
            }

            return SessionManager.Singleton.CreateOrSwitchSession(this, serverObjs.ToArray(), silent) != null;
        }

        public bool Connect(Server server, bool silent = false)
        {
            //ThreadOwner.Verify(this);
            return ConnectInternal(server, silent);
        }

        private bool ConnectInternal(Server server, bool silent)
        {
            return SessionManager.Singleton.CreateOrSwitchSession(this, new[] { server }, silent) != null;
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

                    NetworkWriter messageWriter = new NetworkWriter();
                    messageWriter.WriteString(message);

                    wr.WriteArraySegmentAndSize(messageWriter.ToArraySegment());
                });

                return;
            }

            Peer.Disconnect();
        }

        public void Dispose()
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
