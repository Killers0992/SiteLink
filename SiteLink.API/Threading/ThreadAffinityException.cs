namespace SiteLink.API.Threading;

/// <summary>
/// Exception thrown when a method with thread affinity is called from the wrong thread.
/// </summary>
public class ThreadAffinityException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the ThreadAffinityException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ThreadAffinityException(string message) : base(message)
    {
    }
}
