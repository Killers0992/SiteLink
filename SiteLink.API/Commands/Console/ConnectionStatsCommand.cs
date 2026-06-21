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
            SiteLinkLogger.Info(TranslationManager.Command("connstats.usage"), "connstats");
            return;
        }

        string userId = args[0];

        if (!RemoteConnection.TryGet(userId, out RemoteConnection connection))
        {
            SiteLinkLogger.Info(TranslationManager.Command(
                "player.not_found",
                new TranslationContext().With("user_id", userId)), "connstats");
            return;
        }

        string sessionText;
        if (connection.Session != null)
        {
            sessionText = TranslationManager.Command(
                "connstats.session",
                TranslationContext.For(connection.Session)
                    .With("uptime", connection.Session.Stats.Uptime.ToReadableString())
                    .With("to_server", FormatBytes(connection.Session.Stats.BytesToServer))
                    .With("from_server", FormatBytes(connection.Session.Stats.BytesFromServer))
                    .With("reconnections", connection.Session.Stats.ReconnectionCount)
                    .With("switches", connection.Session.Stats.ServerSwitchCount));
        }
        else
            sessionText = TranslationManager.Command("connstats.no_session");

        SiteLinkLogger.Info(TranslationManager.Command(
            "connstats.result",
            new TranslationContext()
                .With("user_id", connection.PreAuth.UserId)
                .With("ip", connection.PreAuth.IpAddress)
                .With("client_version", connection.PreAuth.ClientVersion)
                .With("connected_at", connection.Stats.ConnectedAt.ToString("yyyy-MM-dd HH:mm:ss"))
                .With("duration", connection.Stats.ConnectionDuration.ToReadableString())
                .With("last_activity", connection.Stats.LastActivityAt.ToString("yyyy-MM-dd HH:mm:ss"))
                .With("bytes_sent", FormatBytes(connection.Stats.BytesSent))
                .With("bytes_received", FormatBytes(connection.Stats.BytesReceived))
                .With("total_bytes", FormatBytes(connection.Stats.TotalBytes))
                .With("packets_sent", connection.Stats.PacketsSent)
                .With("packets_received", connection.Stats.PacketsReceived)
                .With("total_packets", connection.Stats.TotalPackets)
                .With("session", sessionText)), "connstats");
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
