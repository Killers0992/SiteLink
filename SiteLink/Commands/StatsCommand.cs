using SiteLink.API.Attributes;
using SiteLink.API.Commands;
using SiteLink.API.Metrics;
using SiteLink.API.Networking;
using System.Text;

namespace SiteLink.Commands;

public class StatsCommand
{
    [ConsoleCommand("stats")]
    public static void OnStatsCommand(string[] args)
    {
        if (args.Length == 0)
        {
            ShowSystemStats();
            return;
        }

        string subCommand = args[0].ToLower();

        switch (subCommand)
        {
            case "system":
                ShowSystemStats();
                break;
            case "services":
                ShowServicesStats();
                break;
            case "listeners":
                ShowListenersStats();
                break;
            case "connections":
                ShowConnectionsStats();
                break;
            case "sessions":
                ShowSessionsStats();
                break;
            default:
                SiteLinkLogger.Info("Unknown stats command. Usage: stats [system|services|listeners|connections|sessions]", "stats");
                break;
        }
    }

    private static void ShowSystemStats()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("System Statistics:");
        sb.AppendLine($" - Uptime: (f=green){SystemStats.Singleton.Uptime.ToReadableString()}(f=white)");
        sb.AppendLine($" - Memory: (f=cyan){SystemStats.Singleton.MemoryUsageMB}(f=white) MB");
        sb.AppendLine($" - Total Transferred: (f=yellow){FormatBytes(SystemStats.Singleton.TotalBytesTransferred)}(f=white)");
        sb.AppendLine($" - Active Connections: (f=green){Connection.ConnectionByUserId.Count}(f=white)");
        sb.AppendLine($" - Active Listeners: (f=green){Listener.List.Count}(f=white)");

        SiteLinkLogger.Info(sb.ToString(), "stats");
    }

    private static void ShowServicesStats()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Service Statistics:");


        sb.AppendLine($" SessionService:");
        var sessionService = Services.SessionService.Instance;
        if (sessionService != null)
        {
            sb.AppendLine($"  - Iterations: (f=green){sessionService.Stats.IterationsCompleted}(f=white)");
            sb.AppendLine($"  - Avg Time: (f=cyan){sessionService.Stats.AverageIterationTimeMs:F2}(f=white) ms");
            sb.AppendLine($"  - CPU: (f=yellow){sessionService.Stats.CpuUsagePercentage:F1}(f=white)%");
            sb.AppendLine($"  - Queue Depth: (f=green){sessionService.Stats.QueueDepth}(f=white)");
            sb.AppendLine($"  - Items Processed: (f=green){sessionService.Stats.TotalProcessedItems}(f=white)");
        }

        sb.AppendLine($" ListenersService:");
        var listenersService = Services.ListenersService.Instance;
        if (listenersService != null)
        {
            sb.AppendLine($"  - Iterations: (f=green){listenersService.Stats.IterationsCompleted}(f=white)");
            sb.AppendLine($"  - Avg Time: (f=cyan){listenersService.Stats.AverageIterationTimeMs:F2}(f=white) ms");
            sb.AppendLine($"  - CPU: (f=yellow){listenersService.Stats.CpuUsagePercentage:F1}(f=white)%");
            sb.AppendLine($"  - Items Processed: (f=green){listenersService.Stats.TotalProcessedItems}(f=white)");
        }

        sb.AppendLine($" CommandsService:");
        var commandsService = Services.CommandsService.Instance;
        if (commandsService != null)
        {
            sb.AppendLine($"  - Iterations: (f=green){commandsService.Stats.IterationsCompleted}(f=white)");
            sb.AppendLine($"  - Avg Time: (f=cyan){commandsService.Stats.AverageIterationTimeMs:F2}(f=white) ms");
            sb.AppendLine($"  - CPU: (f=yellow){commandsService.Stats.CpuUsagePercentage:F1}(f=white)%");
            sb.AppendLine($"  - Commands Executed: (f=green){commandsService.Stats.TotalProcessedItems}(f=white)");
        }

        SiteLinkLogger.Info(sb.ToString(), "stats");
    }

    private static void ShowListenersStats()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Listener Statistics:");

        foreach (var listener in Listener.List)
        {
            sb.AppendLine($" - (f=cyan){listener.Name}(f=white) [(f=green){listener.ListenAddress}:{listener.ListenPort}(f=white)]");
            sb.AppendLine($"   Connections: (f=green){listener.Stats.TotalConnections}(f=white) | Errors: (f=red){listener.Stats.ConnectionErrors}(f=white)");
            sb.AppendLine($"   Traffic Sent: (f=yellow){FormatBytes(listener.Stats.TotalBytesSent)}(f=white)");
            sb.AppendLine($"   Traffic Received: (f=yellow){FormatBytes(listener.Stats.TotalBytesReceived)}(f=white)");
            sb.AppendLine($"   Packets: (f=green){listener.Stats.TotalPacketsSent + listener.Stats.TotalPacketsReceived}(f=white) | Uptime: (f=darkcyan){listener.Stats.Uptime.ToReadableString()}(f=white)");
        }

        SiteLinkLogger.Info(sb.ToString(), "stats");
    }

    private static void ShowConnectionsStats()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Connection Statistics:");

        var connections = Connection.ConnectionByUserId.Values.ToList();
        if (connections.Count == 0)
        {
            sb.AppendLine(" No active connections.");
        }
        else
        {
            foreach (var connection in connections)
            {
                sb.AppendLine($" - (f=cyan){connection.PreAuth.UserId}(f=white) [(f=green){connection.Stats.ConnectionDuration.ToReadableString()}(f=white)]");
                sb.AppendLine($"   Sent: (f=yellow){FormatBytes(connection.Stats.BytesSent)}(f=white) | Received: (f=yellow){FormatBytes(connection.Stats.BytesReceived)}(f=white)");
                sb.AppendLine($"   Packets: (f=green){connection.Stats.TotalPackets}(f=white) | Session: {(connection.Session?.Server.Name ?? "None")}");
            }
        }

        SiteLinkLogger.Info(sb.ToString(), "stats");
    }

    private static void ShowSessionsStats()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Session Statistics:");

        foreach (var server in Server.List)
        {
            Session[] sessions = server.GetSessionsSnapshot();

            sb.AppendLine($" - Server (f=cyan){server.Name}(f=white) [ (f=green){sessions.Length}(f=white) sessions ]");

            foreach (Session session in sessions)
            {
                sb.AppendLine($"   - {(session.Connection?.PreAuth.UserId ?? "Unknown")}");
                sb.AppendLine($"     To Server: (f=yellow){FormatBytes(session.Stats.BytesToServer)}(f=white) | From Server: (f=yellow){FormatBytes(session.Stats.BytesFromServer)}(f=white)");
                sb.AppendLine($"     Uptime: (f=darkcyan){session.Stats.Uptime.ToReadableString()}(f=white) | Reconnections: (f=green){session.Stats.ReconnectionCount}(f=white)");
            }
        }

        SiteLinkLogger.Info(sb.ToString(), "stats");
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
