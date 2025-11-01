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

                int id = 0;
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

    public RemoteServer(ServerSettings settings) : base(settings) { }

    public override void OnClientSpawned(Client client) => client.SendServerSpecificEntries(ServerSettings);
    public override void OnClientSSSReponse(Client client, int id)
    {
        if (!_servers.TryGetValue(id, out string server))
            return;

        client.Connect(server);
    }
}
