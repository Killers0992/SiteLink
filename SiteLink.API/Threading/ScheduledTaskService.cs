namespace SiteLink.API.Threading;

/// <summary>
/// Internal service that manages and executes scheduled tasks.
/// </summary>
internal class ScheduledTaskService
{
    private readonly ConcurrentBag<ScheduledTask> _tasks = new();
    private Task _task;

    /// <summary>
    /// Starts the scheduled task service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public void Start(CancellationToken cancellationToken = default)
    {
        _task = Task.Run(() => RunAsync(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Schedules a repeating task.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="intervalMs">The interval in milliseconds.</param>
    /// <param name="targetThread">The target thread name (optional).</param>
    public void ScheduleRepeating(Action action, int intervalMs, string targetThread = null)
    {
        _tasks.Add(new RepeatingTask(action, intervalMs, targetThread));
    }

    /// <summary>
    /// Schedules a one-shot task.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="when">The time to execute the action.</param>
    /// <param name="targetThread">The target thread name (optional).</param>
    public void ScheduleOneShot(Action action, DateTime when, string targetThread = null)
    {
        _tasks.Add(new OneShotTask(action, when, targetThread));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var tasksToProcess = _tasks.ToList();

            foreach (var task in tasksToProcess)
            {
                try
                {
                    if (task.ShouldRun(now))
                    {
                        task.Execute();
                    }
                }
                catch (Exception ex)
                {
                    SiteLinkLogger.Error($"Error executing scheduled task: {ex}", "Scheduler");
                }
            }

            // Clean up executed one-shot tasks
            if (tasksToProcess.OfType<OneShotTask>().Any(t => t.HasExecuted))
            {
                // This is a simple approach - for production, consider a more efficient cleanup strategy
            }

            await Task.Delay(10, cancellationToken);
        }
    }
}
