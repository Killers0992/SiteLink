using Org.BouncyCastle.Asn1.Cmp;
using SiteLink.Core;

namespace SiteLink.API.Networking
{
    public enum SessionStatus
    {
        PreAuthentication,
        Connected,
    }

    public class Session
    {
        public ChallengeHandler Challenge { get; private set; }

        private NetManager _netManager;
        private EventBasedNetListener _listener;

        private Client Client;

        private Queue<Server> ConnectToServers;

        private Server ConnectingToServer;

        public Server Server { get; private set; }

        public SessionStatus Status { get; set; } = SessionStatus.PreAuthentication;

        public DateTime AliveUntil { get; set; }

        public Action ClientIsBanned;

        public Session(Client client, Server[] servers)
        {
            Client = client;

            ConnectToServers = new Queue<Server>(servers);

            Challenge = new ChallengeHandler(this);

            _listener = new EventBasedNetListener();

            _listener.PeerConnectedEvent += OnConnected;
            _listener.NetworkReceiveEvent += OnReceiveData;
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

            SiteLinkLogger.Info(servers.Length > 1
                ? $"{Client.Tag} Connecting to one of {servers.Length} servers..."
                : $"{Client.Tag} Connecting to server {servers[0].Tag}...");
        }

        public void ConnectWithChallenge(int challengeId, byte[] challengeResponse)
        {
            _netManager.Connect(ConnectingToServer.IpAddress, ConnectingToServer.Port, Client.PreAuth.Create(ConnectingToServer.ForwardIpAddress, challengeId, challengeResponse));
        }

        public void Update()
        {
            if (_netManager != null)
                _netManager.PollEvents();

            if (ConnectingToServer == null && ConnectToServers.Count > 0)
            {
                ConnectingToServer = ConnectToServers.Dequeue();
                
                _netManager.Connect(ConnectingToServer.IpAddress, ConnectingToServer.Port, Client.PreAuth.Create(ConnectingToServer.ForwardIpAddress));
                SiteLinkLogger.Info("Connecting to next " + ConnectingToServer.IpAddress + ":" + ConnectingToServer.Port);
            }
        }

        public void Send(byte[] data, int offset, int length, DeliveryMethod method)
        {
            _netManager.FirstPeer.Send(data, offset, length, method);
        }

        private void OnConnected(NetPeer peer)
        {
            Status = SessionStatus.Connected;
            Server = ConnectingToServer;

            SiteLinkLogger.Info($"{Client.Tag} {Server.Tag} Connected to server!");

            if (Client.Session == null)
            {
                Client.SendToScene("Facility");
                Client.Session = this;
            }
            else
            {
                Client.ReconnectToProxy();
            }
        }

        private void OnDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            switch (disconnectInfo.Reason)
            {
                default:
                    SiteLinkLogger.Info($"{Client.Tag} Disconnect undefined {disconnectInfo.Reason}");
                    break;

                case DisconnectReason.ConnectionFailed when disconnectInfo.AdditionalData.RawData == null:
                    if (ConnectToServers.Count == 0)
                    {
                        SiteLinkLogger.Info($"{Client.Tag} Connection to server {Server.IpAddress}:{Server.Port} failed, no more servers to try.");

                        Client.OnDisconnectedFromServerInternal(Server, new ConnectionFailedInfo($"Failed to connect to server {Server.IpAddress}:{Server.Port}!", DisconnectType.BadSignature));
                        return;
                    }

                    SiteLinkLogger.Info($"{Client.Tag} Connection to server {Server.IpAddress}:{Server.Port} failed, trying next server...");

                    // Set connecting to server as null so we can try the next one in the update loop
                    ConnectingToServer = null;
                    return;

                case DisconnectReason.ConnectionRejected when disconnectInfo.AdditionalData.RawData != null:
                    NetDataWriter rejectedData = NetDataWriter.FromBytes(disconnectInfo.AdditionalData.RawData, disconnectInfo.AdditionalData.UserDataOffset, disconnectInfo.AdditionalData.UserDataSize);

                    if (!disconnectInfo.AdditionalData.TryGetByte(out byte lastRejectionReason))
                        break;

                    RejectionReason reason = (RejectionReason)lastRejectionReason;

                    switch (reason)
                    {
                        case RejectionReason.RateLimit:
                            Client.AddToReconnectAttempt(TimeSpan.FromSeconds(4));
                            break;

                        case RejectionReason.Delay:
                            if (!disconnectInfo.AdditionalData.TryGetByte(out byte offset))
                                break;

                            break;

                        case RejectionReason.ServerFull:
                            if (ConnectToServers.Count == 0)
                            {
                                Client.OnDisconnectedFromServerInternal(Server, new ConnectionFailedInfo($"Server {Server.IpAddress}:{Server.Port} is full!", DisconnectType.ServerIsFull));
                                return;
                            }

                            // Set connecting to server as null so we can try the next one in the update loop
                            ConnectingToServer = null;
                            break;

                        case RejectionReason.Banned:
                            long expireTime = disconnectInfo.AdditionalData.GetLong();
                            string banReason = disconnectInfo.AdditionalData.GetString();

                            DateTime date = new DateTime(expireTime, DateTimeKind.Utc).ToLocalTime();

                            if (ConnectToServers.Count == 0)
                            {
                                Client.OnDisconnectedFromServerInternal(Server, new ConnectionFailedInfo($"Banned for reason {banReason} on {Server.IpAddress}:{Server.Port}!", DisconnectType.ServerIsFull));
                                return;
                            }

                            ConnectingToServer = null;
                            break;

                        case RejectionReason.Challenge:
                            Challenge.ProcessChallenge(disconnectInfo.AdditionalData);
                            break;

                        default:

                            break;
                    }

                    return;
            }
        }

        private void OnReceiveData(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (Client.Session != this)
                return;

            byte[] bytes = reader.RawData;
            int pos = reader.Position;
            int length = reader.AvailableBytes;

            Client.SendData(bytes, pos, length, deliveryMethod);
        }
    }
}