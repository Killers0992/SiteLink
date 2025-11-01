
using SiteLink.API.Handlers;

namespace SiteLink.Services;

public class ListService : BackgroundService

{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ScpServerListHandler scpService = new ScpServerListHandler(stoppingToken);

        await scpService.InitializeAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            await scpService.RefreshAsync();
            await Task.Delay(5000, stoppingToken);
        }
    }
}
