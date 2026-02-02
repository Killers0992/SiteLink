using System.Runtime.CompilerServices;

namespace SiteLink.API.Threading;

/// <summary>
/// Internal class that stores thread affinity information for an object.
/// </summary>
internal class ThreadAffinityInfo
{
    private string _threadName;
    private int _ownerThreadId;

    /// <summary>
    /// Initializes the thread affinity information.
    /// </summary>
    /// <param name="threadName">The name of the owning thread.</param>
    /// <param name="threadId">The managed thread ID of the owning thread.</param>
    public void Initialize(string threadName, int threadId)
    {
        _threadName = threadName;
        _ownerThreadId = threadId;
    }

    /// <summary>
    /// Verifies that the current thread is the owner thread. Throws if not.
    /// </summary>
    /// <param name="method">The name of the method being called (for error message).</param>
    public void Verify([CallerMemberName] string method = null)
    {
        if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
        {
            throw new ThreadAffinityException(
                $"{method} must be called on {_threadName} thread. " +
                $"Current: {Thread.CurrentThread.ManagedThreadId}, " +
                $"Owner: {_ownerThreadId}");
        }
    }

    /// <summary>
    /// Gets the thread ID of the owner.
    /// </summary>
    public int OwnerThreadId => _ownerThreadId;
}
