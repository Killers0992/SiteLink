namespace SiteLink.API.Threading;

/// <summary>
/// A scheduled task that repeats at a fixed interval.
/// </summary>
internal class RepeatingTask : ScheduledTask
{
    private readonly int _intervalMs;
    private DateTime _lastRun;

    /// <summary>
    /// Initializes a new instance of the RepeatingTask class.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="intervalMs">The interval in milliseconds.</param>
    /// <param name="targetThread">The target thread name (optional).</param>
    public RepeatingTask(Action action, int intervalMs, string targetThread)
        : base(action, targetThread)
    {
        _intervalMs = intervalMs;
        _lastRun = DateTime.UtcNow;
    }

    /// <summary>
    /// Determines whether the task should run at the given time.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>True if the task should run, false otherwise.</returns>
    public override bool ShouldRun(DateTime now)
    {
        return (now - _lastRun).TotalMilliseconds >= _intervalMs;
    }

    /// <summary>
    /// Executes the task and updates the last run time.
    /// </summary>
    public override void Execute()
    {
        _lastRun = DateTime.UtcNow;

        if (string.IsNullOrEmpty(TargetThread))
        {
            Action();
        }
        else
        {
            Scheduler.ExecuteOnNamedThread(TargetThread, Action);
        }
    }
}
