namespace SiteLink.API.Threading;

/// <summary>
/// Marks a class or method as having thread affinity - it must only be accessed from a specific thread.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ThreadAffinedAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the thread that owns this object.
    /// </summary>
    public string ThreadName { get; }

    /// <summary>
    /// Initializes a new instance of the ThreadAffinedAttribute class.
    /// </summary>
    /// <param name="threadName">The name of the thread that owns this object.</param>
    public ThreadAffinedAttribute(string threadName)
    {
        ThreadName = threadName;
    }
}
