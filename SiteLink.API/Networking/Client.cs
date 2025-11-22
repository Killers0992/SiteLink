using Hints;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using RoundRestarting;
using SiteLink.API.Events;
using SiteLink.API.Events.Args;
using SiteLink.Core;
using SiteLink.Networking.Batchers;
using UserSettings.ServerSpecific;
using static PlayerStatsSystem.SyncedStatMessages;

namespace SiteLink.API.Networking;

/// <summary>
/// Represents a networked client, managing connection, world state, and communication with the server.
/// </summary>
public class Client : IDisposable
{
    public static int TotalPlayersCount => Listener.ClientByUserId.Count;

    public static bool TryGet(string userId, out Client client) => Listener.ClientByUserId.TryGetValue(userId, out client);
    /// <summary>
    /// Minimal vertical angle.
    /// </summary>
    public const float MinimumVer = -88f;

    /// <summary>
    /// Max vertical angle.
    /// </summary>
    public const float MaximumVer = 88f;

    private const float FullAngle = 360;

    /// <summary>
    /// The inverse accuracy constant for position calculations.
    /// </summary>
    public const float InverseAccuracy = 0.00390625f;

    /// <summary>
    /// The default name for a client when the name is unknown.
    /// </summary>
    public const string UnknownName = "(unknown name)";

    int _reconnectAttempt = 0;
    private World _world;
    private Connection _connection = new Connection();
    private ActionScheduler _scheduler;

    /// <summary>
    /// Gets the network identity ID of this client.
    /// </summary>
    public uint NetworkIdentityId { get; private set; }

    /// <summary>
    /// Gets the display name of the client, or <see cref="UnknownName"/> if not set.
    /// </summary>
    public string Name => Object != null ? Object.NicknameSync.MyNickSync : UnknownName;

    /// <summary>
    /// Gets a value indicating whether the client is ready.
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// Gets the server this client is connected to.
    /// </summary>
    public Server Server => Connection.Server;

    public ActionScheduler Scheduler => _scheduler ??= new ActionScheduler(this);

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
                    Object = null;

