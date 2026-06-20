namespace SiteLink.API.Metrics;

/// <summary>
/// Performance statistics for a background service.
/// </summary>
public class ServiceStats
{
    private long _iterationsCompleted;
    private long _totalProcessedItems;
    private long _totalProcessingTimeMs;
    private int _currentQueueDepth;

    /// <summary>
    /// Gets the name of this service.
    /// </summary>
    public string ServiceName { get; }

    /// <summary>
    /// Gets the timestamp when this service was started.
    /// </summary>
    public DateTime StartedAt { get; internal set; }

    /// <summary>
    /// Gets the number of iterations completed by this service.
    /// </summary>
    public long IterationsCompleted => _iterationsCompleted;

    /// <summary>
    /// Gets the total number of work items processed.
    /// </summary>
    public long TotalProcessedItems => _totalProcessedItems;

    /// <summary>
    /// Gets the current work queue depth.
    /// </summary>
    public int QueueDepth => _currentQueueDepth;

    /// <summary>
    /// Gets the average iteration time in milliseconds.
    /// </summary>
    public double AverageIterationTimeMs => _iterationsCompleted > 0
        ? (double)_totalProcessingTimeMs / _iterationsCompleted
        : 0;

    /// <summary>
    /// Gets the most recent iteration time.
    /// </summary>
    public TimeSpan LastIterationTime { get; private set; }

    /// <summary>
    /// Gets the service uptime.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartedAt;

    /// <summary>
    /// Gets the estimated CPU usage percentage (processing time / total time).
    /// </summary>
    public double CpuUsagePercentage
    {
        get
        {
            var uptime = Uptime.TotalMilliseconds;
            if (uptime < 100) return 0; // Not enough data

            var processingTime = _totalProcessingTimeMs;
            return (processingTime / uptime) * 100;
        }
    }

    public ServiceStats(string serviceName)
    {
        ServiceName = serviceName;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records an iteration with the given processing time.
    /// </summary>
    public void RecordIteration(TimeSpan processingTime)
    {
        Interlocked.Increment(ref _iterationsCompleted);
        Interlocked.Add(ref _totalProcessingTimeMs, (long)processingTime.TotalMilliseconds);
        LastIterationTime = processingTime;
    }

    /// <summary>
    /// Records processing of a work item.
    /// </summary>
    public void RecordProcessedItem()
    {
        Interlocked.Increment(ref _totalProcessedItems);
    }

    /// <summary>
    /// Records processing of multiple work items.
    /// </summary>
    public void RecordProcessedItems(int count)
    {
        Interlocked.Add(ref _totalProcessedItems, count);
    }

    /// <summary>
    /// Updates the current queue depth.
    /// </summary>
    public void UpdateQueueDepth(int depth)
    {
        _currentQueueDepth = depth;
    }

    /// <summary>
    /// Gets a formatted summary of service statistics.
    /// </summary>
    public string GetSummary()
    {
        return $"Iterations: {_iterationsCompleted}, Avg Time: {AverageIterationTimeMs:F2}ms, CPU: {CpuUsagePercentage:F1}%, Queue: {_currentQueueDepth}";
    }
}
