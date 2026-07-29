using PlayerRoles;
using UserSettings.ServerSpecific;

namespace SiteLink.Servers;

public class RemoteServer : Server
{
    Dictionary<int, string> _servers = new Dictionary<int, string>();
    ServerSpecificSettingBase[] _settings;

    public ServerSpecificSettingBase[] ServerSettings
    {
        get
        {
            if (_settings == null)
            {
                List<ServerSpecificSettingBase> settings = new List<ServerSpecificSettingBase>()
                {
                    new SSGroupHeader("Servers"),
                };

                // Ids start at the proxy range so they never collide with whatever the game
                // server or its plugins registered - the client keeps a single flat id space.
                int id = ProxySettingIdBase;
                foreach (string server in SiteLinkSettings.Singleton.ServersInSelector)
                {
                    Server target = Get<Server>(name: server);

                    if (target == null)
                        continue;

                    settings.Add(new SSButton(id, target.DisplayName, "Connect"));
                    _servers.Add(id, target.Name);
                    id++;
                }

                _settings = settings.ToArray();
            }
            return _settings;
        }
    }

    public RemoteServer(string name) : base(name) { }

    /// <summary>
    /// Appended to the entries pack the game server sends. Rewriting the game server's own
    /// pack is the only way both sets survive: the client stores one collection per server,
    /// so sending a competing pack would simply overwrite whichever arrived first.
    /// </summary>
    public override ServerSpecificSettingBase[] GetExtraServerSpecificEntries(Session session) => ServerSettings;

    public override void OnSessionSpawned(Session session)
    {
        // Vanilla servers and servers without a single server-specific setting never send an
        // entries pack, so there is nothing to append to and the selector has to be sent on
        // its own.
        if (session.HasGameServerSettings)
            return;

        session.Connection?.AsServer.ServerSpecificEntries(ServerSettings);
    }

    public override void OnSessionSSSReponse(Session session, int id)
    {
        if (!_servers.TryGetValue(id, out string server))
            return;

        // Use session client to connect to the selected server
        session.Connection?.Connect(server, true);
    }
}
