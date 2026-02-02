namespace SiteLink.API.Threading;

/// <summary>
/// Abstract base class for scheduled tasks.
/// </summary>
internal abstract class ScheduledTask
{
    /// <summary>
    /// Gets the action to execute.
    /// </summary>
    protected Action Action { get; }

    /// <summary>
    /// Gets the name of the target thread (optional).
    /// </summary>
    protected string TargetThread { get; }

    /// <summary>
    /// Initializes a new instance of the ScheduledTask class.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="targetThread">The target thread name (optional).</param>
    protected ScheduledTask(Action action, string targetThread)
    {
        Action = action;
        TargetThread = targetThread;
    }

    /// <summary>
    /// Determines whether the task should run at the given time.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>True if the task should run, false otherwise.</returns>
    public abstract bool ShouldRun(DateTime now);

    /// <summary>
    /// Executes the task.
    /// </summary>
    public abstract void Execute();
}
