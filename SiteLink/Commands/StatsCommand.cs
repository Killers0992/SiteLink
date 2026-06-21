using SiteLink.API.Attributes;
using SiteLink.API.Metrics;
using SiteLink.API.Networking.Connections;

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
                SiteLinkLogger.Info(TranslationManager.Command("stats.usage"), "stats");
                break;
        }
    }

    private static void ShowSystemStats()
    {
        SiteLinkLogger.Info(TranslationManager.Command(
            "stats.system",
            new TranslationContext()
                .With("uptime", SystemStats.Singleton.Uptime.ToReadableString())
                .With("memory", SystemStats.Singleton.MemoryUsageMB)
                .With("bytes", FormatBytes(SystemStats.Singleton.TotalBytesTransferred))
                .With("connections", RemoteConnection.ConnectionByUserId.Count)
                .With("listeners", Listener.List.Count)), "stats");
    }

    private static void ShowServicesStats()
    {
        List<string> rows = new();
        var sessionService = Services.SessionService.Instance;
        if (sessionService != null)
            rows.Add(FormatService("SessionService", sessionService.Stats));

        var listenersService = Services.ListenersService.Instance;
        if (listenersService != null)
            rows.Add(FormatService("ListenersService", listenersService.Stats));

        var commandsService = Services.CommandsService.Instance;
        if (commandsService != null)
            rows.Add(FormatService("CommandsService", commandsService.Stats));

        SiteLinkLogger.Info(TranslationManager.Command(
            "stats.services",
            new TranslationContext().With("services", string.Join("\n", rows))), "stats");
    }

    private static void ShowListenersStats()
    {
        List<string> rows = new();

        foreach (var listener in Listener.List)
        {
            rows.Add(TranslationManager.Command(
                "stats.listener",
                new TranslationContext()
                    .With("listener", listener.Name)
                    .With("address", listener.ListenAddress)
                    .With("port", listener.ListenPort)
                    .With("connections", listener.Stats.TotalConnections)
                    .With("errors", listener.Stats.ConnectionErrors)
                    .With("sent", FormatBytes(listener.Stats.TotalBytesSent))
                    .With("received", FormatBytes(listener.Stats.TotalBytesReceived))
                    .With("packets", listener.Stats.TotalPacketsSent + listener.Stats.TotalPacketsReceived)
                    .With("uptime", listener.Stats.Uptime.ToReadableString())));
        }

        SiteLinkLogger.Info(TranslationManager.Command(
            "stats.listeners",
            new TranslationContext().With("listeners", string.Join("\n", rows))), "stats");
    }

    private static void ShowConnectionsStats()
    {
        var connections = RemoteConnection.ConnectionByUserId.Values.ToList();
        List<string> rows = new();
        if (connections.Count == 0)
            rows.Add(TranslationManager.Command("stats.no_connections"));
        else
        {
            foreach (var connection in connections)
            {
                rows.Add(TranslationManager.Command(
                    "stats.connection",
                    TranslationContext.For(connection.Session)
                        .With("user_id", connection.PreAuth.UserId)
                        .With("duration", connection.Stats.ConnectionDuration.ToReadableString())
                        .With("sent", FormatBytes(connection.Stats.BytesSent))
                        .With("received", FormatBytes(connection.Stats.BytesReceived))
                        .With("packets", connection.Stats.TotalPackets)
                        .With("server_name", connection.Session?.Server?.Name ?? "None")));
            }
        }

        SiteLinkLogger.Info(TranslationManager.Command(
            "stats.connections",
            new TranslationContext().With("connections", string.Join("\n", rows))), "stats");
    }

    private static void ShowSessionsStats()
    {
        List<string> serverRows = new();

        foreach (var server in Server.List)
        {
            Session[] sessions = server.GetSessionsSnapshot();

            List<string> players = new();

            foreach (Session session in sessions)
            {
                players.Add(TranslationManager.Command(
                    "stats.session",
                    TranslationContext.For(session, server)
                        .With("user_id", session.Connection?.PreAuth.UserId ?? "Unknown")
                        .With("sent", FormatBytes(session.Stats.BytesToServer))
                        .With("received", FormatBytes(session.Stats.BytesFromServer))
                        .With("uptime", session.Stats.Uptime.ToReadableString())
                        .With("reconnections", session.Stats.ReconnectionCount)));
            }

            serverRows.Add(TranslationManager.Command(
                "stats.server",
                TranslationContext.For(server: server)
                    .With("count", sessions.Length)
                    .With("players", string.Join("\n", players))));
        }

        SiteLinkLogger.Info(TranslationManager.Command(
            "stats.sessions",
            new TranslationContext().With("sessions", string.Join("\n", serverRows))), "stats");
    }

    private static string FormatService(string name, ServiceStats stats) =>
        TranslationManager.Command(
            "stats.service",
            new TranslationContext()
                .With("service", name)
                .With("iterations", stats.IterationsCompleted)
                .With("average", stats.AverageIterationTimeMs.ToString("F2"))
                .With("cpu", stats.CpuUsagePercentage.ToString("F1"))
                .With("queue", stats.QueueDepth)
                .With("processed", stats.TotalProcessedItems));

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
