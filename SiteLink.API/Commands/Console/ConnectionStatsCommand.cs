using SiteLink.API.Networking.Connections;
using System.Text;

namespace SiteLink.Commands;

public class ConnectionStatsCommand
{
    [ConsoleCommand("connstats")]
    public static void OnConnStatsCommand(string[] args)
    {
        if (args.Length == 0)
        {
            SiteLinkLogger.Info("Usage: connstats <userId>", "connstats");
            return;
        }

        string userId = args[0];

        if (!RemoteConnection.TryGet(userId, out RemoteConnection connection))
        {
            SiteLinkLogger.Info($"Connection for user '(f=yellow){userId}(f=white)' not found.", "connstats");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Connection Details:");
        sb.AppendLine($" - User ID: (f=cyan){connection.PreAuth.UserId}(f=white)");
        sb.AppendLine($" - IP Address: (f=green){connection.PreAuth.IpAddress}(f=white)");
        sb.AppendLine($" - Client Version: (f=darkcyan){connection.PreAuth.ClientVersion}(f=white)");
        sb.AppendLine();
        sb.AppendLine("Statistics:");
        sb.AppendLine($" - Connected At: (f=darkcyan){connection.Stats.ConnectedAt:yyyy-MM-dd HH:mm:ss}(f=white)");
        sb.AppendLine($" - Duration: (f=green){connection.Stats.ConnectionDuration.ToReadableString()}(f=white)");
        sb.AppendLine($" - Last Activity: (f=darkcyan){connection.Stats.LastActivityAt:yyyy-MM-dd HH:mm:ss}(f=white)");
        sb.AppendLine();
        sb.AppendLine("Network Traffic:");
        sb.AppendLine($" - Bytes Sent: (f=yellow){FormatBytes(connection.Stats.BytesSent)}(f=white)");
        sb.AppendLine($" - Bytes Received: (f=yellow){FormatBytes(connection.Stats.BytesReceived)}(f=white)");
        sb.AppendLine($" - Total Traffic: (f=yellow){FormatBytes(connection.Stats.TotalBytes)}(f=white)");
        sb.AppendLine();
        sb.AppendLine("Packets:");
        sb.AppendLine($" - Packets Sent: (f=green){connection.Stats.PacketsSent}(f=white)");
        sb.AppendLine($" - Packets Received: (f=green){connection.Stats.PacketsReceived}(f=white)");
        sb.AppendLine($" - Total Packets: (f=green){connection.Stats.TotalPackets}(f=white)");
        sb.AppendLine();
        sb.AppendLine("Session:");

        if (connection.Session != null)
        {
            sb.AppendLine($" - Server: (f=cyan){connection.Session.Server?.Name ?? "Connecting..."}(f=white)");
            sb.AppendLine($" - Session Uptime: (f=green){connection.Session.Stats.Uptime.ToReadableString()}(f=white)");
            sb.AppendLine($" - To Server: (f=yellow){FormatBytes(connection.Session.Stats.BytesToServer)}(f=white)");
            sb.AppendLine($" - From Server: (f=yellow){FormatBytes(connection.Session.Stats.BytesFromServer)}(f=white)");
            sb.AppendLine($" - Reconnections: (f=darkcyan){connection.Session.Stats.ReconnectionCount}(f=white)");
            sb.AppendLine($" - Server Switches: (f=darkcyan){connection.Session.Stats.ServerSwitchCount}(f=white)");
        }
        else
        {
            sb.AppendLine($" - No active session");
        }

        SiteLinkLogger.Info(sb.ToString(), "connstats");
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
