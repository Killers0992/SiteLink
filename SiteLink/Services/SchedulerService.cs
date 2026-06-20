using SiteLink.API.Threading;

namespace SiteLink.Services;

/// <summary>
/// Background service that initializes and manages the scheduler.
/// </summary>
public class SchedulerService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize the scheduler
        Scheduler.Initialize();

        SiteLinkLogger.Info("Scheduler initialized", "Scheduler");

        // The scheduler runs on its own task, so we just wait for cancellation
        return Task.CompletedTask;
    }
}
