using System.Runtime.CompilerServices;

namespace SiteLink.API.Threading;

/// <summary>
/// Manages thread affinity for objects. Allows registration and verification of thread ownership.
/// </summary>
public static class ThreadOwner
{
    private static readonly ConditionalWeakTable<object, ThreadAffinityInfo> _affinity = new();

    /// <summary>
    /// Registers an object as being owned by a specific thread.
    /// </summary>
    /// <param name="obj">The object to register.</param>
    /// <param name="threadName">The name of the owning thread.</param>
    /// <param name="threadId">The managed thread ID of the owning thread.</param>
    public static void Register(object obj, string threadName, int threadId)
    {
        //SiteLinkLogger.Info("Register " + obj.GetType().FullName  + " on thread " + threadName +  " (" + threadId + ")");

        _affinity.GetOrCreateValue(obj).Initialize(threadName, threadId);
    }

    /// <summary>
    /// Verifies that the current thread is the owner of the object. Throws ThreadAffinityException if not.
    /// This method only works in DEBUG builds - it's compiled out in Release.
    /// </summary>
    /// <param name="obj">The object to verify.</param>
    /// <param name="method">The name of the method being called (for error message).</param>
    public static void Verify(object obj, [CallerMemberName] string method = null)
    {
#if DEBUG
        _affinity.TryGetValue(obj, out var info);
        info?.Verify(method);
#endif
    }

    /// <summary>
    /// Gets the thread ID that owns the specified object.
    /// </summary>
    /// <param name="obj">The object to query.</param>
    /// <returns>The owner thread ID, or -1 if the object is not registered.</returns>
    public static int GetOwnerId(object obj)
    {
        if (_affinity.TryGetValue(obj, out var info))
        {
            return info.OwnerThreadId;
        }
        return -1;
    }

    /// <summary>
    /// Checks whether the current thread is the owner of the specified object.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the current thread owns the object, false otherwise.</returns>
    public static bool IsOwnerThread(object obj)
    {
        if (_affinity.TryGetValue(obj, out var info))
        {
            return Thread.CurrentThread.ManagedThreadId == info.OwnerThreadId;
        }
        return false;
    }
}
