using SiteLink.API.Core;
using SiteLink.API.Models;
using SiteLink.Servers;

namespace SiteLink.Services;

public class ListenersService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Listener.Token = stoppingToken;

        try
        {
            foreach (ServerSettings settings in SiteLinkSettings.Singleton.Servers)
            {
                Server.Register(new RemoteServer(settings.Name));
            }

            foreach (ListenerSettings settings in SiteLinkSettings.Singleton.Listeners)
            {
                Listener.Register(new Listener(settings.Name));
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex);
        }


        await RunServerUpdater(stoppingToken);
    }

    private async Task RunServerUpdater(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {

            try
            {
                foreach (var server in Server.RegisteredServers.Values)
                {
                    try
                    {
                        server.OnUpdate();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            
            await Task.Delay(10, token);
        }
    }
}
