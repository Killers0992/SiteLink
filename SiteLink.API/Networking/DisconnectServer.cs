using SiteLink.API.Networking.Connections;
using SiteLink.API.Threading;

namespace SiteLink.API.Networking;

internal sealed class DisconnectServer : Server
{
    private readonly ConcurrentDictionary<Session, string> _messages = new();
    private readonly DisconnectWorld _world;

    public DisconnectServer() : base(
        "disconnect",
        new ServerSettings
        {
            DisplayName = "Disconnect",
            Address = "-local-",
            Port = 0,
            MaxClients = int.MaxValue
        },
        isSimulated: true)
    {
        _world = new DisconnectWorld(this);
    }

    public void Prepare(Session session, string message)
    {
        _messages[session] = message;
    }

    public void Cancel(Session session)
    {
        _messages.TryRemove(session, out _);
    }

    public override bool OnSessionConnecting(Session session) => _messages.ContainsKey(session);

    public override void OnSessionReady(Session session)
    {
        session.World = _world;
    }

    public override void OnSessionDisconnected(Session session)
    {
        _messages.TryRemove(session, out _);
    }

    internal bool TryTakeMessage(Session session, out string message) => _messages.TryRemove(session, out message);

    private sealed class DisconnectWorld : World
    {
        private readonly DisconnectServer _server;

        public DisconnectWorld(DisconnectServer server) : base("Disconnect")
        {
            _server = server;
            AddWaypoint(Vector3.zero);
        }

        public override void OnLoad(Session session)
        {
            session.SpawnPlayer(Vector3.zero);
        }

        public override void OnObjectsSpawned(Session session)
        {
            session.Connection.AsServer.Seed(0);

            if (!_server.TryTakeMessage(session, out string message))
                return;

            RemoteConnection connection = session.Connection;
            Scheduler.Execute(connection, () =>
            {
                if (connection.IsDisposed || connection.Session != session || session.Player == null)
                    return;

                connection.SendDisconnectError(message);
            });
        }
    }
}