                _world.Unload(this, value);
            }
            else
            {
                Object = null;
            }

            _world = value;

            if (value != null)
                value.Load(this);
        }
    }

    /// <summary>
    /// Gets or sets the connection request for this client.
    /// </summary>
    public ConnectionRequest Request { get; set; }

    /// <summary>
    /// Gets the listener associated with this client.
    /// </summary>
    public Listener Listener { get; }

    /// <summary>
    /// Gets or sets the network peer for this client.
    /// </summary>
    public NetPeer Peer { get; set; } = null;

    /// <summary>
    /// Gets the pre-authentication information for this client.
    /// </summary>
    public PreAuth PreAuth { get; }

    /// <summary>
    /// Gets the remote timestamp from the listener.
    /// </summary>
    public double ListenerRemoteTimestamp { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the client is being disposed.
    /// </summary>
    public bool IsDisposing { get; private set; }

    /// <summary>
    /// Gets or sets the last response received by this client.
    /// </summary>
    public IDisconnectResponse LastResponse { get; set; }

    /// <summary>
    /// Gets the waypoint ID associated with this client.
    /// </summary>
    public byte WaypointId { get; private set; }

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

    /// <summary>
    /// Current horizontal rotation.
    /// </summary>
    public float HorizontalRotation { get; private set; }

    /// <summary>
    /// Current vertical rotation.
    /// </summary>
    public float VerticalRotation { get; private set; }

    /// <summary>
    /// Gets or sets the main connection for this client.
    /// </summary>
    public Connection Connection
    {
        get => _connection;
        set
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection.IsMain = false;
                BackupConnection = _connection;
            }

            if (value != null)
            {
                World = null;
                RelativePosition = Vector3.zero;
                WaypointId = 0;

                IsSpawned = false;
                IsReady = false;
                value.IsMain = true;
            }

            _connection = value;

            OnConnectedToServerInternal(Server);
        }
    }

    /// <summary>
    /// Gets the backup connection for this client.
    /// </summary>
    public Connection BackupConnection { get; private set; } = new Connection();

    /// <summary>
    /// Gets the unbatcher for the current server.
    /// </summary>
    public CustomUnbatcher UnbatcherCurrentServer { get; private set; } = new CustomUnbatcher();

    /// <summary>
    /// Gets the unbatcher for the listener.
    /// </summary>
    public CustomUnbatcher UnbatcherListener { get; private set; } = new CustomUnbatcher();

    /// <summary>
    /// Gets the batcher used for sending network messages.
    /// </summary>
    public CustomBatcher Batcher { get; private set; } = new CustomBatcher(65535 * (NetConstants.MaxPacketSize - 6));

    public CustomBatcher BatcherCurrentServer { get; private set; } = new CustomBatcher(65535 * (NetConstants.MaxPacketSize - 6));

    /// <summary>
    /// Gets or sets a value indicating whether the client is reconnecting.
    /// </summary>
    public bool IsReconnecting;

    /// <summary>
    /// Gets or sets the server name to reconnect to.
    /// </summary>
    public string ReconnectTo;

    /// <summary>
    /// The queue of server names to connect to.
    /// </summary>
    public Queue<Server> ServersToConnect = new Queue<Server>();

    public bool SilentConnection;

    /// <summary>
    /// Gets or sets the time of the next reconnect attempt.
    /// </summary>
    public DateTime ReconnectAttempt = DateTime.Now;

    /// <summary>
    /// Gets the time the client connected.
    /// </summary>
    public DateTime ConnectedOn { get; } = DateTime.Now;

    /// <summary>
    /// Gets the duration of the current connection.
    /// </summary>
    public TimeSpan Connectiontime => DateTime.Now - ConnectedOn;

    /// <summary>
    /// Gets the tag used for logging and identification.
    /// </summary>
    public string Tag => $"{Listener.Tag} [(f=green){PreAuth.UserId}(f=white)]{(Server == null ? string.Empty : $" {Server.Tag}")}";

    public int Seed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Client"/> class.
    /// </summary>
    /// <param name="listener">The listener associated with this client.</param>
    /// <param name="request">The connection request.</param>
    /// <param name="preAuth">The pre-authentication information.</param>
    public Client(Listener listener, ConnectionRequest request, PreAuth preAuth)
    {
        Listener = listener;
        Request = request;
        PreAuth = preAuth;

        Listener.RegisterClientInLookup(this);

        Listener.NotConnectedClients.Add(this);
    }

    public void AddToActions(TimedAction action) => Scheduler.Add(action);

    public void RemoveAction(string name) => Scheduler.Remove(name);

    /// <summary>
    /// Called when the client is connected to a server.
    /// </summary>
    /// <param name="Server">The server the client connected to.</param>
    public virtual void OnConnectedToServer(Server Server)
    {

    }

    /// <summary>
    /// Called when the client is disconnected from a server.
    /// </summary>
    /// <param name="Server">The server the client was connected to.</param>
    /// <param name="info">Information about the disconnection.</param>
    /// <returns>True if the disconnection should proceed; otherwise, false.</returns>
    public virtual bool OnDisconnectedFromServer(Server Server, ConnectionFailedInfo info)
    {
        switch (info.Response)
        {
            case DisconnectType.ServerIsFull:
                TakeServerAndTryConnect();
                return false;
        }

        return true;
    }

    /// <summary>
    /// Internal handler for when the client is connected to a server.
    /// </summary>
    /// <param name="server">The server.</param>
    public void OnConnectedToServerInternal(Server server)
    {
        OnConnectedToServer(server);
        SiteLinkLogger.Info($"{Tag} Connected.");
    }

    /// <summary>
    /// Internal handler for when the client is disconnected from a server.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="info">The disconnection info.</param>
    public void OnDisconnectedFromServerInternal(Server server, ConnectionFailedInfo info)
    {
        bool canRun = OnDisconnectedFromServer(server, info);

        if (canRun)
            Disconnect(info.Message);
    }

    /// <summary>
    /// Accepts the pending connection request for this client.
    /// </summary>
    public void AcceptConnection()
    {
        _reconnectAttempt = 0;

        if (Request == null)
            return;

        Peer = Request.Accept();
        Listener.ConnectedClients.Add(Peer.Id, this);

        Request = null;
    }

    /// <summary>
    /// Initiates a reconnect to the specified server after an optional delay.
    /// </summary>
    /// <param name="serverName">The name of the server to reconnect to.</param>
    /// <param name="timeOffset">The delay before reconnecting, in seconds.</param>
    /// <param name="message">An optional message for logging.</param>
    public bool Reconnect(string serverName, float timeOffset = 0f, string message = null)
    {
        if (_reconnectAttempt >= SiteLinkSettings.Singleton.MaximumReconnectAttempts)
        {
            SiteLinkLogger.Info($"{Tag} Reached max attemps -> disconnect ");
            //Connect(Server.Settings.FallbackServers);
            LastResponse = null;
            return false;
        }

        AddToReconnectAttempt(TimeSpan.FromSeconds(timeOffset));

        ReconnectTo = serverName;
        IsReconnecting = true;
        _reconnectAttempt++;
        return true;
    }

    private NetworkWriter batchWriter = new NetworkWriter();

    /// <summary>
    /// Polls for network events and processes outgoing batches.
    /// </summary>
    public void PollEvents()
    {
        Scheduler?.Update();

        if (IsReconnecting)
        {
            if (ReconnectAttempt < DateTime.Now)
            {
                SiteLinkLogger.Info("Reconnect to " + ReconnectTo);
                Connect(ReconnectTo);
                IsReconnecting = false;
            }
        }

        Connection.Update();
        BackupConnection.Update();

        while (Batcher.GetBatch(batchWriter))
        {
            ArraySegment<byte> segment = batchWriter.ToArraySegment();
            SendData(segment.Array, segment.Offset, segment.Count, DeliveryMethod.ReliableOrdered);
            batchWriter.Position = 0;
        }

        while (BatcherCurrentServer.GetBatch(batchWriter))
        {
            ArraySegment<byte> segment = batchWriter.ToArraySegment();
            Connection.Send(segment.Array, segment.Offset, segment.Count, DeliveryMethod.ReliableOrdered);
            batchWriter.Position = 0;
        }
    }

    /// <summary>
    /// Processes Mirror data received from the server.
    /// </summary>
    /// <param name="bytes">The data buffer.</param>
    /// <param name="position">The start position in the buffer.</param>
    /// <param name="length">The length of the data.</param>
    /// <returns>True if all messages were processed successfully; otherwise, false.</returns>
    public bool ProcessMirrorDataFromServer(ref byte[] bytes, ref int position, ref int length)
    {
        ArraySegment<byte> segment = new ArraySegment<byte>(bytes, position, length);

        NetworkReader reader = new NetworkReader(segment);

        double timeStamp = reader.ReadDouble();

        bool end = false;
        int totalReads = 0;

        List<(int, int)> rangesToRemove = new List<(int, int)>();

        bool result = true;

        while (reader.Remaining != 0 && !end)
        {
            int positionBeforeRead = reader.Position;
            int size = (int)Compression.DecompressVarUInt(reader);

            if (reader.Remaining < size)
            {
                end = true;
                continue;
            }

            ArraySegment<byte> message = reader.ReadBytesSegment(size);

            int positionAfterRead = reader.Position;

            NetworkReader reader2 = new NetworkReader(message);

            if (Mirror.NetworkMessages.UnpackId(reader2, out ushort messageId))
            {
                if (!ProcessMirrorMessageFromServer(messageId, reader2))
                {
                    result = false;
                    //rangesToRemove.Add((positionBeforeRead + 1, positionAfterRead));
                }
            }
            totalReads++;
        }

        RemoveByteRanges(ref bytes, rangesToRemove);

        return result;
    }

    /// <summary>
    /// Removes specified byte ranges from the input buffer.
    /// </summary>
    /// <param name="input">The input buffer.</param>
    /// <param name="ranges">The ranges to remove.</param>
    static void RemoveByteRanges(ref byte[] input, List<(int start, int end)> ranges)
    {
        List<byte> result = new List<byte>();
        int currentIndex = 0;

        ranges.Sort((a, b) => a.start.CompareTo(b.start));

        foreach (var (start, end) in ranges)
        {
            if (currentIndex < start)
            {
                result.AddRange(input.Skip(currentIndex).Take(start - currentIndex));
            }
            currentIndex = Math.Max(currentIndex, end + 1);
        }

        if (currentIndex < input.Length)
        {
            result.AddRange(input.Skip(currentIndex));
        }

        input = result.ToArray();
    }

    /// <summary>
    /// Processes Mirror data received from the listener.
    /// </summary>
    /// <param name="bytes">The data buffer.</param>
    /// <param name="position">The start position in the buffer.</param>
    /// <param name="length">The length of the data.</param>
    /// <returns>True if all messages were processed successfully; otherwise, false.</returns>
    public bool ProcessMirrorDataFromListener(ref byte[] bytes, ref int position, ref int length)
    {
        ArraySegment<byte> segment = new ArraySegment<byte>(bytes, position, length);

        NetworkReader reader = new NetworkReader(segment);

        double timeStamp = reader.ReadDouble();

        bool end = false;
        bool canceled = false;

        while (reader.Remaining != 0 && !end)
        {
            int size = (int)Compression.DecompressVarUInt(reader);

            if (reader.Remaining < size)
            {
                end = true;
                continue;
            }

            ArraySegment<byte> message = reader.ReadBytesSegment(size);

            NetworkReader reader2 = new NetworkReader(message);

            if (Mirror.NetworkMessages.UnpackId(reader2, out ushort messageId))
            {
                if (!ProcessMirrorMessageFromListener(messageId, reader2))
                    canceled = true;
            }
        }

        return !canceled;
    }

    /// <summary>
    /// Processes a Mirror message received from the listener.
    /// </summary>
    /// <param name="id">The message ID.</param>
    /// <param name="reader">The network reader.</param>
    /// <returns>True if the message should not be canceled; otherwise, false.</returns>
    public bool ProcessMirrorMessageFromListener(ushort id, NetworkReader reader)
    {
        switch (id)
        {
            case NetworkMessages.FpcFromClientMessage:
                if (!IsReady)
                {
                    SiteLinkLogger.Info("Dont sent position to SERVER becuase CLIENT IS NOT READY");
                    return false;
                }

                byte code = reader.ReadByte();

                bool _bitMouseLook = false;
                bool _bitPosition = false;
                bool _bitCustom = false;

                ushort _rotH, _rotV;

                global::Misc.ByteToBools(code, out bool b1, out bool b2, out bool b3, out bool b4, out bool b5, out _bitMouseLook, out _bitPosition, out _bitCustom);

                PlayerMovementState _state = (PlayerMovementState)global::Misc.BoolsToByte(b1, b2, b3, b4, b5);

                if (_bitPosition)
                {
                    byte waypointId = reader.ReadByte();


                    short PositionX, PositionY, PositionZ;
                    if (waypointId > 0)
                    {
                        PositionX = reader.ReadShort();
                        PositionY = reader.ReadShort();
                        PositionZ = reader.ReadShort();

                        WaypointId = waypointId;
                        RelativePosition = new Vector3(PositionX * InverseAccuracy, PositionY * InverseAccuracy, PositionZ * InverseAccuracy);
                    }
                    else
                    {
                        PositionX = 0;
                        PositionY = 0;
                        PositionZ = 0;
                    }
                }

                if (_bitMouseLook)
                {
                    _rotH = reader.ReadUShort();
                    _rotV = reader.ReadUShort();
                }
                else
                {
                    _rotH = 0;
                    _rotV = 0;
                }
                
                HorizontalRotation = Mathf.Lerp(0, FullAngle, _rotH / (float)ushort.MaxValue);
                VerticalRotation = Mathf.Lerp(MinimumVer, MaximumVer, _rotV / (float)ushort.MaxValue);
                break;

            case NetworkMessages.SSSClientResponse:
                SSSClientResponse response = new SSSClientResponse(reader);
                Server?.OnClientSSSReponse(this, response.Id);
                break;

            case NetworkMessages.ReadyMessage:

                if (Seed != -1) SendSeed(Seed);

                AddToActions(new TimedAction("FinalizeReady", TimeSpan.FromSeconds(1.5), p =>
                {
                    NetworkWriter wr = new NetworkWriter();
                    wr.WriteUShort(NetworkMessages.ReadyMessage);
                    p.SendMirrorDataToCurrentServer(wr);

                    Server?.OnClientReady(this);
                    IsReady = true;

                    wr = new NetworkWriter();
                    wr.WriteUShort(NetworkMessages.AddPlayerMessage);
                    p.SendMirrorDataToCurrentServer(wr);

                    Server?.OnClientSpawnPlayer(this);

                    SiteLinkLogger.Info($"{Tag} Set as ready");
                }));
                return false;

            case NetworkMessages.AddPlayerMessage:
                return false;

            case NetworkMessages.CommandMessage:
                CommandMessage commandMessage = new CommandMessage
                {
                    netId = reader.ReadUInt(),
                    componentIndex = reader.ReadByte(),
                    functionHash = reader.ReadUShort(),
                    payload = reader.ReadArraySegmentAndSize()
                };

                if (World != null && World.Objects.TryGetValue(commandMessage.netId, out NetworkObject spawnedObject))
                {
                    spawnedObject.OnReceiveCommand(commandMessage.componentIndex, commandMessage.functionHash, commandMessage.payload);
                }
                else if (commandMessage.netId == NetworkIdentityId && Object != null)
                {
                    Object.OnReceiveCommand(commandMessage.componentIndex, commandMessage.functionHash, commandMessage.payload);                    
                }   
                break;
        }

        return true;
    }

    /// <summary>
    /// The configuration synchronization object for this client.
    /// </summary>
    public ConfigSynchronizerObject ConfigSync;

    /// <summary>
    /// Gets or sets a value indicating whether the client is spawned.
    /// </summary>
    public bool IsSpawned;

    /// <summary>
    /// Processes a Mirror message received from the server.
    /// </summary>
    /// <param name="id">The message ID.</param>
    /// <param name="reader">The network reader.</param>
    /// <returns>True if the message should not be canceled; otherwise, false.</returns>
    public bool ProcessMirrorMessageFromServer(ushort id, NetworkReader reader)
    {
        // Everything here is send by remote server like dedicated server to client.
        // Its possible to intercept and modify these messages before they reach the client.

        switch (id)
        {
            case NetworkMessages.SeedMessage:
                int seed = reader.ReadInt();
                Seed = seed;
                return false;

            case NetworkMessages.SceneMessage:
                return false;

            case NetworkMessages.ServerShutdownMessage:
                return false;

            case NetworkMessages.RoundRestartMessage:
                if (!Connection.IsConnected)
                    return false;

                RoundRestartMessage restartMessage = RoundRestartMessageReaderWriter.ReadRoundRestartMessage(reader);

                SiteLinkLogger.Info("Set last response to RoundRestart");

                LastResponse = new RoundRestartResponse(restartMessage.Type, restartMessage.TimeOffset);
                Connection.Disconnect();
                return false;

            case NetworkMessages.StatMessage:
                if (!Connection.IsConnected)
                    return false;
                break;

            case NetworkMessages.RoleSyncInfo:
                uint entityId = reader.ReadUInt();

                if (NetworkIdentityId == entityId && !IsSpawned)
                {
                    Server?.OnClientSpawned(this);
                    IsSpawned = true;
                }
                break;

            case NetworkMessages.RpcMessage:
                RpcMessage rpcMessage = new RpcMessage()
                {
                    netId = reader.ReadUInt(),
                    componentIndex = reader.ReadByte(),
                    functionHash = reader.ReadUShort(),
                    payload = reader.ReadArraySegmentAndSize(),
                };

                if (rpcMessage.netId == NetworkIdentityId && Object != null)
                {
                    if (rpcMessage.componentIndex != 1)
                        break;

                    switch (rpcMessage.functionHash)
                    {
                        // System.Void CharacterClassManager::TargetSetDisconnectError(Mirror.NetworkConnection,System.String)
                        case 55061:
                            NetworkReader rpcReader = new NetworkReader(rpcMessage.payload);
                            string reason = rpcReader.ReadString();

                            Connection.Disconnect();

                            AddToActions(
                                new TimedAction(
                                    "ReconnectSequence",
                                    TimeSpan.Zero,
                                    p => p.SendHint($"Kicked from server {Server.Name} with reason\n\n'{reason}'\n\nConnecting to fallback server...", 6),
                                    null,
                                    new TimedAction("ConnectToServer", TimeSpan.FromSeconds(6), p =>
                                    {
                                        if (Server.Settings.FallbackServers.Length == 0)
                                        {
                                            p.Disconnect($"Kicked from server {Server.Name} with reason\n\n'{reason}'\n\nTheres no fallback servers set for '{Server.Name}'");
                                            return;
                                        }

                                        p.Connect(Server.Settings.FallbackServers);
                                    }
                                )
                            ));
                            return false;
                    }
                }
                break;

            case NetworkMessages.SpawnMessage:
                SpawnMessage spawnMessage = new SpawnMessage
                {
                    netId = reader.ReadUInt(),
                    isLocalPlayer = reader.ReadBool(),
                    isOwner = reader.ReadBool(),
                    sceneId = reader.ReadULong(),
                    assetId = reader.ReadUInt(),
                    position = reader.ReadVector3(),
                    rotation = reader.ReadQuaternion(),
                    scale = reader.ReadVector3(),
                    payload = reader.ReadArraySegmentAndSize()
                };

                switch (spawnMessage.assetId)
                {
                    // Player
                    case PlayerObject.ObjectAssetId:
                        if (spawnMessage.isLocalPlayer && spawnMessage.isOwner)
                        {
                            Object = new PlayerObject(World, this, spawnMessage.netId);
                            NetworkIdentityId = spawnMessage.netId;
                        }
                        break;
                    // ConfigSync
                    case ConfigSynchronizerObject.ObjectAssetId:
                        if (spawnMessage.sceneId != ConfigSynchronizerObject.ObjectSceneId)
                            break;

                        ConfigSync = new ConfigSynchronizerObject(null);
                        if (spawnMessage.payload.Count > 0)
                            ConfigSync.Deserialize(new NetworkReader(spawnMessage.payload), true);

                        break;
                }
                break;
        }

        return true;
    }

    /// <summary>
    /// Attempts to connect to the next server in the queue.
    /// </summary>
    public void TakeServerAndTryConnect()
    {
        if (!ServersToConnect.TryDequeue(out Server server))
        {
            Disconnect("Failed to connect to any priority servers");
            return;
        }

        Connect(server);
    }

    /// <summary>
    /// Connects to a server by name.
    /// </summary>
    /// <param name="name">The server name.</param>
    public bool Connect(string name)
    {
        Server server = Server.Get<Server>(name: name);

        if (server == null)
        {
            Disconnect($"Server {name} not found.");
            return false;
        }

        return Connect(server);
    }

    /// <summary>
    /// Connects to a list of servers in order.
    /// </summary>
    /// <param name="servers">The server names.</param>
    public void Connect(string[] servers)
    {
        ServersToConnect = new Queue<Server>(Server.List.Where(x => servers.Contains(x.Name.ToLower())));

        SiteLinkLogger.Info($"{Tag} Connect to (f=yellow){string.Join("(f=white) -> (f=yellow)", servers)}(f=white)");

        TakeServerAndTryConnect();
    }

    /// <summary>
    /// Connects to a specific server instance.
    /// </summary>
    /// <typeparam name="TServer">The server type.</typeparam>
    /// <param name="server">The server instance.</param>
    public bool Connect<TServer>(TServer server, bool isSilent = false) where TServer : Server
    {
        if (server == null)
        {
            Disconnect("Server not found.");
            return false;
        }

        if (Connection.IsConnected && server == Server)
        {
            if (!isSilent)
                SendHint("<color=red>You are already connected to this server!</color>");
            return false;
        }

        if (ReconnectAttempt > DateTime.Now)
        {
            if (!isSilent)
                SendHint($"Try again in {(DateTime.Now - ReconnectAttempt).TotalSeconds.ToString("ss':'fff")}", 3f);

            return false;
        }

        if (Object != null && !isSilent)
            SendHint($"Connecting...", 3f);

        BackupConnection.IsSilent = isSilent;
        BackupConnection.Setup(this);

        return BackupConnection.TryMakeConnection(server, PreAuth.Create(server.ForwardIpAddress));
    }

    public void AddToReconnectAttempt(TimeSpan time)
    {
        ReconnectAttempt = ReconnectAttempt.Add(time);
    }

    /// <summary>
    /// Sends data to the peer using the specified delivery method.
    /// </summary>
    /// <param name="bytes">The data buffer.</param>
    /// <param name="position">The start position in the buffer.</param>
    /// <param name="length">The length of the data.</param>
    /// <param name="method">The delivery method.</param>
    public void SendData(byte[] bytes, int position, int length, DeliveryMethod method)
    {
        if (Peer == null)
            return;

        Peer.Send(bytes, position, length, method);
    }

    /// <summary>
    /// Sends a Mirror data message to the client.
    /// </summary>
    /// <param name="writer">The network writer containing the message.</param>
    public void SendMirrorData(NetworkWriter writer)
    {
        if (Batcher == null)
            return;

        Batcher.AddMessage(writer.ToArraySegment(), Connectiontime.TotalSeconds);

        writer = null;
    }

    public void SendMirrorDataToCurrentServer(NetworkWriter writer)
    {
        if (Batcher == null)
            return;

        BatcherCurrentServer.AddMessage(writer.ToArraySegment(), Connectiontime.TotalSeconds);

        writer = null;
    }

    /// <summary>
    /// Sends a Mirror data message of the specified type to the client.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    public void SendMirrorData<TMessage>() where TMessage : struct, NetworkMessage
    {
        NetworkWriter writer = new NetworkWriter();
        writer.WriteUShort(NetworkMessageId<TMessage>.Id);
        SendMirrorData(writer);
    }

    private int _entriesVersion;

    /// <summary>
    /// Sends server-specific configuration entries to the client.
    /// </summary>
    /// <param name="entires">The configuration entries.</param>
    public void SendServerSpecificEntries(ServerSpecificSettingBase[] entires)
    {
        SSSEntriesPack packed = new SSSEntriesPack(entires, _entriesVersion++);

        NetworkWriter wr = new NetworkWriter();

        wr.WriteUShort(NetworkMessages.SSSEntriesPack);
        packed.Serialize(wr);

        SendMirrorData(wr);
    }

    /// <summary>
    /// Sends a hint message to the client.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="duration">The duration to display the message, in seconds.</param>
    public void SendHint(string message, float duration = 3)
    {
        NetworkWriter wr = new NetworkWriter();

        wr.WriteUShort(NetworkMessageId<HintMessage>.Id);

        // TextHint
        wr.WriteByte(1);

        // Duration
        wr.WriteFloat(duration);

        // 0 - Effects
        wr.WriteInt(0);

        // 1 - Hint Parameters
        wr.WriteInt(1);

        // String Parameter
        wr.WriteByte(0);
        wr.WriteString(string.Empty);

        // Message
        wr.WriteString(message);

        SendMirrorData(wr);
    }

    /// <summary>
    /// Sends object spawn start and finish messages to the client.
    /// </summary>
    public void SpawnObjects()
    {
        SendMirrorData<ObjectSpawnStartedMessage>();
        SendMirrorData<ObjectSpawnFinishedMessage>();
    }

    /// <summary>
    /// Sets the role of the client.
    /// </summary>
    /// <param name="role">The role to set.</param>
    public void SetRole(RoleTypeId role)
    {
        NetworkWriter wr = new NetworkWriter();
        wr.WriteUShort(NetworkMessages.RoleSyncInfo);

        wr.WriteUInt(NetworkIdentityId);
        wr.WriteSByte((sbyte)role);
        wr.WriteRelativePosition(new RelativePosition(new UnityEngine.Vector3(0f, 0f, 0f)));
        wr.WriteUShort(0);

        SendMirrorData(wr);
    }

    /// <summary>
    /// The player object associated with this client.
    /// </summary>
    public PlayerObject Object;

    /// <summary>
    /// Spawns the player object for this client.
    /// </summary>
    public void SpawnPlayer()
    {
        if (Object != null)
        {
            SiteLinkLogger.Info($"{Tag} Player object already exists for client ");
            return;
        }

        Object = new PlayerObject(World);
        Object.AssignOwner(this);

        Object.Position = new Vector3(0f, -299f, 0f);

        this.NetworkIdentityId = Object.NetworkId;
        SiteLinkLogger.Info($"{Tag} Created new player object for client.");
    }

    /// <summary>
    /// Destroys the specified network object for this client.
    /// </summary>
    /// <param name="networkIdentityId">The network identity ID of the object to destroy.</param>
    public void DestroyObject(uint networkIdentityId)
    {
        NetworkWriter wr = new NetworkWriter();

        wr.WriteUShort(NetworkMessages.ObjectDestroyMessage);
        wr.WriteUInt(networkIdentityId);

        SendMirrorData(wr);
    }

    /// <summary>
    /// Performs a fast round restart for this client.
    /// </summary>
    public void FastRoundrestart()
    {
        NetworkWriter wr = new NetworkWriter();

        wr.WriteUShort(NetworkMessages.RoundRestartMessage);

        //Restart Type ( Fast Restart )
        wr.WriteByte(1);

        SendMirrorData(wr);
    }

    private string _sceneToLoad = string.Empty;

    /// <summary>
    /// Sends a scene change message to the client.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    public void SendToScene(string sceneName)
    {
        NetworkWriter wr = new NetworkWriter();

        wr.WriteUShort(NetworkMessages.SceneMessage);

        //Scene name
        wr.WriteString(sceneName);
        //Scene operation ( Normal, LoadAdditive, UnloadAdditive )
        wr.WriteByte(0);
        //Custom handling
        wr.WriteBool(false);

        _sceneToLoad = sceneName;

        SendMirrorData(wr);
    }

    /// <summary>
    /// Marks the client as not ready.
    /// </summary>
    public void NotReady()
    {
        SendMirrorData<NotReadyMessage>();
    }

    /// <summary>
    /// Sets the random seed for the client.
    /// </summary>
    /// <param name="seed">The seed value.</param>
    public void SetSeed(int seed)
    {
        Seed = seed;
        SiteLinkLogger.Info($"{Tag} Set seed to {seed}");
    }

    public void SendSeed(int seed)
    {
        NetworkWriter wr = new NetworkWriter();

        wr.WriteUShort(NetworkMessages.SeedMessage);

        wr.WriteInt(seed);

        SendMirrorData(wr);
    }

    /// <summary>
    /// Sets the health value for the client.
    /// </summary>
    /// <param name="value">The health value.</param>
    public void SetHealth(float value)
    {
        NetworkWriter wr = new NetworkWriter();
        wr.WriteUShort(NetworkMessages.StatMessage);

        wr.WriteUInt(NetworkIdentityId);

        // 0 HealthStat
        // 1 AhpStat
        // 2 StaminaStat
        // 3 AdminFlagsStat
        // 4 HumeShieldStat
        // 5 Vigor Stat
        wr.WriteByte(0);

        wr.WriteByte((byte)StatMessageType.CurrentValue);

        int clampedValue = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.CeilToInt(value), 0, 65535);
        wr.WriteUShort((ushort)clampedValue);

        SendMirrorData(wr);
    }

    /// <summary>
    /// Invokes the connection response action.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="response">The response.</param>
    public void InvokeConnectionResponse(Server server, bool isSilent, IDisconnectResponse response)
    {
        ClientConnectionResponseEvent ev = new ClientConnectionResponseEvent(this, server, response);
        EventManager.Client.InvokeConnectionResponse(ev);

        if (ev.IsCancelled)
            return;

        switch (response)
        {
            case ServerIsOfflineResponse _:
                if (LastResponse is RoundRestartResponse restart)
                {
                    SendHint($"Server <color=orange>{server.Name}</color> is still <color=red>offline</color>!\nReconnecting...", 3f);
                    if (!Reconnect(server.Name, 2f, "is still restarting"))
                    {
                        Disconnect("Reached max reconnect attemps!");
                        break;
                    }
                }
                else
                {
                    if (!isSilent)
                        SendHint($"Server <color=orange>{server.Name}</color> is <color=red>offline</color>!", 3f);

                    AddToReconnectAttempt(TimeSpan.FromSeconds(4));
                    LastResponse = null;
                }
                break;
            case ServerIsFullResponse _:
                if (!isSilent)
                    SendHint($"Server <color=orange>{server.Name}</color> is full!", 3f);

                AddToReconnectAttempt(TimeSpan.FromSeconds(4));
                break;
            case DelayConnectionResponse delay:
                if (!isSilent)
                    SendHint($"Server <color=orange>{server.Name}</color> delayed connection by <color=green>{delay.TimeInSeconds}\nReconnecting...", 3f);

                Reconnect(server.Name, delay.TimeInSeconds, "delayed connection");
                break;
        }
    }

    /// <summary>
    /// Disconnects the client, optionally sending a message.
    /// </summary>
    /// <param name="message">The disconnect message.</param>
    public void Disconnect(string message = null)
    {
        if (Request != null)
        {
            Request.RejectWithMessage(message);
            Listener?.OnClientDisconneted(this, DisconnectReason.DisconnectPeerCalled);
            Dispose();

            SiteLinkLogger.Info($"{Tag} Disconnected{(string.IsNullOrEmpty(message) ? string.Empty : $" with reason '(f=yellow){message}(f=white)'")}");
            return;
        }

        if (message != null)
        {
            int id = -2106075371;

            NetworkWriter wr = new NetworkWriter();
            wr.WriteUShort(NetworkMessages.RpcMessage);
            wr.WriteUInt(NetworkIdentityId);
            wr.WriteByte(1);
            wr.WriteUShort((ushort)id);

            NetworkWriter wr2 = new NetworkWriter();
            wr2.WriteString(message);

            wr.WriteArraySegmentAndSize(wr2.ToArraySegment());

            SendMirrorData(wr);
            return;
        }

        Peer.Disconnect();
    }

    /// <summary>
    /// Disposes the client and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Connection.Dispose();
        BackupConnection.Dispose();

        if (Peer != null)
            Listener.ConnectedClients.Remove(Peer.Id);

        Listener.UnregisterClientInLookup(this);

        _scheduler = null;

        IsDisposing = true;
    }
}
