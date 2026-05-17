namespace SiteLink.API.Metrics;

/// <summary>
/// Thread-safe statistics tracking for a single connection.
/// </summary>
public class ConnectionStats
{
    private long _bytesSent;
    private long _bytesReceived;
    private long _packetsSent;
    private long _packetsReceived;
    private long _messagesSent;
    private long _messagesReceived;

    /// <summary>
    /// Gets the timestamp when this connection was established.
    /// </summary>
    public DateTime ConnectedAt { get; internal set; }

    /// <summary>
    /// Gets the timestamp of the last network activity.
    /// </summary>
    public DateTime LastActivityAt { get; internal set; }

    /// <summary>
    /// Gets the duration of this connection.
    /// </summary>
    public TimeSpan ConnectionDuration => DateTime.UtcNow - ConnectedAt;

    /// <summary>
    /// Gets the total number of bytes sent to the client.
    /// </summary>
    public long BytesSent => _bytesSent;

    /// <summary>
    /// Gets the total number of bytes received from the client.
    /// </summary>
    public long BytesReceived => _bytesReceived;

    /// <summary>
    /// Gets the total number of packets sent to the client.
    /// </summary>
    public long PacketsSent => _packetsSent;

    /// <summary>
    /// Gets the total number of packets received from the client.
    /// </summary>
    public long PacketsReceived => _packetsReceived;

    /// <summary>
    /// Gets the total number of Mirror messages sent to the client.
    /// </summary>
    public long MessagesSent => _messagesSent;

    /// <summary>
    /// Gets the total number of Mirror messages received from the client.
    /// </summary>
    public long MessagesReceived => _messagesReceived;

    /// <summary>
    /// Gets the total traffic (bytes sent + received).
    /// </summary>
    public long TotalBytes => _bytesSent + _bytesReceived;

    /// <summary>
    /// Gets the total packets (sent + received).
    /// </summary>
    public long TotalPackets => _packetsSent + _packetsReceived;

    internal ConnectionStats()
    {
        ConnectedAt = DateTime.UtcNow;
        LastActivityAt = ConnectedAt;
    }

    /// <summary>
    /// Records bytes sent to the client.
    /// </summary>
    public void RecordBytesSent(int count)
    {
        Interlocked.Increment(ref _packetsSent);
        Interlocked.Add(ref _bytesSent, count);
        UpdateActivity();
    }

    /// <summary>
    /// Records bytes received from the client.
    /// </summary>
    public void RecordBytesReceived(int count)
    {
        Interlocked.Increment(ref _packetsReceived);
        Interlocked.Add(ref _bytesReceived, count);
        UpdateActivity();
    }

    /// <summary>
    /// Records a Mirror message sent to the client.
    /// </summary>
    public void RecordMessageSent()
    {
        Interlocked.Increment(ref _messagesSent);
    }

    /// <summary>
    /// Records a Mirror message received from the client.
    /// </summary>
    public void RecordMessageReceived()
    {
        Interlocked.Increment(ref _messagesReceived);
    }

    private void UpdateActivity()
    {
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets a formatted summary of connection statistics.
    /// </summary>
    public string GetSummary()
    {
        return $"Sent: {FormatBytes(BytesSent)}, Received: {FormatBytes(BytesReceived)}, Duration: {ConnectionDuration.ToReadableString()}";
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
