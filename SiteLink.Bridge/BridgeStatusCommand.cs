using System;
using System.Collections.Generic;
using System.Text;
using CommandSystem;
using SiteLink.API;

namespace SiteLink.Bridge
{
    /// <summary>
    /// Reports the state of the bridge: whether it is connected, what it last told the
    /// proxy, and which target servers the proxy advertised.
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class BridgeStatusCommand : ICommand
    {
        public string Command => "slbridge";

        public string[] Aliases => new[] { "sitelinkbridge" };

        public string Description => "Shows the SiteLink bridge connection state and the player count reported to the proxy.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            BridgeConfig config = SiteLinkBridgePlugin.Instance?.Config;

            StringBuilder builder = new StringBuilder();

            builder.AppendLine("SiteLink bridge status");
            builder.AppendLine($"  connected: {SiteLinkBridge.IsConnected}");
            builder.AppendLine(config == null
                ? "  proxy: <plugin not enabled>"
                : $"  proxy: {config.Ip}:{config.Port}");

            int players = PlayerCountReporter.CountPlayers(out int raw, out int dummies);

            builder.AppendLine($"  players now: counted={players} raw={raw} dummy={dummies}");
            builder.AppendLine($"  last reported: {FormatReported()}");

            List<string> servers = SiteLinkBridge.TargetServers;

            if (servers == null || servers.Count == 0)
            {
                builder.AppendLine("  target servers: none received from the proxy");
            }
            else
            {
                builder.AppendLine($"  target servers ({servers.Count}):");
                foreach (string server in servers)
                    builder.AppendLine($"    - {server}");
            }

            response = builder.ToString();
            return true;
        }

        private static string FormatReported()
        {
            if (PlayerCountReporter.LastReportedCount < 0)
                return "nothing reported yet";

            return $"{PlayerCountReporter.LastReportedCount}/{PlayerCountReporter.LastReportedMax}";
        }
    }
}
