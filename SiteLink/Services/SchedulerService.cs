using SiteLink.API.Threading;

namespace SiteLink.Services;

/// <summary>
/// Background service that initializes and manages the scheduler.
/// </summary>
public class SchedulerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Scheduler.Initialize(stoppingToken);
    }
}
