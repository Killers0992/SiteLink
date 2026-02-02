namespace SiteLink.API.Metrics;

/// <summary>
/// System-wide aggregated statistics.
/// </summary>
public class SystemStats
{
    public static SystemStats Singleton { get; } = new SystemStats();

    private long _totalBytesTransferred;

    /// <summary>
    /// Gets the timestamp when the application started.
    /// </summary>
    public DateTime StartTime { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the application uptime.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartTime;

    /// <summary>
    /// Gets the total number of bytes transferred (in + out).
    /// </summary>
    public long TotalBytesTransferred => _totalBytesTransferred;

    /// <summary>
    /// Gets the current memory usage in MB.
    /// </summary>
    public long MemoryUsageMB => GC.GetTotalMemory(false) / (1024 * 1024);

    /// <summary>
    /// Records bytes transferred.
    /// </summary>
    public void RecordBytesTransferred(long bytes)
    {
        Interlocked.Add(ref _totalBytesTransferred, bytes);
    }

    /// <summary>
    /// Gets a formatted summary of system statistics.
    /// </summary>
    public string GetSummary()
    {
        return $"Uptime: {Uptime.ToReadableString()}, Memory: {MemoryUsageMB} MB, Total Transferred: {FormatBytes(_totalBytesTransferred)}";
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
