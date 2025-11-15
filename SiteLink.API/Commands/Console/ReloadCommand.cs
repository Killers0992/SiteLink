using SiteLink.Servers;

namespace SiteLink.API.Commands;

public class ReloadCommand
{
    [ConsoleCommand("reload")]
    public static void OnReloadCommand(string[] args)
    {
        string[] oldServers = GetServers();
        string[] oldListeners = GetListeners();

        SiteLinkSettings.Reload();

        string[] newServers = GetServers();
        string[] newListeners = GetListeners();

        string[] removedServers = oldServers.Except(newServers).ToArray();
        string[] removedListeners = oldListeners.Except(newListeners).ToArray();

        foreach (string server in removedServers)
        {
            Server.Unregister(server);
        }

        foreach (string listener in removedListeners)
        {
            Server.Unregister(listener);
        }

        string[] addedServers = newServers.Except(oldServers).ToArray();
        string[] addedListeners = newListeners.Except(oldListeners).ToArray();

        foreach (string server in addedServers)
        {
            Server.Register(new RemoteServer(server));
        }

        foreach (string listener in addedListeners)
        {
            Listener.Register(new Listener(listener));
        }

        SiteLinkLogger.Info("Reloaded");
    }

    static string[] GetServers() => 
        SiteLinkSettings.Singleton.Servers
            .Select(x => x.Name)
            .ToArray();

    static string[] GetListeners() =>
        SiteLinkSettings.Singleton.Listeners
            .Select(x => x.Name)
            .ToArray();
}
