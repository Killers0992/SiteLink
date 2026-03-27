namespace SiteLink.API.Threading;

/// <summary>
/// A thread-safe work queue for a specific thread.
/// </summary>
internal class ThreadWorkQueue
{
    private readonly string _threadName;
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly Action<Action> _queueCallback;

    /// <summary>
    /// Initializes a new instance of the ThreadWorkQueue class.
    /// </summary>
    /// <param name="threadName">The name of the thread.</param>
    /// <param name="queueCallback">Callback to invoke when work is queued.</param>
    public ThreadWorkQueue(string threadName, Action<Action> queueCallback)
    {
        _threadName = threadName;
        _queueCallback = queueCallback;
    }

    /// <summary>
    /// Enqueues an action to be processed on the target thread.
    /// </summary>
    /// <param name="action">The action to enqueue.</param>
    public void Enqueue(Action action)
    {
        _queue.Enqueue(action);
        _queueCallback(ProcessQueue);
    }

    /// <summary>
    /// Processes all queued actions.
    /// </summary>
    public void ProcessQueue()
    {
        while (_queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Error($"Error processing work queue for {_threadName}: {ex}", _threadName);
            }
        }
    }
}
