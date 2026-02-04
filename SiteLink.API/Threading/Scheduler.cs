using System.Runtime.CompilerServices;

namespace SiteLink.API.Threading;

/// <summary>
/// Central scheduler for managing thread marshaling, scheduled tasks, and repeating tasks.
/// </summary>
public static class Scheduler
{
    private static readonly ConcurrentDictionary<int, ThreadWorkQueue> _threadQueues = new();
    private static readonly ConcurrentDictionary<string, int> _threadNameToId = new();
    private static ScheduledTaskService _taskService;

    /// <summary>
    /// Initializes the scheduler.
    /// </summary>
    public static void Initialize()
    {
        _taskService = new ScheduledTaskService();
        _taskService.Start();
    }

    /// <summary>
    /// Registers a thread's work queue with the scheduler.
    /// </summary>
    /// <param name="threadId">The managed thread ID.</param>
    /// <param name="threadName">The name of the thread.</param>
    /// <param name="queueCallback">Callback to invoke when work is queued.</param>
    public static void RegisterThread(int threadId, string threadName, Action<Action> queueCallback)
    {
        SiteLinkLogger.Info("Registering thread '" + threadName + "' with ID " + threadId, "Scheduler");

        _threadQueues[threadId] = new ThreadWorkQueue(threadName, queueCallback);
        _threadNameToId[threadName] = threadId;
    }

    /// <summary>
    /// Marshals an action to execute on the thread that owns the specified object.
    /// </summary>
    /// <param name="threadOwner">The object that owns the target thread.</param>
    /// <param name="action">The action to execute.</param>
    public static void Execute(object threadOwner, Action action)
    {
        int targetThreadId = ThreadOwner.GetOwnerId(threadOwner);

        if (targetThreadId == -1)
        {
            // Object not registered with thread affinity, execute synchronously
            action();
            return;
        }

        if (_threadQueues.TryGetValue(targetThreadId, out var queue))
        {
            queue.Enqueue(action);
        }
        else
        {
            // Thread not registered, execute synchronously
            SiteLinkLogger.Warn($"Thread {targetThreadId} not registered with scheduler, executing synchronously", "Scheduler");
            action();
        }
    }

    /// <summary>
    /// Marshals an action to execute on a named thread.
    /// </summary>
    /// <param name="threadName">The name of the target thread.</param>
    /// <param name="action">The action to execute.</param>
    public static void ExecuteOnNamedThread(string threadName, Action action)
    {
        if (_threadNameToId.TryGetValue(threadName, out int threadId))
        {
            if (_threadQueues.TryGetValue(threadId, out var queue))
            {
                queue.Enqueue(action);
                return;
            }
        }

        // Thread not found, execute synchronously
        SiteLinkLogger.Warn($"Thread '{threadName}' not registered with scheduler, executing synchronously", "Scheduler");
        action();
    }

    /// <summary>
    /// Schedules a repeating task.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="intervalMs">The interval in milliseconds.</param>
    /// <param name="targetThread">The target thread name (optional). If null, runs on scheduler thread.</param>
    public static void RepeatTask(Action action, int intervalMs, string targetThread = null)
    {
        _taskService?.ScheduleRepeating(action, intervalMs, targetThread);
    }

    /// <summary>
    /// Schedules a one-shot task.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="when">The time to execute the action.</param>
    /// <param name="targetThread">The target thread name (optional). If null, runs on scheduler thread.</param>
    public static void ScheduleTask(Action action, DateTime when, string targetThread = null)
    {
        _taskService?.ScheduleOneShot(action, when, targetThread);
    }

    /// <summary>
    /// Schedules a one-shot task with a delay.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="delayMs">The delay in milliseconds.</param>
    /// <param name="targetThread">The target thread name (optional). If null, runs on scheduler thread.</param>
    public static void ScheduleDelayedTask(Action action, int delayMs, string targetThread = null)
    {
        _taskService?.ScheduleOneShot(action, DateTime.UtcNow.AddMilliseconds(delayMs), targetThread);
    }
}
