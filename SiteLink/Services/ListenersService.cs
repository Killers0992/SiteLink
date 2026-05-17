using SiteLink.API.Metrics;
using SiteLink.API.Models;
using SiteLink.Servers;
using System.Diagnostics;

namespace SiteLink.Services;

public class ListenersService : BackgroundService
{
    public static ListenersService Instance { get; private set; }

    public ServiceStats Stats { get; } = new ServiceStats("ListenersService");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Instance = this;
        Listener.Token = stoppingToken;

        try
        {
            foreach (ServerSettings settings in SiteLinkSettings.Singleton.Servers)
            {
                Server.Register(new RemoteServer(settings.Name));
            }

            foreach (ListenerSettings settings in SiteLinkSettings.Singleton.Listeners)
            {
                new Listener(settings.Name);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }


        await RunServerUpdater(stoppingToken);
    }

    private async Task RunServerUpdater(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                foreach (var server in Server.RegisteredServers.Values)
                {
                    try
                    {
                        server.OnUpdate();
                        Stats.RecordProcessedItem();
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

            stopwatch.Stop();
            Stats.RecordIteration(stopwatch.Elapsed);

            await Task.Delay(10, token);
        }
    }
}
