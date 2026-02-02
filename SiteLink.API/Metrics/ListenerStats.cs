namespace SiteLink.API.Metrics;

/// <summary>
/// Aggregated statistics for a listener.
/// </summary>
public class ListenerStats
{
    private long _totalConnections;
    private long _totalBytesSent;
    private long _totalBytesReceived;
    private long _totalPacketsSent;
    private long _totalPacketsReceived;
    private long _connectionErrors;

    /// <summary>
    /// Gets the timestamp when this listener was started.
    /// </summary>
    public DateTime StartedAt { get; internal set; }

    /// <summary>
    /// Gets the total number of connections accepted.
    /// </summary>
    public long TotalConnections => _totalConnections;

    /// <summary>
    /// Gets the total number of bytes sent to all clients.
    /// </summary>
    public long TotalBytesSent => _totalBytesSent;

    /// <summary>
    /// Gets the total number of bytes received from all clients.
    /// </summary>
    public long TotalBytesReceived => _totalBytesReceived;

    /// <summary>
    /// Gets the total number of packets sent to all clients.
    /// </summary>
    public long TotalPacketsSent => _totalPacketsSent;

    /// <summary>
    /// Gets the total number of packets received from all clients.
    /// </summary>
    public long TotalPacketsReceived => _totalPacketsReceived;

    /// <summary>
    /// Gets the total number of connection errors/disconnections.
    /// </summary>
    public long ConnectionErrors => _connectionErrors;

    /// <summary>
    /// Gets the total traffic (bytes sent + received).
    /// </summary>
    public long TotalBytes => _totalBytesSent + _totalBytesReceived;

    /// <summary>
    /// Gets the uptime of this listener.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartedAt;

    internal ListenerStats()
    {
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a new connection being accepted.
    /// </summary>
    public void RecordConnectionAccepted()
    {
        Interlocked.Increment(ref _totalConnections);
    }

    /// <summary>
    /// Records a connection error or disconnection.
    /// </summary>
    public void RecordConnectionError()
    {
        Interlocked.Increment(ref _connectionErrors);
    }

    /// <summary>
    /// Records bytes sent to clients.
    /// </summary>
    public void RecordBytesSent(long bytes)
    {
        Interlocked.Add(ref _totalBytesSent, bytes);
    }

    /// <summary>
    /// Records bytes received from clients.
    /// </summary>
    public void RecordBytesReceived(long bytes)
    {
        Interlocked.Add(ref _totalBytesReceived, bytes);
    }

    /// <summary>
    /// Records packets sent to clients.
    /// </summary>
    public void RecordPacketsSent(long count)
    {
        Interlocked.Add(ref _totalPacketsSent, count);
    }

    /// <summary>
    /// Records packets received from clients.
    /// </summary>
    public void RecordPacketsReceived(long count)
    {
        Interlocked.Add(ref _totalPacketsReceived, count);
    }

    /// <summary>
    /// Gets a formatted summary of listener statistics.
    /// </summary>
    public string GetSummary()
    {
        return $"Connections: {_totalConnections}, Traffic: {FormatBytes(TotalBytes)}, Errors: {_connectionErrors}";
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F2} GB",
            >= MB => $"{bytes / (double)MB:F2} MB",
            >= KB => $"{bytes / (double)KB:F2} KB",
            _ => $"{bytes} B"
        };
    }
}
