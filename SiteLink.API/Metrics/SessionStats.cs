namespace SiteLink.API.Metrics;

/// <summary>
/// Statistics tracking for a session (connection to a backend server).
/// </summary>
public class SessionStats
{
    private long _bytesToServer;
    private long _bytesFromServer;
    private int _reconnectionCount;
    private int _serverSwitchCount;

    /// <summary>
    /// Gets the timestamp when this session was created.
    /// </summary>
    public DateTime CreatedAt { get; internal set; }

    /// <summary>
    /// Gets the timestamp when this session successfully connected to a server.
    /// </summary>
    public DateTime? ConnectedAt { get; internal set; }

    /// <summary>
    /// Gets the number of reconnection attempts.
    /// </summary>
    public int ReconnectionCount => _reconnectionCount;

    /// <summary>
    /// Gets the number of server switches.
    /// </summary>
    public int ServerSwitchCount => _serverSwitchCount;

    /// <summary>
    /// Gets the total bytes sent to the server.
    /// </summary>
    public long BytesToServer => _bytesToServer;

    /// <summary>
    /// Gets the total bytes received from the server.
    /// </summary>
    public long BytesFromServer => _bytesFromServer;

    /// <summary>
    /// Gets the total traffic (bytes to + from server).
    /// </summary>
    public long TotalBytes => _bytesToServer + _bytesFromServer;

    /// <summary>
    /// Gets the time it took to connect to the server.
    /// </summary>
    public TimeSpan TimeToConnect => ConnectedAt.HasValue ? ConnectedAt.Value - CreatedAt : TimeSpan.Zero;

    /// <summary>
    /// Gets the session uptime (time since connected).
    /// </summary>
    public TimeSpan Uptime => ConnectedAt.HasValue ? DateTime.UtcNow - ConnectedAt.Value : TimeSpan.Zero;

    internal SessionStats()
    {
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records successful connection to a server.
    /// </summary>
    public void RecordConnected()
    {
        ConnectedAt ??= DateTime.UtcNow;
    }

    /// <summary>
    /// Records a reconnection attempt.
    /// </summary>
    public void RecordReconnection()
    {
        Interlocked.Increment(ref _reconnectionCount);
    }

    /// <summary>
    /// Records a server switch.
    /// </summary>
    public void RecordServerSwitch()
    {
        Interlocked.Increment(ref _serverSwitchCount);
    }

    /// <summary>
    /// Records bytes sent to the server.
    /// </summary>
    public void RecordBytesToServer(int count)
    {
        Interlocked.Add(ref _bytesToServer, count);
    }

    /// <summary>
    /// Records bytes received from the server.
    /// </summary>
    public void RecordBytesFromServer(int count)
    {
        Interlocked.Add(ref _bytesFromServer, count);
    }

    /// <summary>
    /// Gets a formatted summary of session statistics.
    /// </summary>
    public string GetSummary()
    {
        string status = ConnectedAt.HasValue ? $"Up {Uptime.ToReadableString()}" : "Connecting...";
        return $"To Server: {FormatBytes(BytesToServer)}, From Server: {FormatBytes(BytesFromServer)}, {status}";
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
