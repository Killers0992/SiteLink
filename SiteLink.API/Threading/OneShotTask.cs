namespace SiteLink.API.Threading;

/// <summary>
/// A scheduled task that runs once at a specific time.
/// </summary>
internal class OneShotTask : ScheduledTask
{
    private readonly DateTime _scheduledTime;
    private bool _hasExecuted;

    /// <summary>
    /// Initializes a new instance of the OneShotTask class.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="scheduledTime">The time to execute the action.</param>
    /// <param name="targetThread">The target thread name (optional).</param>
    public OneShotTask(Action action, DateTime scheduledTime, string targetThread)
        : base(action, targetThread)
    {
        _scheduledTime = scheduledTime;
        _hasExecuted = false;
    }

    /// <summary>
    /// Determines whether the task should run at the given time.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>True if the task should run, false otherwise.</returns>
    public override bool ShouldRun(DateTime now)
    {
        return !_hasExecuted && now >= _scheduledTime;
    }

    /// <summary>
    /// Executes the task and marks it as executed.
    /// </summary>
    public override void Execute()
    {
        if (_hasExecuted)
            return;

        _hasExecuted = true;

        if (string.IsNullOrEmpty(TargetThread))
        {
            Action();
        }
        else
        {
            Scheduler.ExecuteOnNamedThread(TargetThread, Action);
        }
    }

    /// <summary>
    /// Gets whether the task has executed.
    /// </summary>
    public bool HasExecuted => _hasExecuted;
}
